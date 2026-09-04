# Migrations — Evidências

## Semana 4: CreateIncidentesAndBairros

### Comando rodado

```bash
dotnet ef migrations add CreateIncidentesAndBairros -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

### Migration aplicada ao banco

```bash
dotnet ef database update -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

### Saída

```
Build succeeded.
Applying migration '20260902183546_CreateIncidentesAndBairros'.
CREATE TABLE [Bairros] (
    [Id] int NOT NULL IDENTITY,
    [Nome] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Bairros] PRIMARY KEY ([Id])
);
CREATE TABLE [Incidentes] (
    [Id] int NOT NULL IDENTITY,
    [DataHora] datetime2 NOT NULL,
    [Tipo] int NOT NULL,
    [Severidade] int NULL,
    [BairroId] int NOT NULL,
    [Descricao] nvarchar(max) NULL,
    CONSTRAINT [PK_Incidentes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Incidentes_Bairros_BairroId] FOREIGN KEY ([BairroId]) REFERENCES [Bairros] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_Incidentes_BairroId] ON [Incidentes] ([BairroId]);
Done.
```

### Por quê

- **Bairro entidade própria:** normalização — elimina repetição de strings
- **FK com CASCADE:** deletar bairro deleta incidentes relacionados
- **Índice em BairroId:** consultas `WHERE BairroId = X` ficam mais rápidas (seek)

---

## Atendimento (1:N com Incidente)

### Entidade criada

`src/Mobilidade.Domain/Modelos/Atendimento.cs`

Campos: Id, IncidenteId (FK), OrgaoResponsavel, DataHora, Situacao

### Migration criada

```bash
dotnet ef migrations add AtendimentoTable -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

### Aplicada ao banco

```bash
dotnet ef database update -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

### Saída

```
Applying migration '20260903185136_AtendimentoTable'.
CREATE TABLE [Atendimento] (
    [Id] int NOT NULL IDENTITY,
    [IncidenteId] int NOT NULL,
    [OrgaoResponsavel] nvarchar(100) NOT NULL,
    [DataHora] datetime2 NOT NULL,
    [Situacao] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Atendimento] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Atendimento_Incidentes_IncidenteId] FOREIGN KEY ([IncidenteId]) REFERENCES [Incidentes] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_Atendimento_IncidenteId] ON [Atendimento] ([IncidenteId]);
Done.
```

### Por quê

- Atendimento é 1:N com Incidente
- FK com CASCADE: deletar incidente deleta atendimentos
- Índice em IncidenteId: queries filtrando por incidente ficam mais rápidas

---

## Migration destrutiva: SeveridadeObrigatoria

### Contexto

`Severidade` deixa de ser nullable (`Severidade?`) e passa a ser obrigatória.
Novo valor `NaoTriada` adicionado ao final do enum (Baixa=0, Media=1, Alta=2,
NaoTriada=3) para não alterar os valores numéricos já gravados no banco.

Incidentes que tinham `Severidade = NULL` precisam de uma decisão: viram
`NaoTriada`, tanto no `Up` (aplicar) quanto — de volta pra `NULL` — no `Down`
(reverter), para não perder informação na reversão.

### Migrations existentes

```bash
dotnet ef migrations list -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

```
20260902183546_CreateIncidentesAndBairros
20260903185136_AtendimentoTable
20260903212651_SeveridadeObrigatoria
```

### Estado antes da migration

```sql
SELECT Id, Severidade FROM Incidentes
```

```
Id          Severidade
----------- -----------
          2        NULL
```

### Aplicando (Up)

```bash
dotnet ef database update -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

Trecho relevante da saída:

```
Applying migration '20260903212651_SeveridadeObrigatoria'.
UPDATE Incidentes SET Severidade = 3 WHERE Severidade IS NULL
ALTER TABLE [Incidentes] ALTER COLUMN [Severidade] int NOT NULL;
ALTER TABLE [Incidentes] ADD DEFAULT 3 FOR [Severidade];
Done.
```

Estado do dado depois do Up:

```
Id          Severidade
----------- -----------
          2           3
```

### Revertendo (Down)

```bash
dotnet ef database update AtendimentoTable -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

Trecho relevante da saída:

```
Reverting migration '20260903212651_SeveridadeObrigatoria'.
ALTER TABLE [Incidentes] ALTER COLUMN [Severidade] int NULL;
UPDATE Incidentes SET Severidade = NULL WHERE Severidade = 3
Done.
```

Estado do dado depois do Down — prova que reverteu de verdade:

```sql
SELECT Id, Severidade FROM Incidentes
```

```
Id          Severidade
----------- -----------
          2        NULL
