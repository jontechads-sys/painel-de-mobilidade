# Migrations — Evidências

> **Nota:** todos os comandos abaixo assumem que você está dentro da pasta
> `backend/` (`cd backend` a partir da raiz do repositório, onde este
> arquivo está localizado).

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
sqlcmd -S localhost,1433 -U sa -P "Sua_Senha_Aqui" -d MobilidadeDb -Q "SELECT COUNT(*) AS TotalIncidentes FROM Incidentes; SELECT COUNT(*) AS TotalAtendimentos FROM Atendimento"
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
sqlcmd -S localhost,1433 -U sa -P "Sua_Senha_Aqui" -d MobilidadeDb -Q "SELECT COUNT(*) AS TotalIncidentes FROM Incidentes; SELECT COUNT(*) AS TotalAtendimentos FROM Atendimento"
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

---

## Testes de integração contra SQL Server real

### Contexto

Três casos mínimos, todos contra banco real (`UseSqlServer`, não
`InMemory`) — ver seção "Testes de integração" no README para o porquê
de não usar o provider `InMemory` aqui.

### Caso 1: Criar e buscar

```bash
dotnet test --filter CriarEBuscarIncidente
```

```
Resumo do teste: total: 1; falhou: 0; bem-sucedido: 1; ignorado: 0; duração: 4,8s
Construir êxito(s) com 7 aviso(s) em 7,2s
```

Cria um `Incidente` contra o banco real via `RepositorioIncidentesEfCore.Criar`,
busca de volta pelo `Id` gerado pelo SQL Server (`IDENTITY`), e confirma
que a `Descricao` recuperada é igual à que foi gravada.

### Caso 2: Buscar id inexistente retorna null

```bash
dotnet test --filter BuscarPorIdInexistente
```

```
Resumo do teste: total: 1; falhou: 0; bem-sucedido: 1; ignorado: 0; duração: 2,1s
Construir êxito(s) com 1 aviso(s) em 3,7s
```

Confirma que o contrato do `BuscarPorId` (retorna `null`, nunca lança
exceção, quando o id não existe) se mantém contra o banco real — o
mesmo contrato já verificado contra `InMemory` em
`RepositorioComparativoTests`.

### Caso 3: Rollback da transação

Ver seção "Transação com falha provocada" acima — mesmo teste
(`ServicoAberturaIncidenteTests.Executar_ComAtendimentoInvalido_DesfazIncidenteViaRollback`).

### Os três juntos — comando único documentado no README

```bash
dotnet test --filter "RepositorioIncidentesEfCoreTests|ServicoAberturaIncidenteTests"
```

```
Resumo do teste: total: 3; falhou: 0; bem-sucedido: 3; ignorado: 0; duração: 2,3s
Construir êxito(s) com 1 aviso(s) em 3,9s
```

### Limitação conhecida: sem cleanup automático

`CriarEBuscarIncidente_ConfirmaQueÉOMesmoIncidente` grava um `Incidente`
real no banco e não o remove ao final — cada execução do teste deixa um
registro novo e permanente. Depois de rodar a suíte repetidas vezes
durante o desenvolvimento, o banco acumulou 3 incidentes de teste (Ids
8, 10 e 11), que foram removidos manualmente antes desta entrega:

```bash
sqlcmd -S localhost,1433 -U sa -P "Sua_Senha_Aqui" -d MobilidadeDb -Q "DELETE FROM Incidentes WHERE Id IN (8, 10, 11)"
```

O ideal seria o teste fazer seu próprio cleanup (chamando
`repositorio.Remover(incidenteCriado.Id)` ao final, ou envolvendo tudo
numa transação que sempre reverte) para não depender de limpeza manual
e manter os testes independentes de quantas vezes já rodaram. Não
implementado nesta entrega por não ser exigência do checklist da Parte 3,
mas é uma melhoria identificada para a Semana 5+.

## Git e cadência — Semana 5

### Rebase da `semana-05` sobre o `main` atualizado

**O que se quer provar:** a branch estava baseada em `3316214` (`semana-04`,
antes do merge). Após o rebase, passou a estar em cima de `dd03da9`, o merge
do PR #22 no `main`.

