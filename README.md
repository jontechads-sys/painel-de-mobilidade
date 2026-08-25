# painel-mobilidade

Monorepo do Painel de Mobilidade. Este é o esqueleto da Semana 1: estrutura de
pastas, solution .NET e infraestrutura local do banco. Ainda não há regra de
negócio nem integração real com o banco — isso vem nas semanas seguintes.

## Estrutura

```
painel-mobilidade/
├── docker-compose.yml
├── README.md
├── .gitignore
└── backend/
    ├── Mobilidade.sln
    └── src/
        ├── Mobilidade.Api/
        ├── Mobilidade.Domain/
        ├── Mobilidade.Application/
        ├── Mobilidade.Infrastructure/
        ├── Mobilidade.Tests/
        └── Mobilidade.IncidentesConsole/
```

Camadas (referências de projeto já ligadas, formalização vem nas semanas 5–6):

- **Domain**: sem dependências.
- **Application**: depende de Domain.
- **Infrastructure**: depende de Domain e Application.
- **Api**: depende de Domain, Application e Infrastructure.
- **Tests**: depende de Domain, Application e IncidentesConsole.
- **IncidentesConsole**: projeto console **temporário**, autocontido (não referencia Domain/Application/Infrastructure). Ver justificativa abaixo.

## Console de incidentes (Semana 1, Parte 2)

`backend/src/Mobilidade.IncidentesConsole` importa um CSV fictício de
incidentes de trânsito e responde consultas via LINQ. Roda assim:

```bash
cd backend/src/Mobilidade.IncidentesConsole
dotnet run                        # usa dados/incidentes.csv (copiado pro output)
dotnet run -- caminho/outro.csv   # ou aponta pra outro arquivo
```

**Por que um projeto console separado, e não dentro de `Mobilidade.Api`:**
`Mobilidade.Api` é um host web (`Program.cs` sobe Kestrel) — não tem onde
pendurar um fluxo "lê arquivo, imprime relatório, encerra" sem forçar um
encaixe artificial (um controller/endpoint só pra rodar uma vez, ou dois
entry points concorrendo no mesmo processo). O enunciado já chama esse
console de temporário, e a separação em camadas de domínio só é formalizada
nas semanas 5–6 — então também evitei acoplar o modelo de `Incidente` ao
`Mobilidade.Domain` agora: forçar essa forma cedo é comprometer um formato
que ainda não foi pensado para durar. O projeto é autocontido; só o
`Mobilidade.Tests` referencia ele, para testar o parser e as consultas.

**Decisões de `Nullable`** (todas em `Modelos/Incidente.cs` e
`Importacao/CsvIncidenteImportador.cs`):

| Campo | Ausente no CSV vira | Por quê |
|---|---|---|
| `Id`, `DataHora`, `Tipo` | linha inteira descartada (com aviso) | sem eles o registro não tem chave, eixo temporal ou classificação mínima — não dá pra inventar um valor sem distorcer o relatório |
| `Severidade` (`Severidade?`) | `null`, registro mantido | incidente pode legitimamente ainda não ter sido triado; consultas que dependem de severidade simplesmente não contam essas linhas, sem `if` especial |
| `Bairro` (`string`, não nulo) | `"(nao informado)"` | é problema de qualidade do dado, não ausência válida — vira categoria própria no `GroupBy` em vez de exigir tratamento de chave nula |
| `Descricao` (`string?`) | `null` | texto livre genuinamente opcional; `null` (ninguém preencheu) fica distinto de string vazia |

**Método genérico reutilizável:** `RelatorioConsultas.ContarPor<TChave>` faz
o `GroupBy` + `Count` + `OrderByDescending` usado por duas das quatro
consultas pedidas (total por tipo, top bairros por severidade alta).
`CsvIncidenteImportador` também tem um `TryParseEnum<TEnum>` privado,
reaproveitado no parse de `Tipo` e `Severidade`.

O CSV (`dados/incidentes.csv`) tem 50 linhas válidas de dados fictícios mais
duas linhas propositalmente malformadas (id não numérico, data inválida) —
para provar que o importador descarta a linha ruim com um aviso em vez de
quebrar a leitura do arquivo inteiro.

## Pré-requisitos

- [.NET SDK 8+](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) com Docker Compose

## Banco de dados (SQL Server)

```bash
docker compose up -d
```

Sobe só o SQL Server, em `localhost:1433`, com usuário `sa` e senha definida em
`MSSQL_SA_PASSWORD` (veja `.env.example`; padrão `Mobilidade#2026` se nenhuma
`.env` for criada). Os dados ficam no volume nomeado `sqlserver_data` e
sobrevivem a `docker compose down` — só somem com `docker compose down -v`.

```bash
docker compose down     # para os containers, mantém os dados
docker compose down -v  # para os containers E apaga o volume (dados perdidos)
```

## Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test
```

## Minimal API vs Controller

O CRUD de incidentes (`Criar`, `Listar`, `BuscarPorId`, `Atualizar`, `Remover`) está em `IncidentesController` (`Mobilidade.Api/Controllers/IncidentesController.cs`). A importação via CSV (`POST /api/incidentes/importar`) está como minimal API direto no `Program.cs`.

**Por quê:** as cinco ações do CRUD giram em torno do mesmo recurso e se beneficiam do que `[ApiController]` dá de graça — validação automática de `DataAnnotations` nos DTOs (400 sem checar `ModelState.IsValid` manualmente) e convenções de roteamento REST já prontas. A importação é uma ação avulsa, sem DTO complexo, que só aciona um serviço injetado — não tem múltiplas ações relacionadas que justifiquem a estrutura de um controller inteiro só para ela.

## SOLID aplicado

### SRP — Responsabilidade Única

**Arquivo:** `Mobilidade.Infrastructure/Repositorio/RepositorioIncidentesEmMemoria.cs`
**Classe:** `RepositorioIncidentesEmMemoria`

Essa classe só sabe guardar, buscar, atualizar e remover incidentes de uma coleção em memória. Ela não sabe nada sobre HTTP, validação de entrada ou de onde os dados vieram (CSV, request manual, etc.) — isso é responsabilidade de outras classes (`IncidentesController` e `ImportadorIncidentesCsv`, respectivamente).

**O que quebraria se violado:** se o repositório também tratasse requisições HTTP (ex: retornasse `IActionResult`, chamasse `NotFound()`), ele passaria a depender do ASP.NET Core — impossível reutilizá-lo em outro contexto (um worker, um teste de unidade puro, um futuro serviço de importação em lote) sem arrastar a camada web junto.

---

### OCP — Aberto para extensão, fechado para modificação

**Arquivo:** `Mobilidade.Application/Repositorio/IIncidenteRepository.cs`
**Classe:** interface `IIncidenteRepository`

O `IncidentesController` depende só da interface, nunca da implementação concreta. Isso significa que dá para adicionar uma nova forma de persistência — por exemplo, um `RepositorioIncidentesEfCore` na Semana 4, quando o banco entrar — sem alterar uma linha do controller ou dos endpoints. Só troca o registro no `Program.cs`.

**O que quebraria se violado:** sem a interface, trocar de "em memória" para "banco de dados" exigiria editar o controller inteiro (iniection do tipo concreto, chamadas diferentes), arriscando quebrar comportamento já testado só para adicionar uma nova forma de guardar dado.

---

### LSP — Substituição de Liskov

**Arquivo:** `Mobilidade.Application/Repositorio/IIncidenteRepository.cs` (mesma interface do OCP, ângulo diferente)

Qualquer implementação de `IIncidenteRepository` — a atual em memória, ou uma futura com banco — precisa se comportar da forma que o `IncidentesController` espera: `BuscarPorId` retorna `null` (não lança exceção) quando não encontra, `Remover` retorna `bool` indicando sucesso, `Criar` sempre devolve o incidente com o Id definitivo preenchido. Enquanto uma nova implementação respeitar esse contrato de comportamento (não só a assinatura de tipos), ela pode substituir a atual sem o controller perceber a troca.

**O que quebraria se violado:** se uma implementação futura decidisse, por exemplo, lançar exceção em vez de retornar `null` quando o Id não existe, o controller pararia de devolver 404 corretamente — o comportamento visível pra quem consome a API mudaria só por trocar a implementação por baixo, o que é exatamente o que LSP proíbe.

---

### ISP — Segregação de Interfaces

**Arquivos:** `IIncidenteRepository.cs` e `IIncidenteImportador.cs`

Em vez de uma interface genérica única (tipo `IServicoDeIncidentes` com métodos de CRUD **e** de importação **e** de relatório misturados), existem duas interfaces pequenas e focadas: `IIncidenteRepository` (persistência) e `IIncidenteImportador` (leitura de CSV). Quem precisa só importar dados (o endpoint de importação) depende apenas de `IIncidenteImportador` — não é forçado a implementar ou enxergar métodos de CRUD que não usa.

**O que quebraria se violado:** com uma interface única e inchada, qualquer implementação (inclusive futuros testes com dublês/mocks) precisaria satisfazer métodos que não usa, só para compilar — aumentando acoplamento sem necessidade.

---

### DIP — Inversão de Dependência

**Arquivo:** `Mobilidade.Api/Controllers/IncidentesController.cs`
**Classe:** `IncidentesController`

O controller recebe `IIncidenteRepository` no construtor — uma abstração — nunca instancia `RepositorioIncidentesEmMemoria` diretamente. Quem decide qual implementação real usar é o `Program.cs`, na hora do registro de DI (`AddSingleton<IIncidenteRepository, RepositorioIncidentesEmMemoria>()`). O mesmo vale para o endpoint de importação, que depende de `IIncidenteImportador`, não de `ImportadorIncidentesCsv`.

**O que quebraria se violado:** se o controller instanciasse `new RepositorioIncidentesEmMemoria()` diretamente, o código de alto nível (regras de HTTP) passaria a depender de detalhe de baixo nível (como os dados são armazenados) — impossível trocar a implementação sem editar o controller, e impossível testar o controller isoladamente com um repositório falso.