```

### Reaplicando (Up de novo)

```bash
dotnet ef database update -s src/Mobilidade.Api -p src/Mobilidade.Infrastructure
```

Estado do dado depois de reaplicar:

```sql
SELECT Id, Severidade FROM Incidentes
```

```
Id          Severidade
----------- -----------
          2           3
```

### Por quê

- **NaoTriada no final do enum:** preserva os valores numéricos já gravados
  (Baixa=0, Media=1, Alta=2) — inserir no início bagunçaria dado histórico.
- **`migrationBuilder.Sql()` explícito no Up:** garante que o `defaultValue`
  do `AlterColumn` não seja a única fonte de verdade sobre a conversão —
  fica claro que `NULL` vira `NaoTriada`, não `Baixa` (valor 0, que seria
  o padrão "acidental" do EF Core se não fosse tratado).
- **`Down` reverte o dado, não só a estrutura:** sem o `UPDATE` de volta
  pra `NULL`, reverter a migration deixaria os incidentes marcados como
  `NaoTriada` permanentemente — perdendo a informação de que eles nunca
  tiveram severidade definida.

---

## Transação com falha provocada (ServicoAberturaIncidente)

### Contexto

`ServicoAberturaIncidente.Executar` grava um `Incidente` e um `Atendimento`
inicial dentro de uma única transação. Se a gravação do `Atendimento`
falhar, o rollback desfaz o `Incidente` também — o negócio exige que todo
incidente nasça com atendimento; um incidente órfão (sem atendimento) é
um estado inconsistente.

Falha provocada de propósito: `Atendimento.OrgaoResponsavel` recebe uma
string de 101 caracteres, violando o `HasMaxLength(100)` configurado em
`AtendimentoConfiguration`. O SQL Server rejeita o INSERT.

### Estado do banco ANTES do teste

```bash
sqlcmd -S localhost,1433 -U sa -P "Mobilidade#2026" -d MobilidadeDb -Q "SELECT COUNT(*) AS TotalIncidentes FROM Incidentes; SELECT COUNT(*) AS TotalAtendimentos FROM Atendimento"
```

```
TotalIncidentes
---------------
              1

TotalAtendimentos
-----------------
                0
```

### Rodando o teste (dispara a falha)

```bash
dotnet test --filter Executar_ComAtendimentoInvalido
```

```
Resumo do teste: total: 1; falhou: 0; bem-sucedido: 1; ignorado: 0; duração: 2,2s
Construir êxito(s) com 1 aviso(s) em 3,8s
```

O teste espera (`Assert.ThrowsAsync<DbUpdateException>`) que a gravação do
`Atendimento` lance uma exceção real do banco — não um erro forçado
artificialmente. A exceção capturada durante o desenvolvimento foi:

```
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while
saving the entity changes. See the inner exception for details.
 ---> Microsoft.Data.SqlClient.SqlException: String or binary data
would be truncated in table 'MobilidadeDb.dbo.Atendimento', column
'OrgaoResponsavel'. Truncated value: '****...'.
```

### Estado do banco DEPOIS do teste

```bash
sqlcmd -S localhost,1433 -U sa -P "Mobilidade#2026" -d MobilidadeDb -Q "SELECT COUNT(*) AS TotalIncidentes FROM Incidentes; SELECT COUNT(*) AS TotalAtendimentos FROM Atendimento"
```

```
TotalIncidentes
---------------
              1

TotalAtendimentos
-----------------
                0
```

**Idêntico ao estado anterior** — o `Incidente` que já tinha sido gravado
pelo primeiro `SaveChangesAsync()` foi desfeito pelo `RollbackAsync()`
quando o segundo `SaveChangesAsync()` (do Atendimento) falhou.

### Sem transação, o que aconteceria

Sem a transação, o `Incidente` já teria sido persistido de verdade no
primeiro `SaveChangesAsync()` (INSERT commitado imediatamente). O erro no
`Atendimento` não teria como desfazer essa gravação anterior — o banco
ficaria com um `Incidente` órfão, sem nenhum atendimento associado,
violando a regra de negócio silenciosamente, sem qualquer aviso.