**Comandos:**

```
git fetch origin
git rebase origin/main
git log --oneline -5
```

**Saída do `git fetch origin`:**

```
remote: Enumerating objects: 1, done.
remote: Counting objects: 100% (1/1), done.
remote: Total 1 (delta 0), reused 0 (delta 0), pack-reused 0 (from 0)
Unpacking objects: 100% (1/1), 898 bytes | 179.00 KiB/s, done.
From https://github.com/renatodota/painel-mobilidade
d4ba2af..dd03da9 main -> origin/main
```

**Saída do `git rebase origin/main`:**

```
Successfully rebased and updated refs/heads/semana-05.
```

**Saída do `git log --oneline -5` (base já realinhada):**

```
2311634 (HEAD -> semana-05, origin/semana-05) registra decisao sobre NaoTriada na criacao de Incidente (campos pendentes)
dd03da9 (origin/main, origin/HEAD) Merge pull request #22 from renatodota/semana-04
3316214 (origin/semana-04, semana-04) fix: mover EVIDENCIAS.md para a raiz do repositorio
710cf9c docs: registrar limitacao conhecida sobre cleanup dos testes
6df2bb0 feat: testes de integracao criar+buscar e buscar id inexistente
```

### Force push após o rebase

**O que se quer provar:** o rebase reescreveu o histórico local, então o
remoto precisou ser sobrescrito. Usei `--force-with-lease` em vez de
`--force`: ele recusa o push se alguém tiver enviado algo para a branch desde
o último fetch, evitando sobrescrever trabalho de terceiros.

**Comando:**
```
git push --force-with-lease
```

**Confirmação:** no `git log` acima, `origin/semana-05` aparece junto de
`HEAD`, ou seja, o remoto está no mesmo commit que o local.

### Fusão de dois commits com a mesma mensagem

**O que se quer provar:** `3f9e807` e `e34e332` tinham mensagem idêntica — o
primeiro criou o `DECISOES.md` (76 linhas), o segundo corrigia duas linhas
dele (título markdown e nome do valor do enum). Foram mesclados em `2311634`
antes do rebase, sem perda de conteúdo.

**Comandos:**

```
git reset --soft 3316214
git commit -m "registra decisao sobre NaoTriada na criacao de Incidente (campos pendentes)"
git push --force-with-lease
```

**Observação:** os hashes `3f9e807` e `e34e332` não existem mais no remoto
após a reescrita.

## Invariante 3: DataHora não pode ser futura

### O que se quer provar

Regra protegida em `src/Mobilidade.Domain/Modelos/Incidente.cs`, linha 27-28.
Teste em `src/Mobilidade.Tests/IncidenteTests.cs`.
Prova que um incidente não pode ser criado com data e hora no futuro.

### Comando

```bash
dotnet test --filter IncidenteTests
```

### 1. Com a proteção ativa — passa

```
[xUnit.net 00:00:01.28] Starting: Mobilidade.Tests
[xUnit.net 00:00:03.24] Finished: Mobilidade.Tests
Mobilidade.Tests teste net10.0 êxito (4,2s)

Resumo do teste: total: 1; falhou: 0; bem-sucedido: 1; ignorado: 0; duração: 4,2s
```

### 2. Com o `if` e o `throw` comentados — falha

```
[xUnit.net 00:00:01.11] Mobilidade.Tests.IncidenteTests.Criar_ComDataHoraFutura_LancaArgumentException [FAIL]
[xUnit.net 00:00:01.11] Assert.Throws() Failure: No exception was thrown
[xUnit.net 00:00:01.11] Expected: typeof(System.ArgumentException)
[xUnit.net 00:00:01.11] Stack Trace:
[xUnit.net 00:00:01.11] IncidenteTests.cs(16,0): at Mobilidade.Tests.IncidenteTests.Criar_ComDataHoraFutura_LancaArgumentException()

Resumo do teste: total: 2; falhou: 1; bem-sucedido: 1; ignorado: 0; duração: 3,2s
```

Com a proteção comentada, o incidente com data futura foi criado sem erro — o Assert.Throws reclamou de "No exception was thrown". Isso prova que o teste detecta a ausência da regra e não está passando por acaso.

