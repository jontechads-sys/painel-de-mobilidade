# Decisões de modelagem — Semana 5

Repositório: painel-mobilidade

Cada decisão segue 4 campos: o que foi decidido, qual alternativa foi
considerada, por que a alternativa foi rejeitada, o que mudaria de ideia.

---

## 1. `NaoTriada` não é alcançável na criação de `Incidente`

**O que foi decidido**

Construtor de `Incidente` rejeita severidade `NaoTriada`; linhas do CSV sem
severidade serão descartadas, como já acontece com `Id`, `DataHora` e `Tipo`.

Registros legados no banco com `Severidade.NaoTriada` (valor 3 na coluna)
continuam existindo e são lidos normalmente — a rejeição vale só para
criação de incidentes novos, não para leitura de dados já gravados.

**Alternativa considerada**

Deixar `NaoTriada` como severidade válida na criação (tratando-a como
estado legítimo de negócio, não como ausência de valor), ou fazer a
importação de CSV preencher automaticamente com `Baixa` quando o campo
vier vazio.

**Por que foi rejeitada**

`NaoTriada` é ausência de valor disfarçada de valor — ela só existe porque
uma migration anterior converteu `NULL` para ela. Aceitar isso na criação
seria continuar aceitando `null`, só que sem o compilador avisar.
Preencher com `Baixa` seria pior: inventaria um dado que não existe, e a
severidade errada tem consequência real (prioridade de atendimento).

**O que mudaria de ideia**

Se a triagem virasse uma etapa reconhecida do fluxo de negócio — incidente
nasce sem severidade, alguém classifica depois — `NaoTriada` deixaria de
ser ausência e viraria um estado legítimo. Isso exigiria redesenhar a
invariante 2 inteira (qual operação passaria a exigir severidade real), e
não coube no escopo desta semana.

**Evidência**

Testes `Criar_ComSeveridadeNaoTriada_LancaArgumentException` em
`IncidenteTests.cs`. Ciclo comentar/descomentar registrado no
`EVIDENCIAS.md`, seção "Invariante 2".

---

## 2. `DataHora` e `Severidade` não são editáveis após a criação

**O que foi decidido**

`DataHora` e `Severidade` de `Incidente` têm `private set`, atribuídas só
no construtor. As linhas que as sincronizavam em `Atualizar` (em
`RepositorioIncidentesEfCore` e `RepositorioIncidentesEmMemoria`) foram
removidas.

**Alternativa considerada**

Manter os dois campos editáveis via `Atualizar`, permitindo correção
direta se algum dado fosse digitado errado.

**Por que foi rejeitada**

Um incidente registra o que aconteceu, não uma opinião atual sobre o que
aconteceu. Se esses campos pudessem ser reescritos livremente, o registro
deixaria de ser confiável como histórico — alguém poderia mudar quando ou
com que gravidade um incidente ocorreu, sem deixar rastro de que isso
mudou.

**O que mudaria de ideia**

Se corrigir esses campos fosse uma operação de negócio real e frequente,
a solução seria um método de domínio nomeado (`CorrigirDataHora`,
`Reclassificar`) que valida e registra a mudança — não voltar a expor
setter público.

**Evidência**

Commits `c19cb5b` (remoção de `DataHora`) e `a606eff` (remoção de
`Severidade`). Seção "Regras movidas" do `EVIDENCIAS.md`, Regras 2 e 3,
com arquivo, linha e trecho de código de antes/depois.

---

## 3. `Encerrado` como `bool`, não como enum de status

**O que foi decidido**

Estado de encerramento do `Incidente` é um `bool` simples
(`Encerrado`), protegido por `Encerrar()`.

**Alternativa considerada**

Um `enum StatusIncidente { Aberto, Encerrado }`, ou algo mais rico
cobrindo estados intermediários.

**Por que foi rejeitada**

Resolve o problema de hoje — só existem dois estados reais no domínio
atual. Criar um enum de status com um valor a mais seria generalizar
antes de ter um segundo estado real para justificar.

**O que mudaria de ideia**

