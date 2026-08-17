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