### 3. Com a proteção restaurada — passa de novo

```
[xUnit.net 00:00:00.17] Starting: Mobilidade.Tests
[xUnit.net 00:00:01.36] Finished: Mobilidade.Tests
Mobilidade.Tests teste net10.0 êxito (2,4s)

Resumo do teste: total: 2; falhou: 0; bem-sucedido: 2; ignorado: 0; duração: 2,3s
```

> Saídas cortadas: removidos os avisos de build (NU1903, CS8618, CS8602) e a
> lista de projetos compilados, que se repetem nas três execuções.

### Por quê

A regra vive no construtor, e não num serviço de aplicação, porque assim não existe caminho de criação que produza um incidente com data futura: qualquer new Incidente(...) passa por ela. Se a validação morasse num serviço, todo caso de uso novo teria que lembrar de chamá-lo. O private set completa a proteção — sem ele, a data poderia ser reatribuída depois da criação, desfazendo a validação.

## Semana 6: Integração de Periodo no Incidente

### Contagem de ocorrências de Periodo antes da integração

**O que se quer provar:** o Value Object `Periodo` já existia (criado em
sessão anterior), mas não estava integrado ao `Incidente` — dívida da
Semana 5.

Comando:
```bash
grep -rn "Periodo" --include=*.cs .
```

Saída:
```
./backend/src/Mobilidade.Domain/ValueObjects/Periodo.cs:3:public sealed record Periodo
./backend/src/Mobilidade.Domain/ValueObjects/Periodo.cs:8:    public Periodo(DateTime inicio, DateTime? fim = null)
./backend/src/Mobilidade.Tests/PeriodoTests.cs:8:public class PeriodoTests
./backend/src/Mobilidade.Tests/PeriodoTests.cs:11:    public void Igualdade_DoisPeriodosComMesmasDatas_SaoIguais()
./backend/src/Mobilidade.Tests/PeriodoTests.cs:16:        var a = new Periodo(inicio, fim);
./backend/src/Mobilidade.Tests/PeriodoTests.cs:17:        var b = new Periodo(inicio, fim);
./backend/src/Mobilidade.Tests/PeriodoTests.cs:28:        Assert.Throws<ArgumentException>(() => new Periodo(inicio, fimInvalido));
./backend/src/Mobilidade.Tests/PeriodoTests.cs:34:        var periodo = new Periodo(DateTime.UtcNow);
./backend/src/Mobilidade.Tests/PeriodoTests.cs:45:        var periodo = new Periodo(inicio, fim);
```

**9 ocorrências**, só na própria classe `Periodo.cs` e no teste `PeriodoTests.cs`
— nenhuma em `Incidente.cs`. Confirma que o VO existia isolado, sem uso no
agregado.

### Ciclo de teste — Periodo não inicializado (antes da integração)

Comando:
```bash
dotnet test --filter Abrir_DeveTerPeriodoComInicioIgualDataHora
```

Saída (com o campo `Periodo` apenas adicionado, sem integração real no
construtor/`Abrir()`):

```
[xUnit.net 00:00:01.81]     Mobilidade.Tests.IncidenteTests.Abrir_DeveTerPeriodoComInicioIgualDataHora [FAIL]
[xUnit.net 00:00:01.81]       Assert.NotNull() Failure: Value is null

Resumo do teste: total: 1; falhou: 1; bem-sucedido: 0; ignorado: 0; duração: 2,9s
```

Depois da integração completa (`Periodo` substitui `DataHora`, construído
dentro de `Abrir()`), o mesmo teste passa — prova que a integração real
(não só a existência do campo) foi o que resolveu.

### Estado da tabela "antes" (via sqlcmd, banco revertido pra migration anterior)

Comando:
```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Mobilidade#2026 -d MobilidadeDb -C -Q "SELECT Id, DataHora, Encerrado FROM Incidentes"
```