Se surgisse um terceiro estado genuíno (por exemplo, "em andamento" como
etapa distinta de "aberto"), o `bool` deixaria de bastar e o enum seria
necessário.

**Evidência**

Migration `AdicionaEncerradoAoIncidente`, documentada no `EVIDENCIAS.md`.

---

## 4. Construção de `Incidente` via factory method, construtor privado

**O que foi decidido**

O construtor de `Incidente` é `private`. O único caminho de criação é
`Incidente.Abrir(...)`, que monta o incidente e o atendimento inicial
juntos antes de devolver o objeto.

**Alternativa considerada**

Manter o construtor público e confiar em um serviço de aplicação
(`ServicoAberturaIncidente`) para sempre criar incidente e atendimento
juntos.

**Por que foi rejeitada**

Essa garantia dependia de disciplina: todo código que criasse um
`Incidente` precisava lembrar de passar pelo serviço certo. Isso já tinha
falhado antes — `RepositorioIncidentesEmMemoria` e `ImportadorIncidentesCsv`
criavam incidentes direto, sem atendimento, sem que ninguém percebesse até
o build quebrar. Com construtor privado, não existe caminho alternativo —
a garantia é estrutural, não comportamental.

**O que mudaria de ideia**

Nada muda essa decisão dentro do escopo atual — é a base da invariante 1.

**Evidência**

`EVIDENCIAS.md`, seção "Invariante 1", com a explicação de por que não há
ciclo comentar/falhar/descomentar (a violação é impossível de compilar,
não apenas rejeitada em runtime).

---

## 5. `Severidade` de enum para value object (`record`)

**O que foi decidido**

`Severidade` deixou de ser `enum` e virou `sealed record`, com quatro
instâncias estáticas (`Baixa`, `Media`, `Alta`, `NaoTriada`), construtor
privado, e o comportamento `DeveriaEscalonar(TimeSpan)`.

**Alternativa considerada**

Manter `Severidade` como `enum` e adicionar comportamento através de
métodos de extensão (`static` em outra classe), sem trocar o tipo em si.

**Por que foi rejeitada**

Métodos de extensão sobre enum funcionam, mas não impedem a criação de
valores fora da lista fechada (`(Severidade)99` continuaria compilando).
Como `record` com construtor privado, a invariante "severidade válida"
deixou de precisar de checagem em runtime — virou impossibilidade
estrutural, e um teste inteiro (`Criar_ComSeveridadeForaDoEnum_...`)
deixou de fazer sentido e foi removido.

**O que mudaria de ideia**

Se a mudança tivesse custado mais do que o tempo disponível permitia,
teria sido revertida para a alternativa mais simples (enum + extensão).

**Evidência**

`Severidade.cs`. `IncidenteConfiguration.cs`, com o `ValueConverter`
ensinando o EF Core a persistir a classe como `int`. `SeveridadeTests.cs`,
provando igualdade por valor e o comportamento `DeveriaEscalonar`.

---

## 6. `Periodo` criado como value object independente, sem integração

**O que foi decidido**

`Periodo` foi criado como segundo value object (`Inicio`, `Fim?`,
`Duracao`, `EstaEmAberto`, `Contem`), mas não foi integrado em `Incidente`
— `DataHora` continua sendo um `DateTime` simples.

**Alternativa considerada**

Substituir `Incidente.DataHora` por um `Periodo`, unificando abertura e
encerramento num único objeto.

**Por que foi rejeitada**

A troca de `Severidade` para value object já custou uma tarde inteira de
correção em cascata (49 ocorrências no código). Repetir isso para
`DataHora`/`Periodo` faltando poucas horas para o prazo era risco alto
sem necessidade — a issue pede um segundo VO que "faça sentido no
domínio", não que já esteja integrado em tudo.

**O que mudaria de ideia**

Com mais tempo disponível, integrar `Periodo` em `Incidente` (substituindo
`DataHora` e futuramente calculando duração real do incidente a partir de
`Encerrar()`) seria a evolução natural.

**Evidência**

`Periodo.cs`, `PeriodoTests.cs` (igualdade, invariante de `Fim < Inicio`,
cálculo de `Duracao`).