Saída:
```
Id          DataHora                               Encerrado
----------- -------------------------------------- ---------
          2            2026-09-03 00:00:00.0000000         0
         13            2026-09-08 21:51:04.5823336         0
         19            2026-09-09 13:49:35.0304101         0
         21            2026-09-09 13:59:25.5020524         0
         23            2026-09-09 14:12:58.3579565         0
         25            2026-09-09 14:18:56.6264448         0
         35            2026-09-09 15:27:03.7945376         0
         37            2026-09-09 18:11:42.9615329         0
         40            2026-09-10 11:03:29.9709755         0
         41            2026-09-10 11:03:52.7277436         0
         43            2026-09-10 13:06:54.1483226         0
         47            2026-09-10 13:47:27.4719032         0
         48            2026-09-10 10:00:00.0000000         0
         49            2026-09-10 10:00:00.0000000         0
         50            2026-09-10 10:00:00.0000000         0
         51            2026-09-10 10:00:00.0000000         0
         52            2026-09-10 10:00:00.0000000         0
         53            2026-09-10 13:57:49.7116041         0
         54            2026-09-10 15:48:09.5460470         0
         55            2026-09-10 15:48:21.9884071         0
         56            2026-09-10 16:23:38.5732274         0
         57            2026-09-10 17:10:44.2750443         0
         58            2026-09-10 18:57:11.0304526         0

(23 rows affected)
```

### Migration aplicada — SQL executado (primeira aplicação)

```
Applying migration '20260914200312_IntegraPeriodoNoIncidente'.
ALTER TABLE [Incidentes] ADD [Periodo_Fim] datetime2 NULL;
UPDATE Incidentes SET Periodo_Fim = GETUTCDATE() WHERE Encerrado = 1
[...DROP CONSTRAINT + DROP COLUMN Encerrado...]
ALTER TABLE [Incidentes] DROP COLUMN [Encerrado];
EXEC sp_rename N'[Incidentes].[DataHora]', N'Periodo_Inicio', 'COLUMN';
Done.
```

### Migration revertida — SQL executado (rollback, para capturar "antes" real)

```
Reverting migration '20260914200312_IntegraPeriodoNoIncidente'.
ALTER TABLE [Incidentes] DROP COLUMN [Periodo_Fim];
EXEC sp_rename N'[Incidentes].[Periodo_Inicio]', N'DataHora', 'COLUMN';
ALTER TABLE [Incidentes] ADD [Encerrado] bit NOT NULL DEFAULT CAST(0 AS bit);
Done.
```

### Criação de incidente de teste com Encerrado = 1

**O que se quer provar:** o `UPDATE` da migration só populava `Periodo_Fim`
para incidentes com `Encerrado = 1` — mas o banco não tinha nenhum
incidente nesse estado até este momento, então a migration nunca havia
sido testada contra um caso real. Inserido manualmente um incidente com
`Encerrado = 1` antes de reaplicar a migration.

Comando:
```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Mobilidade#2026 -d MobilidadeDb -C -Q "INSERT INTO Incidentes (DataHora, Tipo, Severidade, BairroId, Encerrado) VALUES ('2026-09-01T10:00:00', 0, 2, 1, 1)"
```

Saída:
```
(1 rows affected)
```

Confirmação do dado inserido:
```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Mobilidade#2026 -d MobilidadeDb -C -Q "SELECT Id, DataHora, Encerrado FROM Incidentes WHERE Encerrado = 1"
```

```
Id          DataHora                               Encerrado
----------- -------------------------------------- ---------
         59            2026-09-01 10:00:00.0000000         1

(1 rows affected)
```

### Reaplicando a migration (Up de novo, com o caso de teste presente)

Comando:
```bash
dotnet ef database update --project backend/src/Mobilidade.Infrastructure --startup-project backend/src/Mobilidade.Api
```

Trecho relevante da saída:
```
Applying migration '20260914200312_IntegraPeriodoNoIncidente'.
ALTER TABLE [Incidentes] ADD [Periodo_Fim] datetime2 NULL;
UPDATE Incidentes SET Periodo_Fim = GETUTCDATE() WHERE Encerrado = 1
ALTER TABLE [Incidentes] DROP COLUMN [Encerrado];
EXEC sp_rename N'[Incidentes].[DataHora]', N'Periodo_Inicio', 'COLUMN';
Done.
```

### Estado da tabela "depois" — prova que o UPDATE afetou só o incidente certo

Comando:
```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Mobilidade#2026 -d MobilidadeDb -C -Q "SELECT Id, Periodo_Inicio, Periodo_Fim FROM Incidentes"
```

Saída (trecho relevante):
```
Id          Periodo_Inicio                         Periodo_Fim
----------- -------------------------------------- --------------------------------------
          2            2026-09-03 00:00:00.0000000  NULL
         13            2026-09-08 21:51:04.5823336  NULL
         ...
         58            2026-09-10 18:57:11.0304526  NULL
         59            2026-09-01 10:00:00.0000000  2026-09-15 13:35:34.9033333

(24 rows affected)
```

**Confirmado:** somente o Id 59 (o único com `Encerrado = 1` antes da
migration) recebeu `Periodo_Fim` preenchido. Os outros 23 incidentes,
que já estavam abertos, permaneceram com `Periodo_Fim = NULL` — provando
que o `UPDATE ... WHERE Encerrado = 1` afetou exatamente e apenas o caso
esperado, não todo mundo.

### Por quê

- **`Periodo` substitui `DataHora`, `Encerrado` vira `Periodo.EstaEmAberto`:**
  manter os dois (`Encerrado` bool + informação de fim) duplicava estado
  que podia dessincronizar (alguém marcar `Encerrado = true` sem atualizar
  a data, ou vice-versa). `Periodo.Fim == null` já expressa "ainda aberto"
  sem precisar de um campo redundante.
- **`Periodo` é imutável:** `Encerrar()` não muta o objeto `Periodo`
  existente — cria um novo (`new Periodo(Periodo.Inicio, DateTime.UtcNow)`)
  e substitui a referência. O `Periodo` antigo nunca é alterado.
- **Validação de "não pode ser futuro" fica em `Incidente.Abrir()`, não em
  `Periodo`:** essa é uma regra do domínio de incidente, não do conceito
  genérico de período — um "período de agendamento", por exemplo, poderia
  legitimamente começar no futuro.
- **`RegistrarAtendimento` não valida data futura:** diferente de
  `Incidente`, um `Atendimento` pode representar agendamento futuro (ex:
  equipe programada para atuar no dia seguinte). Validar "não pode ser
  futuro" aqui bloquearia um cenário legítimo do negócio.
- **`OwnsOne` em vez de `Property()` no EF Core:** `Property()` mapeia
  valores escalares simples; `Periodo` é um Value Object com dois campos
  internos (`Inicio`, `Fim`). Confirmado experimentalmente — `Property()`
  falha em runtime com `"could not be mapped because the database
  provider does not support this type"` ao rodar `dotnet ef migrations
  add`.
- **Construtor vazio (`private Incidente() { }`) adicionado:** o EF Core
  não consegue passar um owned type (`Periodo`) como argumento de
  construtor ao materializar a entidade a partir do banco — só sabe
  preencher propriedades depois de instanciada. O construtor vazio existe
  só para essa materialização; nunca é chamado pelo código de domínio
  (que sempre passa por `Abrir()`, com suas validações).
- **`Periodo_Fim = GETUTCDATE()` para incidentes já encerrados:** o
  sistema antigo guardava só um `bool Encerrado`, sem registrar a data
  real do encerramento. Não há como recuperar essa informação — qualquer
  valor escolhido é uma aproximação. Consideradas três opções: (1) data
  do momento da migration, (2) data do último atendimento registrado, (3)
  deixar `null` (reabre os incidentes). A opção 2 foi rejeitada por
  assumir uma relação causal que pode não existir, e por quebrar em
  incidentes sem nenhum atendimento. A opção 3 foi rejeitada por mudar
  silenciosamente o estado de negócio de incidentes já fechados. Escolhida
  a opção 1: não finge saber o que não se sabe, e representa um marco real
  e rastreável (o momento em que o dado foi migrado).
- **`UPDATE` executado antes do `DropColumn Encerrado`:** a coluna
  `Encerrado` precisa existir no momento do `WHERE Encerrado = 1` — se
  dropada antes, o filtro perde a fonte de dado necessária para saber
  quais incidentes migrar.