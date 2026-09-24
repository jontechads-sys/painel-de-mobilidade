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
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P SuaSenhaAqui -d MobilidadeDb -C -Q "SELECT Id, DataHora, Encerrado FROM Incidentes"
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
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P SuaSenhaAqui -d MobilidadeDb -C -Q "INSERT INTO Incidentes (DataHora, Tipo, Severidade, BairroId, Encerrado) VALUES ('2026-09-01T10:00:00', 0, 2, 1, 1)"
```

Saída:
```
(1 rows affected)
```

Confirmação do dado inserido:
```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P SuaSenhaAqui -d MobilidadeDb -C -Q "SELECT Id, DataHora, Encerrado FROM Incidentes WHERE Encerrado = 1"
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
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P SuaSenhaAqui -d MobilidadeDb -C -Q "SELECT Id, Periodo_Inicio, Periodo_Fim FROM Incidentes"
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
---

## Semana 6: Handler de evento — auditoria de incidente grave

### O que se quer provar

Quando um incidente de severidade `Alta` é aberto, um handler registra
uma linha na tabela `Auditoria`. Quando a severidade é `Baixa` ou `Media`,
nada acontece. A decisão é do handler
(`RegistrarAuditoriaAoAbrirIncidenteGrave`), não do agregado — o
`Incidente` emite `IncidenteAberto` para toda severidade e não sabe que
alguém filtra.

O despacho acontece em `AbrirIncidente.Executar`, depois de
`_repositorio.Criar(incidente)`. Se a persistência falhar, nenhum evento
é despachado.

Testes em `src/Mobilidade.Tests/AuditoriaTest.cs`.

### Comando

```bash
dotnet test --filter AuditoriaTests
```

### 1. Com o despacho comentado em `AbrirIncidente.cs` — falha

Trecho comentado:

```csharp
// foreach (var evento in incidenteNovo.Eventos)
//     _despachador.Publicar(evento);
```

Saída:
```
[xUnit.net 00:00:00.67] Mobilidade.Tests.AuditoriaTests.Executar_ComSeveridadeAlta_GeraUmRegistroDeAuditoria [FAIL]
[xUnit.net 00:00:00.67] Assert.Single() Failure: The collection was empty
[xUnit.net 00:00:00.67] Stack Trace:
[xUnit.net 00:00:00.67] AuditoriaTest.cs(28,0): at Mobilidade.Tests.AuditoriaTests.Executar_ComSeveridadeAlta_GeraUmRegistroDeAuditoria()

Resumo do teste: total: 2; falhou: 1; bem-sucedido: 1; ignorado: 0; duração: 1,5s
Construir falhou com1 erros e 3 avisos em 3,2s
```

Sem o despacho, o evento `IncidenteAberto` fica acumulado na lista interna
do agregado e é descartado por `LimparEventos()` — o handler nunca roda e
nenhum registro de auditoria é criado. O teste de severidade `Alta` falha
(`Assert.Single()` numa coleção vazia); o de severidade `Baixa` continua
passando, o que confirma que ele mede a regra do handler e não a ausência
de despacho.

### 2. Com o despacho restaurado — passa

```
[xUnit.net 00:00:00.14] Starting: Mobilidade.Tests
[xUnit.net 00:00:00.19] Finished: Mobilidade.Tests
Mobilidade.Tests teste net10.0 êxito (1,0s)

Resumo do teste: total: 2; falhou: 0; bem-sucedido: 2; ignorado: 0; duração: 1,0s
Construir êxito(s) com 1 aviso(s) em 2,6s
```

> Saídas cortadas: removidos os avisos de build (NU1903, CS8602) e a lista
> de projetos compilados, que se repetem nas duas execuções.

### Bug encontrado no caminho: `CriarIncidenteRequest` expunha o Value Object

Ao tentar abrir um incidente pela API para capturar o estado real da tabela,
o endpoint retornou 500:

```
System.NotSupportedException: Deserialization of types without a parameterless
constructor, a singular parameterized constructor, or a parameterized constructor
annotated with 'JsonConstructorAttribute' is not supported.
Type 'Mobilidade.Domain.ValueObjects.Severidade'. Path: $.severidade
```
`CriarIncidenteRequest` recebia `Severidade` (o Value Object de domínio)
diretamente no contrato HTTP. Como o `Severidade` tem construtor privado —
proposital, para que só se possa obter uma instância via `DeInt` ou pelos
valores estáticos — o `System.Text.Json` não consegue materializá-lo. O
endpoint estava quebrado desde que `Severidade` deixou de ser enum.

Corrigido fazendo o DTO receber `int` e converter na fronteira:

```csharp
// CriarIncidenteRequest.cs
public sealed record CriarIncidenteRequest(
    DateTime DataHora,
    TipoIncidente Tipo,
    int Severidade,
    ...
);

// IncidentesController.Criar
var severidade = Severidade.DeInt(request.Severidade);
```

### Estado da tabela `Auditoria` ANTES de abrir um incidente Alta

```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SuaSenhaAqui" -d MobilidadeDb -C -Q "SELECT Id, IncidenteId, Severidade, Data FROM Auditoria"
```
Saída: 

```
Id IncidenteId Severidade Data

(0 rows affected)
```

### Abrindo um incidente de severidade Alta pela API

Caminho real, com o `Despachador` e o handler resolvidos pelo container de DI
— não pelos fakes dos testes.

```bash
curl -X POST http://localhost:5041/api/Incidentes -H "Content-Type: application/json" -d "{\"dataHora\":\"2026-09-17T10:00:00\",\"tipo\":0,\"severidade\":2,\"bairroId\":1,\"descricao\":\"teste auditoria\",\"orgaoResponsavel\":\"Defesa Civil\",\"situacao\":\"Em analise\"}"
```

Saída:
```
{"eventos":[],"periodo":{"inicio":"2026-09-17T10:00:00","fim":null,"estaEmAberto":true,"duracao":null},"id":63,"tipo":0,"severidade":{"valor":2,"nome":"Alta"},"bairroId":1,"descricao":"teste auditoria","bairro":null,"atendimentos":[{"id":43,"incidenteId":63,"orgaoResponsavel":"Defesa Civil","dataHora":"2026-09-17T10:00:00","situacao":"Em analise","incidente":null}]}
```

O `"eventos":[]` na resposta é esperado: o caso de uso chama `LimparEventos()`
após o despacho, e o objeto retornado vem de uma nova consulta ao banco.

### Estado da tabela `Auditoria` DEPOIS
```
Id IncidenteId Severidade Data

      3          63           2            2026-09-17 10:00:00.0000000

(1 rows affected)
```


**Confirmado:** o `IncidenteId` gravado (63) é o mesmo Id que o SQL Server
gerou para o incidente, e a severidade é 2 (`Alta`). O handler rodou pelo
caminho real da aplicação — controller → caso de uso → repositório →
despachador → handler → repositório de auditoria.

### Por quê

- **A regra de severidade vive no handler, não no agregado:** o `Incidente`
  emite `IncidenteAberto` em toda abertura, sem saber quem escuta. Se o
  filtro `Severidade != Alta` morasse no agregado, ele passaria a conhecer
  a razão de existir da auditoria — exatamente o acoplamento que o evento
  de domínio existe para evitar. Amanhã, um handler de notificação com
  outro critério de severidade entra sem tocar no `Incidente`.
- **O despacho acontece depois de `Criar()`, não antes:** o handler grava
  na `Auditoria` uma linha com FK para `Incidentes`. Se o evento fosse
  despachado antes da persistência, o `IncidenteId` não existiria no banco
  e o INSERT violaria a FK. Além disso, uma falha na gravação do incidente
  deixaria auditoria de algo que não aconteceu.
- **`RegistrarIncidenteAberto()` é chamado após a persistência:** diferente
  de `Encerrar()` e `RegistrarAtendimento()`, que emitem o evento dentro da
  própria transição, o `IncidenteAberto` só pode ser construído depois que
  o banco gera o `Id` (IDENTITY). Os demais dados do evento (severidade,
  data) já existem desde `Abrir()`. O custo dessa escolha é que um caso de
  uso novo precisa lembrar de chamar o método — aceito por ora, registrado
  na decisão #14.
- **`LimparEventos()` depois do despacho:** sem isso, um segundo `Salvar()`
  na mesma instância redespacharia os mesmos eventos. A lista é um buffer
  de uma transação, não um histórico.
- **Os testes usam `DespachadorComManipuladores`, não o `Despachador` real:**
  o fake entrega `IncidenteAberto` direto ao handler, sem `ServiceProvider`.
  Testa a regra do handler e o ponto de despacho no caso de uso; o registro
  no DI é o que a evidência via `sqlcmd` acima verifica.
- **O endpoint `/api/incidentes/importar` não despacha eventos:** ele chama
  `repositorio.Criar` direto, sem passar pelo caso de uso. Incidentes
  importados por CSV não geram auditoria, mesmo com severidade `Alta`.
  Deliberado — a importação é carga de dados histórica, não abertura de
  incidente pelo domínio.

  ## Semana 6: Validação em duas camadas

### O que se quer provar

Erro de formato (campo ausente, fora de range) é resolvido na borda da API,
que responde 400 sem que o domínio tome conhecimento da requisição. Erro de
regra de negócio (severidade `NaoTriada` na criação, incidente já encerrado,
data futura) só o domínio sabe julgar: o agregado lança `DomainException`
com mensagem na linguagem do negócio, e o `DomainExceptionFilter` traduz
para 422.

As três requisições abaixo foram feitas contra a API rodando em
`http://localhost:5041`, com o banco real.

### 1. Falha na borda — 400, o domínio nunca é chamado

Requisição, sem `bairroId` e sem `situacao`:

```bash
curl -i -X POST http://localhost:5041/api/Incidentes -H "Content-Type: application/json" -d "{\"dataHora\":\"2026-09-18T10:00:00\",\"tipo\":0,\"severidade\":2,\"descricao\":\"teste borda\",\"orgaoResponsavel\":\"Defesa Civil\"}"
```

Resposta:
```
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json; charset=utf-8

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"BairroId":["BairroId é obrigatório."],"Situacao":["Situacao é obrigatória."]},"traceId":"00-de280ecd0df1928cc5cb5a91208df886-a1426556fa799967-00"}
```


O `[ApiController]` valida o DTO antes de entrar no método do controller —
`IncidentesController.Criar` nunca executa, `Incidente.Abrir` nunca é
chamado. Os dois campos ausentes aparecem nomeados, com as mensagens
declaradas nas anotações.

### 2. Passa a borda, falha no domínio — 422

Requisição com `severidade: 3` (`NaoTriada`), que o `[Range(0, 3)]` aceita
de propósito (decisão #21):

```bash
curl -i -X POST http://localhost:5041/api/Incidentes -H "Content-Type: application/json" -d "{\"dataHora\":\"2026-09-18T10:00:00\",\"tipo\":0,\"severidade\":3,\"bairroId\":1,\"descricao\":\"teste dominio\",\"orgaoResponsavel\":\"Defesa Civil\",\"situacao\":\"Em analise\"}"
```

Resposta:
```
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json; charset=utf-8

{"title":"Regra de negócio violada","status":422,"detail":"Incidente não pode ser aberto sem severidade definida"}
```


Todos os campos estão presentes e dentro dos ranges, então a borda deixa
passar. O construtor privado de `Incidente` rejeita `NaoTriada` (decisão #1)
e lança `DomainException`; o filtro traduz para 422 com a mensagem do
domínio intacta.

### 3. Passa as duas camadas — 201

```bash
curl -i -X POST http://localhost:5041/api/Incidentes -H "Content-Type: application/json" -d "{\"dataHora\":\"2026-09-18T10:00:00\",\"tipo\":0,\"severidade\":2,\"bairroId\":1,\"descricao\":\"teste ok\",\"orgaoResponsavel\":\"Defesa Civil\",\"situacao\":\"Em analise\"}"
```

Resposta:
```
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Location: http://localhost:5041/api/Incidentes/64

{"eventos":[],"periodo":{"inicio":"2026-09-18T10:00:00","fim":null,"estaEmAberto":true,"duracao":null},"id":64,"tipo":0,"severidade":{"valor":2,"nome":"Alta"},"bairroId":1,"descricao":"teste ok","bairro":null,"atendimentos":[{"id":44,"incidenteId":64,"orgaoResponsavel":"Defesa Civil","dataHora":"2026-09-18T10:00:00","situacao":"Em analise","incidente":null}]}
```


### Tentativa rejeitada: anotações com `[property:]`

**O que se quer provar:** que a escolha da decisão #22 não é preferência,
e sim a única forma que o ASP.NET Core aceita.

Primeiro esboço do teste de borda usava `Validator.TryValidateObject`, que
devolvia coleção vazia de erros — `Validator` só enxerga atributos em
propriedades, e num record posicional as anotações ficam nos parâmetros do
construtor:
```
Mobilidade.Tests.ValidacaoBordaTests.CriarIncidenteRequest_SemCamposObrigatorios_FalhaNaValidacao [FAIL]
Assert.Contains() Failure: Filter not matched in collection
Collection: []
```


Anotações trocadas para `[property: Required(...)]`. O teste passou, mas a
API quebrou — toda requisição ao endpoint passou a devolver 500:
```
HTTP/1.1 500 Internal Server Error

{"title":"System.InvalidOperationException","status":500,"detail":"Record type 'Mobilidade.Api.Dtos.CriarIncidenteRequest' has validation metadata defined on property 'Situacao' that will be ignored. 'Situacao' is a parameter in the record primary constructor and validation metadata must be associated with the constructor parameter."}
```

Lançado por `ModelMetadata.ThrowIfRecordTypeHasValidationOnProperties()`
durante o model binding, antes mesmo de qualquer validação rodar.

As duas exigências são incompatíveis. As anotações voltaram para o
parâmetro (comportamento HTTP correto, confirmado pelo 400 da seção 1) e o
teste passou a ler os atributos por reflection sobre os parâmetros do
construtor.

### Testes

```bash
dotnet test --filter ValidacaoBordaTests
```

**Saída:**

```Resumo do teste: total: 7; falhou: 0; bem-sucedido: 7; ignorado: 0; duração: 2,3s```

```bash
dotnet test --filter DomainExceptionFilterTests
```

**Saída:**
```
Resumo do teste: total: 3; falhou: 0; bem-sucedido: 3; ignorado: 0; duração: 1,5s
Construir êxito(s) com 4 aviso(s) em 5,6s
```


```bash
dotnet test --filter IncidenteTests
```
**Saída:**
```
Resumo do teste: total: 9; falhou: 0; bem-sucedido: 9; ignorado: 0; duração: 3,9s
```

### Por quê

- **`DomainException` em vez de `InvalidOperationException`:** um filtro que
  traduzisse `InvalidOperationException` para 422 transformaria qualquer bug
  de programação (um `First()` em coleção vazia, por exemplo) em "regra de
  negócio violada". Com tipo próprio, só o que o domínio declarou como regra
  vira 422; o resto continua 500, que é o que de fato é (decisão #19).
- **Filtro global em vez de `try/catch` por método:** os `catch
  (InvalidOperationException)` que existiam no controller viraram código
  morto no instante em que o domínio passou a lançar `DomainException` — e
  o resultado era 500 silencioso. O filtro cobre qualquer endpoint sem
  depender de alguém lembrar (decisão #20).
- **Mudança de contrato:** encerrar incidente já encerrado respondia 409 e
  agora responde 422. 409 tratava a violação como conflito de concorrência,
  o que não descreve o caso — o incidente não está sendo disputado, ele está
  num estado que não permite a operação.
- **A borda aceita `severidade: 3`:** é o que torna as duas camadas
  visivelmente distintas na seção 2. Barrar na borda duplicaria em dois
  lugares uma regra que pertence ao domínio (decisão #21).
- **Limitação conhecida:** o 400 não tem teste automatizado que o produza —
  é provado apenas pela requisição HTTP da seção 1. Os testes verificam que
  as anotações estão declaradas, não que o pipeline as executa. Fechar essa
  lacuna exige `WebApplicationFactory`, registrado como pendência na
  decisão #23.

  ## Semana 6: Incidentes legados sem atendimento

### O que se quer provar

A invariante 1 (todo incidente nasce com pelo menos um atendimento) é
garantida estruturalmente para incidentes novos desde a decisão #4, mas o
banco tinha registros anteriores a essa garantia — incidentes sem nenhum
atendimento, violando a invariante em repouso. Dívida reconhecida na
sabatina da Semana 5, paga aqui.

### Estado ANTES — sete órfãos

```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SuaSenhaAqui" -d MobilidadeDb -C -Q "SELECT i.Id, i.Periodo_Inicio, i.Severidade, i.BairroId FROM Incidentes i WHERE NOT EXISTS (SELECT 1 FROM Atendimento a WHERE a.IncidenteId = i.Id)"
```

**Saída:**
```
Id Periodo_Inicio Severidade BairroId

      2            2026-09-03 00:00:00.0000000           3           1
     13            2026-09-08 21:51:04.5823336           2           1
     19            2026-09-09 13:49:35.0304101           2           1
     21            2026-09-09 13:59:25.5020524           2           1
     23            2026-09-09 14:12:58.3579565           2           1
     25            2026-09-09 14:18:56.6264448           2           1
     59            2026-09-01 10:00:00.0000000           2           1

(7 rows affected)
```

O Id 2 é o registro que a migration `SeveridadeObrigatoria` converteu de
`NULL` para `NaoTriada` (severidade 3). Os Ids 13 a 25 são de 08 e 09 de
setembro, antes de o construtor de `Incidente` ser fechado.

### Remoção do artefato de teste (Id 59)

O Id 59 não é dado legado: foi inserido manualmente por `sqlcmd` durante a
evidência da migration do `Periodo`, para testar o `UPDATE ... WHERE
Encerrado = 1` contra um caso real. Removido antes da migração.

```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SuaSenhaAqui" -d MobilidadeDb -C -Q "DELETE FROM Incidentes WHERE Id = 59"
```
**Saída:**

```
(1 rows affected)
```

Restaram seis órfãos, todos dado de negócio anterior à invariante.

### Migration criada e aplicada

```bash
dotnet ef migrations add AtendimentoSinteticoParaIncidentesLegados --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api
dotnet ef database update --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api
```

SQL executado:
```
Applying migration '20260918170657_AtendimentoSinteticoParaIncidentesLegados'.

INSERT INTO Atendimento (IncidenteId, OrgaoResponsavel, DataHora, Situacao)
SELECT i.Id,
'Importacao (dado legado)',
i.Periodo_Inicio,
'Sem registro de atendimento'
FROM Incidentes i
WHERE NOT EXISTS (SELECT 1 FROM Atendimento a WHERE a.IncidenteId = i.Id)

Done.
```


### Estado DEPOIS — zero órfãos

```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SuaSenhaAqui" -d MobilidadeDb -C -Q "SELECT COUNT(*) AS Orfaos FROM Incidentes i WHERE NOT EXISTS (SELECT 1 FROM Atendimento a WHERE a.IncidenteId = i.Id)"
```
**Saída:**
```
Orfaos
      0

(1 rows affected)
```

### Os seis atendimentos sintéticos criados

```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SuaSenhaAqui" -d MobilidadeDb -C -Q "SELECT Id, IncidenteId, OrgaoResponsavel, DataHora, Situacao FROM Atendimento WHERE OrgaoResponsavel = 'Importacao (dado legado)'"
```
**Saída:**
```
Id IncidenteId OrgaoResponsavel DataHora Situacao

45 2 Importacao (dado legado) 2026-09-03 00:00:00.0000000 Sem registro de atendimento
46 13 Importacao (dado legado) 2026-09-08 21:51:04.5823336 Sem registro de atendimento
47 19 Importacao (dado legado) 2026-09-09 13:49:35.0304101 Sem registro de atendimento
48 21 Importacao (dado legado) 2026-09-09 13:59:25.5020524 Sem registro de atendimento
49 23 Importacao (dado legado) 2026-09-09 14:12:58.3579565 Sem registro de atendimento
50 25 Importacao (dado legado) 2026-09-09 14:18:56.6264448 Sem registro de atendimento

(6 rows affected)
```
Cada `DataHora` é o `Periodo_Inicio` do incidente correspondente — nenhum
instante foi inventado.

### Por quê

- **Migração em vez de dívida documentada:** com seis registros e um
  `INSERT ... SELECT` resolvendo, aceitar a dívida seria conveniência
  vestida de decisão técnica. A invariante 1 é a base do agregado; dado que
  a viola em repouso quebra qualquer consulta futura que a assuma
  (decisão #26).
- **`Periodo_Inicio` como data:** o atendimento sintético não aconteceu em
  instante nenhum. Usar a data de abertura do incidente evita inventar um
  momento — diferente da migration do `Periodo`, onde não havia data honesta
  disponível e `GETUTCDATE()` foi a escolha.
- **`OrgaoResponsavel` explícito:** deixa o registro identificável como
  sintético e permite que o `Down` reverta exatamente esses seis, sem tocar
  em atendimento real.
- **Custo assumido:** esses seis ocupam a mesma tabela dos atendimentos
  reais e entram em qualquer contagem que não os filtre.

  ## Git e cadência — Semana 6

### Rebase sobre o `main` atualizado

**O que se quer provar:** que a branch `semana-06` está baseada no `main`
atual, e não numa versão defasada dele.

```bash
git fetch origin
git rebase origin/main
```
**Saída:**
```Current branch semana-06 is up to date``` 

Não houve commit para reaplicar: a branch foi criada a partir do `main` já
atualizado e nenhum commit entrou no `main` desde então. A saída confirma o
alinhamento — é o resultado correto, não um comando que falhou.

### Rebase interativo tentado e abortado

**O que se quer provar:** os sete commits da Parte 1 ficaram com assunto e
corpo na mesma linha, porque as quebras se perderam ao colar as mensagens no
terminal. A tentativa de corrigir com `rebase -i` foi abortada ao perceber
que estava embaralhando os commits.

```bash
git rebase -i HEAD~7
```

O `--amend` do primeiro commit aplicou a mensagem errada (a do último commit
da lista), e o commit original ficou vazio:
```
The previous cherry-pick is now empty, possibly due to conflict resolution.
If you wish to commit it anyway, use:
git commit --allow-empty
Otherwise, please use 'git rebase --skip'
...
Could not apply a025111... # tipa a colecao de eventos do agregado
```

Continuar dali significaria reescrever sete commits num estado já
inconsistente, com risco de perder conteúdo. Abortado:

```bash
git rebase --abort
git stash pop
```

```bash
git log --oneline -7
```
**Saída:**
```
cc3e230 (HEAD -> semana-06) registra o ciclo do handler e as decisoes 16, 17 e 18 ...
c5a4ede prova que a auditoria depende do despacho, nao do acaso ...
72ed522 desbloqueia a criacao de incidente pela API ...
35762ae despacha os eventos depois que a transacao foi persistida ...
2c12676 entrega eventos aos interessados sem o agregado saber quem sao ...
eb9374f da a Auditoria um caminho de escrita a partir do codigo ...
a025111 tipa a colecao de eventos do agregado ...
```


Os sete commits voltaram intactos, com os hashes originais. O conteúdo de
cada mensagem está correto; o que ficou por corrigir é a formatação (assunto
e corpo na mesma linha), que não altera o histórico nem o que cada commit
resolve.

### Por quê

- **`--abort` em vez de seguir:** um rebase interativo que já aplicou a
  mensagem errada num commit e esvaziou outro não se conserta seguindo em
  frente. Abortar devolve o estado exato de antes, e o custo é só a
  formatação continuar imperfeita.
- **`git stash` antes do rebase:** o `appsettings.json` tem a senha real do
  banco local e fica deliberadamente fora dos commits (decisão #12). O
  rebase recusa rodar com mudanças não commitadas, então o arquivo é
  guardado e recuperado depois — sem nunca entrar no histórico.
- **Formatação das mensagens não corrigida:** decisão de prazo. Corrigir os
  sete exigiria sete `reword` sequenciais no dia da entrega, e o conteúdo
  das mensagens — que é o que a issue pede, o porquê de cada commit — já
  está lá.

  ## Semana 6: CQRS — SQL da query com projeção vs. carregar o agregado

### O que se quer provar

Uma query de leitura que projeta direto para DTO gera SQL visivelmente menor
do que carregar o agregado inteiro pelo repositório. A comparação foi feita
com `ToQueryString()`, que produz o SQL sem executá-lo.

### Comando

Endpoint de diagnostico em `Program.cs`, usado só para capturar os dois SQLs:

```csharp
app.MapGet("/api/debug/sql", (IncidentesDbContext ctx) =>
{
    var sqlProjecao = ctx.Incidentes
        .AsNoTracking()
        .Select(i => new { i.Id, i.Periodo.Inicio, Severidade = i.Severidade.Nome,
                           Bairro = i.Bairro!.Nome, EstaEmAberto = i.Periodo.Fim == null })
        .ToQueryString();

    var sqlAgregado = ctx.Incidentes
        .Include(i => i.Bairro)
        .Include(i => i.Atendimentos)
        .ToQueryString();

    return Results.Text($"...{sqlProjecao}...{sqlAgregado}");
});
```

```bash
curl http://localhost:5041/api/debug/sql
```
O endpoint foi mantido no código, não removido após capturar a saída: ele não
toca no banco (`ToQueryString()` só gera o texto do SQL) e serve para conferir
o SQL de qualquer projeção futura sem precisar recriá-lo. Fica sob `/api/debug/`
para deixar claro que não faz parte do contrato da aplicação.

### SQL gerado pela query com projeção (`ListarIncidentesHandler`)

```sql
SELECT [i].[Id], [i].[Periodo_Inicio], [i].[Severidade], [b].[Nome], CASE
    WHEN [i].[Periodo_Fim] IS NULL THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [Incidentes] AS [i]
INNER JOIN [Bairros] AS [b] ON [i].[BairroId] = [b].[Id]
```

### SQL gerado carregando a entidade inteira (caminho do repositório)

```sql
SELECT [i].[Id], [i].[BairroId], [i].[Descricao], [i].[Severidade], [i].[Tipo], [i].[Periodo_Fim], [i].[Periodo_Inicio], [b].[Id], [b].[Nome], [a].[Id], [a].[DataHora], [a].[IncidenteId], [a].[OrgaoResponsavel], [a].[Situacao]
FROM [Incidentes] AS [i]
INNER JOIN [Bairros] AS [b] ON [i].[BairroId] = [b].[Id]
LEFT JOIN [Atendimento] AS [a] ON [i].[Id] = [a].[IncidenteId]
ORDER BY [i].[Id], [b].[Id]
```

### A diferença

A projeção traz **5 colunas de 2 tabelas**; o agregado traz **14 colunas de 3
tabelas**, incluindo `Descricao`, que é `nvarchar(max)` e pode ser grande.

O agregado precisa do `LEFT JOIN Atendimento` porque a invariante 1 diz que
todo incidente tem atendimentos, e o EF tem que materializar a coleção. Isso
multiplica linhas: um incidente com cinco atendimentos vira cinco linhas que o
EF reagrupa em memória. A projeção não conhece `Atendimento` e devolve uma
linha por incidente.

O `ORDER BY [i].[Id], [b].[Id]` existe só no agregado — o EF precisa das linhas
agrupadas para remontar cada incidente com sua coleção. A projeção não ordena
nada, porque não tem o que remontar.

### Observação sobre `Severidade`

O SQL da projeção traz `[i].[Severidade]` (o `int` da coluna), não o nome. O
`.Nome` do DTO é resolvido em memória, pelo `ValueConverter` da decisão #5, que
reconstrói o Value Object a partir do inteiro. Ou seja, a projeção não conseguiu
empurrar essa conversão para o banco — mas também não precisou carregar nada a
mais por causa disso: a coluna viria de qualquer forma.

### As três queries respondendo

Todas contra a API rodando em `http://localhost:5041`, com o banco real. O
controller não usa `IIncidenteRepository` em nenhuma delas.

**`ListarIncidentes` — DTO de resumo, 5 campos:**

```bash
curl -i http://localhost:5041/api/Incidentes
```

```
HTTP/1.1 200 OK

[{"id":2,"inicio":"2026-09-03T00:00:00","severidade":"NaoTriada","bairro":"Centro","estaEmAberto":true},
 {"id":13,"inicio":"2026-09-08T21:51:04.5823336","severidade":"Alta","bairro":"Centro","estaEmAberto":true},
 ... 28 registros no total ...
 {"id":64,"inicio":"2026-09-18T10:00:00","severidade":"Alta","bairro":"Centro","estaEmAberto":true}]
```

A `severidade` sai como nome e o `bairro` como nome — nenhum objeto aninhado,
nenhuma coleção de atendimentos, nenhum campo que a listagem não use.

**`BuscarIncidentePorId` — DTO de detalhe:**

```bash
curl -i http://localhost:5041/api/Incidentes/64
```

```
HTTP/1.1 200 OK

{"id":64,"inicio":"2026-09-18T10:00:00","fim":null,"severidade":"Alta","bairro":"Centro","tipo":0,"descricao":"teste ok","quantidadeAtendimentos":1}
```

`quantidadeAtendimentos` é um número, não a lista. A projeção manda o banco
contar; nenhum objeto `Atendimento` é materializado.

**`IncidentesPorBairro` — filtro de período usando o `Periodo`:**

Sem estreitar o período (setembro inteiro), devolve os 28:

```bash
curl -i "http://localhost:5041/api/Incidentes/bairro/1?inicio=2026-09-01T00:00:00&fim=2026-09-30T23:59:59"
```

```
[ ... 28 registros ... ]
```

Estreitando para um único dia, devolve 4:

```bash
curl -i "http://localhost:5041/api/Incidentes/bairro/1?inicio=2026-09-17T00:00:00&fim=2026-09-17T23:59:59"
```

```
HTTP/1.1 200 OK

[{"id":60,"inicio":"2026-09-17T10:00:00","severidade":"Alta","bairro":"Centro","estaEmAberto":true},
 {"id":61,"inicio":"2026-09-17T10:00:00","severidade":"Alta","bairro":"Centro","estaEmAberto":true},
 {"id":62,"inicio":"2026-09-17T10:00:00","severidade":"Alta","bairro":"Centro","estaEmAberto":true},
 {"id":63,"inicio":"2026-09-17T10:00:00","severidade":"Alta","bairro":"Centro","estaEmAberto":true}]
```

Os dois períodos foram consultados em sequência, contra os mesmos dados: 28
contra 4 mostra o filtro fazendo efeito, não uma consulta que ignora o
intervalo.

O `Periodo` entra aqui como **parâmetro de entrada**, não como coluna
consultada: `IncidentesPorBairroQuery(int BairroId, Periodo Intervalo)`, e o
handler usa `Intervalo.Inicio` e `Intervalo.Fim` como limites do `WHERE`.
Período sem fim (`Fim == null`) é tratado como "até agora".

## Semana 7: Hash de senha — `Usuario`, `IHasherDeSenha` e PBKDF2

### O que se quer provar

A senha em texto entra em `Usuario.Registrar` e não sai: o agregado guarda
apenas `SenhaHash`, e não existe propriedade ou método que devolva a senha
original. Duas contas com a mesma senha produzem hashes diferentes, porque
o `PasswordHasher` do ASP.NET Core Identity gera salt aleatório por senha.
A verificação funciona nos dois sentidos: senha certa confere, senha errada
não confere.

Domínio em `Mobilidade.Domain/Entity/Usuario.cs`, `ValueObjects/Email.cs`,
`ValueObjects/Papel.cs` e `Interfaces/IHasherDeSenha.cs`. Implementação em
`Mobilidade.Infrastructure/Seguranca/HasherDeSenhaPbkdf2.cs`. Testes em
`Mobilidade.Tests/UsuarioTests.cs`.

Os testes usam o hasher real (`new HasherDeSenhaPbkdf2()`), não um dublê —
um fake com salt fixo faria o teste do salt passar sem provar nada sobre o
algoritmo que a aplicação de fato usa.

### O contrato cobrado pelo compilador

Antes de implementar os métodos, a classe declarou `: IHasherDeSenha` vazia.
O build recusou:

```
Seguranca/HasherDeSenhaPbkdf2.cs(6,36): error CS0535: "HasherDeSenhaPbkdf2"
não implementa membro de interface "IHasherDeSenha.Gerar(string)"
Seguranca/HasherDeSenhaPbkdf2.cs(6,36): error CS0535: "HasherDeSenhaPbkdf2"
não implementa membro de interface "IHasherDeSenha.Verificar(string, string)"
```

A interface não é documentação: o compilador exige as duas assinaturas
exatas antes de aceitar a classe como implementação.

### Comando

```bash
dotnet test --filter UsuarioTests
```

**Saída:**

```
[xUnit.net 00:00:00.08]   Discovering: Mobilidade.Tests
[xUnit.net 00:00:00.13]   Discovered:  Mobilidade.Tests
[xUnit.net 00:00:00.15]   Starting:    Mobilidade.Tests
[xUnit.net 00:00:00.55]   Finished:    Mobilidade.Tests
  Mobilidade.Tests teste net10.0 êxito (1,3s)

Resumo do teste: total: 5; falhou: 0; bem-sucedido: 5; ignorado: 0; duração: 1,3s
Construir êxito(s) com 4 aviso(s) em 3,3s
```

> Saídas cortadas: removidos os avisos de build (NU1903, CS8618, CS8602) e a
> lista de projetos compilados, que se repetem em todas as execuções.

### Os cinco casos

| Teste | O que prova |
|---|---|
| `SenhaConfere_ComSenhaCerta_RetornaTrue` | a senha registrada confere contra o próprio hash |
| `SenhaConfere_ComSenhaErrada_RetornaFalse` | senha diferente não confere |
| `Registrar_MesmaSenhaEmDoisUsuarios_GeraHashesDiferentes` | salt aleatório por senha |
| `Registrar_ComSenhaCurta_LancaDomainException` | 7 caracteres é rejeitado |
| `Registrar_ComSenhaDeOitoCaracteres_CriaUsuario` | 8 caracteres é aceito |

### Os dois testes que falharam antes de passar

**1. `SenhaConfere_ComSenhaErrada_RetornaFalse` — assert invertido**

O primeiro esboço repetiu o `Assert.True` do teste anterior:

```
Assert.True() Failure
Expected: True
Actual:   False
UsuarioTests.cs(36,0): at UsuarioTests.SenhaConfere_ComSenhaErrada_RetornaFalse()
```

`Actual: False` é o comportamento correto do domínio — o `SenhaConfere`
rejeitou a senha errada. O que estava errado era a expectativa do teste.
Corrigido para `Assert.False`.

Essa falha responde a pergunta que o par de testes existe para responder: se
`SenhaConfere` devolvesse `true` incondicionalmente, este teste teria passado
com o `Assert.True`. Ele devolveu `false`, logo a comparação de fato acontece.

**2. `Registrar_ComSenhaCurta_LancaDomainException` — senha longa demais**

A senha usada no primeiro esboço tinha nove caracteres (`"123456789"`), acima
do mínimo:

```
Assert.Throws() Failure: No exception was thrown
Expected: typeof(Mobilidade.Domain.Excecoes.DomainException)
UsuarioTests.cs(59,0): at UsuarioTests.Registrar_ComSenhaCurta_LancaDomainException()
```

O `Registrar` aceitou a senha e criou o usuário — correto, já que a regra é
`Length < 8`. Corrigido para uma senha de sete caracteres.

### Tentativa rejeitada: assert sobre o objeto errado

O teste do salt passou na primeira execução sem provar nada. O assert
comparava as duas instâncias do hasher:

```csharp
Assert.NotEqual(hasherUsuario1, hasherUsuario2);
```

Duas instâncias distintas de uma classe nunca são iguais, então esse assert
passaria mesmo se o algoritmo não tivesse salt — não olha para senha nem
para hash. A segunda tentativa comparava `usuario1` com `usuario2`, com o
mesmo defeito.

Corrigido para comparar os hashes, com um **único** hasher compartilhado
pelos dois usuários:

```csharp
Assert.NotEqual(usuario1.SenhaHash, usuario2.SenhaHash);
```

Com um hasher só, a única explicação possível para a diferença entre os
hashes é o salt gerado por chamada.

### Conflito de versão de pacote ao instalar o Identity

`dotnet add package Microsoft.Extensions.Identity.Core` (sem versão) trouxe a
10.0.12 e o restore falhou:

```
error: NU1605: Downgrade de pacote detectado:
Microsoft.Extensions.Configuration.Abstractions de 10.0.12 para 10.0.11.
 Mobilidade.Infrastructure -> Microsoft.Extensions.Identity.Core 10.0.12
   -> Microsoft.Extensions.Diagnostics 10.0.12
   -> Microsoft.Extensions.Configuration 10.0.12
   -> Microsoft.Extensions.Configuration.Abstractions (>= 10.0.12)
 Mobilidade.Infrastructure
   -> Microsoft.Extensions.Configuration.Abstractions (>= 10.0.11)
```

A Infrastructure já usava `Configuration.Abstractions` 10.0.11; a 10.0.12 do
Identity exigia 10.0.12 ou maior. Resolvido fixando o pacote na versão que o
resto do projeto usa:

```bash
dotnet add src/Mobilidade.Infrastructure package Microsoft.Extensions.Identity.Core --version 10.0.11
```

```
info : PackageReference do pacote 'Microsoft.Extensions.Identity.Core' versão
'10.0.11' atualizada no arquivo 'Mobilidade.Infrastructure.csproj'.
log  : Mobilidade.Infrastructure.csproj restaurado (em 1,34 seg).
```

### Por quê

- **Interface no Domain, implementação na Infrastructure:** o `Usuario` sabe
  *que* a senha vira hash; *como* (algoritmo, custo, salt) é decisão de
  infraestrutura. Mesmo DIP já aplicado a `IIncidenteRepository` — o agregado
  depende da abstração, e trocar PBKDF2 por outro algoritmo custa uma classe
  nova, sem tocar no domínio (decisão #29).
- **PBKDF2 em vez de SHA256:** SHA256 é rápido por projeto, e velocidade é a
  propriedade errada para senha — uma GPU calcula bilhões por segundo,
  tornando força bruta sobre banco vazado viável. Também não tem salt
  embutido nem custo ajustável (decisão #29).
- **`PasswordHasher<object>`:** a API do Identity espera um tipo de usuário
  como primeiro parâmetro de `HashPassword`, mas não o usa no cálculo. O
  projeto não adota o Identity inteiro, só o algoritmo — daí o `object` e a
  instância descartável `SemUsuario`.
- **`Verificar` aceita `SuccessRehashNeeded` como sucesso:** esse resultado
  significa senha correta com hash gerado por um custo antigo. Tratá-lo como
  falha impediria o login de quem se registrou antes de uma mudança de
  parâmetros.
- **`Papel` como value object, não enum:** `(Papel)99` compilaria com enum. Com
  construtor privado e instâncias estáticas, um terceiro papel é
  estruturalmente impossível — mesmo raciocínio da decisão #5, aplicado aqui.
  O `Nome` é a string que vai para a claim `role` e para
  `[Authorize(Roles = "Admin")]`.
- **`Email` normaliza para minúsculas e sem espaços:** o índice único da
  migration compara texto. Sem normalizar, `Fulano@X.com` e `fulano@x.com`
  seriam duas contas para o mesmo endereço.
- **A validação de 8 caracteres é testada nos dois lados da fronteira:** 7
  rejeitado e 8 aceito provam que a regra é `< 8`, não `<= 8`.

### Pendente nesta issue

- ~~Mapeamento de `Usuario` no EF Core e migration com índice único no
  email.~~ **Resolvido** na seção "Semana 7: Migration da tabela `Usuarios`".
- ~~Repositório traduzindo violação de índice único para `DomainException`.~~
  **Resolvido** em `RepositorioUsuariosEfCore.Criar`.
- ~~Seed do Admin com senha vinda de variável de ambiente.~~ **Resolvido** na
  seção "Semana 7: Registro de usuário".
- ~~`POST /api/usuarios`.~~ **Resolvido** na seção "Semana 7: Registro de
  usuário".
- ~~**Prova no banco de que duas contas com a mesma senha têm hashes
  diferentes.**~~ **Resolvido** na seção "Semana 7: Registro de usuário", com
  o `SELECT` na tabela real.
- Grep no repositório pela senha do seed, provando zero ocorrências fora da
  variável de ambiente.

  ## Semana 7: Migration da tabela `Usuarios` com índice único no email

### O que se quer provar

A tabela `Usuarios` existe no banco com as quatro colunas do agregado, e o
email tem índice **único** — a garantia de unicidade fica no banco, não numa
checagem de aplicação que duas requisições simultâneas conseguiriam furar.

Os dois Value Objects (`Email` e `Papel`) são persistidos como texto via
`ValueConverter`, mesmo mecanismo já usado para `Severidade` na decisão #5.

Configuração em
`src/Mobilidade.Infrastructure/Data/Configuracoes/UsuarioConfiguration.cs`.
Registro em `IncidentesDbContext` (`DbSet<Usuario>` + `ApplyConfiguration`).

### Conversão dos Value Objects

```csharp
builder.Property(u => u.Email)
    .HasConversion(
        email => email.Valor,
        valor => Email.Criar(valor))
    .HasMaxLength(254)
    .IsRequired();

builder.Property(u => u.Papel)
    .HasConversion(
        papel => papel.Nome,
        nome => Papel.DeNome(nome))
    .HasMaxLength(20)
    .IsRequired();

builder.HasIndex(u => u.Email).IsUnique();
```

O caminho de volta (`Email.Criar` e `Papel.DeNome`) não é só desserialização:
são os mesmos métodos de validação usados na criação. Um valor inválido no
banco — papel `"Gerente"`, email sem `@` — lança `DomainException` na leitura,
em vez de materializar um agregado em estado impossível.

### Migration criada

```bash
dotnet ef migrations add CriaTabelaUsuarios --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api
```

**Saída:**
```
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

### Tentativa de aplicar — falha de conexão

```bash
dotnet ef database update --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api
```

**Saída (trecho relevante):**
```
Microsoft.Data.SqlClient.SqlException (0x80131904): Erro de rede ou específico
à instância ao estabelecer conexão com o SQL Server. O servidor não foi
encontrado ou não estava acessível. (provider: Provedor de TCP, error: 0 -
Este host não é conhecido.)
 ---> System.ComponentModel.Win32Exception (11001): Este host não é conhecido.
Error Number:11001,State:0,Class:20
```

O banco estava de pé:

```bash
docker ps
```

```
CONTAINER ID   IMAGE                                        STATUS                        PORTS                     NAMES
6c5ecf2f6f4a   painel-mobilidade-api                        Up 56 seconds                 0.0.0.0:8080->8080/tcp    painel-mobilidade-api-1
04152ba45c54   mcr.microsoft.com/mssql/server:2022-latest   Up About a minute (healthy)   0.0.0.0:1433->1433/tcp    mobilidade-sqlserver
```

Erro 11001 é falha de **resolução de nome**, não de autenticação nem de
servidor fora do ar: o cliente não descobriu o endereço do host. O
`appsettings.json` aponta para `Server=sqlserver,1433` — nome do serviço no
`docker-compose.yml`, que só resolve dentro da rede do Compose. O `dotnet ef`
roda no host, fora dessa rede.

O `appsettings.Development.json`, que apontava para `localhost`, não existe no
repositório: ele consta no `.gitignore` (seção "Ambiente / segredos"), porque
carrega a senha do banco. Um commit anterior removeu do rastreamento a cópia
que havia sido commitada por engano, e com ela sumiu a configuração local desta
máquina.

### Migration aplicada

Contornado passando a connection string do host diretamente:

```bash
dotnet ef database update --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api --connection "Server=localhost,1433;Database=MobilidadeDb;User Id=sa;Password=SuaSenhaAqui;TrustServerCertificate=True"
```

**SQL executado:**
```
Applying migration '20260923165425_CriaTabelaUsuarios'.

CREATE TABLE [Usuarios] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(254) NOT NULL,
    [SenhaHash] nvarchar(255) NOT NULL,
    [Papel] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Usuarios_Email] ON [Usuarios] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260923165425_CriaTabelaUsuarios', N'10.0.11');
Done.
```

**LINHA RELEVANTE**

`CREATE UNIQUE INDEX [IX_Usuarios_Email] ON [Usuarios] ([Email]);`

> Saídas cortadas: removidos o aviso NU1903, o aviso de versão das ferramentas
> do EF e os comandos de lock/histórico de migration, que se repetem em toda
> aplicação.

### Por quê

- **Índice único no email, não checagem na aplicação:** um `SELECT` antes do
  `INSERT` tem janela de corrida — duas requisições simultâneas passam as
  duas pela checagem e gravam as duas. O banco é o único ponto que decide
  serialmente. A aplicação traduz a violação para `DomainException`; ela não
  a previne.
- **`HasMaxLength(254)` no email não é enfeite:** sem ele o EF geraria
  `nvarchar(max)`, e o SQL Server recusa índice em coluna desse tipo. O
  número é o limite de um endereço de email pelo padrão da internet.
- **`Email` normalizado para minúsculas antes de chegar aqui:** o índice
  único compara texto. Sem a normalização feita em `Email.Criar`,
  `Fulano@X.com` e `fulano@x.com` seriam duas linhas distintas para o mesmo
  endereço, e o índice não impediria nada.
- **`HasMaxLength(255)` no `SenhaHash`:** o formato v3 do `PasswordHasher`
  produz 84 caracteres em base64 (marcador de formato, algoritmo, iterações,
  salt de 16 bytes e subchave de 32 bytes). 255 acomoda o atual com folga
  para uma mudança de versão do formato.
- **Sem índice no `SenhaHash`:** índice único ali significaria que duas
  contas nunca poderiam ter o mesmo hash. Com salt aleatório isso quase
  nunca colidiria, mas se colidisse o banco recusaria o cadastro — e a
  recusa informaria que aquele hash já existe. Unicidade é regra de negócio
  do email, não do hash.
- **`ApplyConfiguration` explícito:** o `IncidentesDbContext` registra cada
  configuração uma a uma, em vez de `ApplyConfigurationsFromAssembly`.
  Esquecer a linha do `UsuarioConfiguration` não geraria erro nenhum — a
  configuração seria ignorada em silêncio e a migration sairia com as
  convenções padrão do EF, sem índice e com `nvarchar(max)`.

### Pendente

- ~~**`appsettings.Development.json` ausente.**~~ **Resolvido** na seção
  seguinte: o arquivo foi recriado localmente com `localhost,1433`. Ele está
  no `.gitignore` por carregar a senha, então continua fora do repositório —
  quem clonar precisa criá-lo (pendência de documentação no README).
- ~~**Repositório traduzindo violação de índice único para
  `DomainException`.**~~ **Resolvido** em `RepositorioUsuariosEfCore.Criar`,
  que trata os erros 2601/2627 do SQL Server quando vêm do índice
  `IX_Usuarios_Email`.
- ~~**Prova no banco de que duas contas com a mesma senha têm hashes
  diferentes.**~~ **Resolvido** na seção "Semana 7: Registro de usuário".
  
## Semana 7: Registro de usuário — endpoint, seed do Admin e hashes distintos no banco

### O que se quer provar

`POST /api/usuarios` cria um usuário com a senha já em hash, e a resposta não
carrega o hash. Duas contas registradas com **a mesma senha** têm hashes
diferentes gravados no banco — a prova de salt pedida pela issue, agora contra
a tabela real e não só em memória.

O Admin inicial nasce de uma migration que lê a senha de uma variável de
ambiente; nenhuma senha aparece no código.

Endpoint em `src/Mobilidade.Api/Controllers/UsuarioController.cs`. DTOs em
`src/Mobilidade.Api/Dtos/`. Migration
`20260923225151_SeedUsuarioAdmin`.

### Seed do Admin — senha de variável de ambiente

A migration não grava senha nenhuma: ela lê `ADMIN_SENHA_INICIAL` do ambiente,
calcula o hash pelo mesmo `HasherDeSenhaPbkdf2` que a aplicação usa, e insere o
resultado. Sem a variável definida, a migration falha em vez de criar um admin
com senha vazia ou previsível:

```csharp
var senha = Environment.GetEnvironmentVariable("ADMIN_SENHA_INICIAL");

if (string.IsNullOrWhiteSpace(senha))
    throw new InvalidOperationException(
        "A variável de ambiente ADMIN_SENHA_INICIAL não está definida...");

var admin = Usuario.Registrar(
    Email.Criar("admin@mobilidade.local"), senha, Papel.Admin, new HasherDeSenhaPbkdf2());

migrationBuilder.InsertData(
    table: "Usuarios",
    columns: new[] { "Email", "SenhaHash", "Papel" },
    values: new object[] { admin.Email.Valor, admin.SenhaHash, admin.Papel.Nome });
```

Definindo a variável e aplicando:

```bash
set ADMIN_SENHA_INICIAL=<senha definida fora do repositório>
dotnet ef database update --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api --connection "Server=localhost,1433;Database=MobilidadeDb;User Id=sa;Password=SuaSenhaAqui;TrustServerCertificate=True"
```

**SQL executado:**
```
Applying migration '20260923225151_SeedUsuarioAdmin'.

INSERT INTO [Usuarios] ([Email], [SenhaHash], [Papel])
VALUES (N'admin@mobilidade.local', N'AQAAAAIAAYagAAAAEGcZVLeAG0EP...', N'Admin');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260923225151_SeedUsuarioAdmin', N'10.0.11');
Done.
```

> Hash truncado nesta evidência. O `InsertData` do EF emite o valor literal no
> SQL, então o hash completo aparece no log de quem aplica a migration — hash
> não é senha, mas é consequência da escolha de semear pela migration.

### Falha de conexão da API rodando fora do Docker

Primeira chamada ao endpoint respondeu 500. O log da API mostrou o mesmo
erro 11001 da seção anterior:

```
fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
      Microsoft.Data.SqlClient.SqlException (0x80131904): Erro de rede ou
      específico à instância ao estabelecer conexão com o SQL Server.
      (provider: Provedor de TCP, error: 0 - Este host não é conhecido.)
       ---> System.ComponentModel.Win32Exception (11001): Este host não é conhecido.
         at Mobilidade.Infrastructure.Repositorio.RepositorioUsuariosEfCore.Criar(Usuario usuario)
         at Mobilidade.Api.Controllers.UsuariosController.Criar(CriarUsuarioRequest request)
Error Number:11001,State:0,Class:20
```

`dotnet run` sobe em ambiente Development e lê o `appsettings.json`, que aponta
para `Server=sqlserver,1433` — nome que só resolve dentro da rede do Compose.
Resolvido recriando `appsettings.Development.json` com `localhost,1433`, o
arquivo que a pendência da seção anterior apontava como ausente. Ele já consta
no `.gitignore` (seção "Ambiente / segredos"), então a senha não entra no
repositório.

### Dois usuários com a mesma senha

Ambos registrados com `"senha":"mesmasenha123"`:

```bash
curl -i -X POST http://localhost:5041/api/Usuarios -H "Content-Type: application/json" -d "{\"email\":\"joao@teste.com\",\"senha\":\"mesmasenha123\",\"papel\":\"Operador\"}"
```

```
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Location: http://localhost:5041/api/Usuarios?id=2

{"id":2,"email":"joao@teste.com","papel":"Operador"}
```

```bash
curl -i -X POST http://localhost:5041/api/Usuarios -H "Content-Type: application/json" -d "{\"email\":\"maria@teste.com\",\"senha\":\"mesmasenha123\",\"papel\":\"Operador\"}"
```

```
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Location: http://localhost:5041/api/Usuarios?id=3

{"id":3,"email":"maria@teste.com","papel":"Operador"}
```

As duas respostas trazem `id`, `email` e `papel`. **Nenhuma traz `senhaHash`** —
o `UsuarioResponse` não tem esse campo, então ele não pode escapar por
serialização acidental do agregado.

### Os hashes no banco — mesma senha, hashes diferentes

```bash
docker exec -it mobilidade-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SuaSenhaAqui" -d MobilidadeDb -C -Q "SELECT Id, Email, SenhaHash, Papel FROM Usuarios"
```

**Saída (hashes truncados após os 40 primeiros caracteres):**
```
Id   Email                     SenhaHash                                   Papel
---- ------------------------- ------------------------------------------- ---------
 1   admin@mobilidade.local    AQAAAAIAAYagAAAAEGcZVLeAG0EPx6OnccaBfguw... Admin
 2   joao@teste.com            AQAAAAIAAYagAAAAELQWmkK/uOWhBqrZi1hGFwhu... Operador
 3   maria@teste.com           AQAAAAIAAYagAAAAEEjg4njuS/n6PBe1FrPK6WRG... Operador

(3 rows affected)
```

**A leitura que importa.** Os Ids 2 e 3 foram registrados com a mesma senha.
Os 17 primeiros caracteres (`AQAAAAIAAYagAAAAE`) são idênticos nos três — é o
cabeçalho do formato v3 do `PasswordHasher`, que codifica versão, algoritmo e
número de iterações, e não depende da senha. A partir daí os valores divergem
completamente: `LQWmkK/uOWhBqrZi1hGFwhu...` contra `Ejg4njuS/n6PBe1FrPK6WRG...`.

Essa divergência é o salt aleatório gerado por chamada. Com SHA256 ou MD5 sem
salt, as duas linhas teriam hash byte a byte idêntico, e quem lesse a tabela
saberia que João e Maria usam a mesma senha — sem quebrar hash nenhum.

### Por quê

- **Seed na migration, não no start da aplicação:** é o que a issue pede, e
  garante que o admin exista assim que o banco está migrado, sem depender de a
  API subir. O custo é o hash literal no SQL da migration e a exigência da
  variável de ambiente em toda aplicação — inclusive em CI, que precisaria
  defini-la.
- **A migration falha se a variável não existir:** seed pulado em silêncio
  deixaria o banco sem nenhum admin, e a descoberta só viria na primeira
  tentativa de login. Falhar cedo torna o problema visível no momento em que
  ele acontece.
- **`Down` deleta pelo email, não pelo Id:** o Id é `IDENTITY`, gerado pelo
  banco, e não se conhece no momento de escrever a migration.
- **DTO recebe `string Papel`, não o Value Object:** mesmo motivo da
  decisão #18 — o `Papel` tem construtor privado e o `System.Text.Json` não
  consegue materializá-lo. A conversão acontece no controller, via
  `Papel.DeNome`, que rejeita qualquer valor fora de `Admin` e `Operador`
  com `DomainException` (422).
- **`UsuarioResponse` em vez de devolver o agregado:** o `IncidentesController`
  devolve a entidade direto, e por isso a resposta dele carrega campos internos
  como `eventos`. Para `Usuario` isso exporia `SenhaHash` em toda resposta de
  criação. Um DTO de saída é o que garante que o hash não sai pela API.

### Pendente

- **`[Authorize(Roles = "Admin")]` no endpoint.** Hoje qualquer um pode chamar
  `POST /api/usuarios` e escolher o próprio papel — inclusive `"Admin"`. É o
  mass assignment que a Issue D cobra, e fecha na Issue C. O
  `Papel.DeNome` impede papel *inválido*, não impede autopromoção.
- **Rotação da senha do `sa`.** A credencial da decisão #12 foi exposta de novo
  durante o desenvolvimento desta semana e não foi rotacionada. Dívida em
  aberto.

  ### Grep pela senha do seed — zero ocorrências

```bash
grep -rn "<senha do seed>" --include="*.cs" --include="*.json" --include="*.md" --include="*.yml" .
```

**Saída:** vazia. Nenhuma ocorrência da senha do Admin inicial em código,
configuração, documentação ou arquivos do compose. Ela existe apenas na
variável de ambiente `ADMIN_SENHA_INICIAL`, lida em tempo de execução pela
migration `SeedUsuarioAdmin`, e o hash gravado no banco não permite recuperá-la.

## Semana 7: Login e JWT — token assinado e 401 indistinguível

### O que se quer provar

`POST /api/auth/login` devolve 200 com um token assinado quando a senha
confere. Email inexistente e senha errada devolvem **o mesmo 401, com o mesmo
corpo** — a resposta não diz qual dos dois falhou, o que impede enumerar quais
emails existem no sistema.

O token carrega as seis claims mínimas (`sub`, `email`, `role`, `exp`, `iss`,
`aud`), é assinado com HS256, e a chave vem de variável de ambiente.

Interface `IGeradorDeToken` em `Mobilidade.Domain/Interfaces/`, implementação
`GeradorDeTokenJwt` em `Mobilidade.Infrastructure/Seguranca/`. Endpoint em
`Mobilidade.Api/Controllers/AuthController.cs`.

### A chave nunca está no código

```csharp
var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Chave"]!));
var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
```

O código conhece apenas o **nome** da configuração. O valor vem da variável de
ambiente `Jwt__Chave` (o duplo underscore é como o ASP.NET mapeia a hierarquia
`Jwt:Chave` para variáveis de ambiente), definida fora do repositório. O mesmo
vale para `Jwt__Emissor` e `Jwt__Audiencia`.

### Login com a senha certa — 200 com token

```bash
curl -i -X POST http://localhost:5041/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"joao@teste.com\",\"senha\":\"mesmasenha123\"}"
```

```
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"token":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIyIiwiZW1haWwiOiJqb2FvQHRlc3RlLmNvbSIsInJvbGUiOiJPcGVyYWRvciIsImV4cCI6MTc5MDI1NTU0NCwiaXNzIjoicGFpbmVsLW1vYmlsaWRhZGUiLCJhdWQiOiJwYWluZWwtbW9iaWxpZGFkZSJ9.<assinatura truncada>"}
```

### O token decodificado, com as três partes separadas

**Cabeçalho** (`eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9`):
```json
{"alg":"HS256","typ":"JWT"}
```

**Corpo** (`eyJzdWIiOiIyIiwiZW1haWwiOiJqb2FvQHRlc3RlLmNvbSIsInJvbGUi...`):
```json
{
  "sub": "2",
  "email": "joao@teste.com",
  "role": "Operador",
  "exp": 1790255544,
  "iss": "painel-mobilidade",
  "aud": "painel-mobilidade"
}
```

**Assinatura:** truncada nesta evidência. É o HMAC-SHA256 do cabeçalho mais o
corpo, calculado com a chave que só o servidor conhece.

As seis claims exigidas estão presentes. O corpo é legível por qualquer um —
base64 não é criptografia, é codificação. O que protege o token não é esconder
o conteúdo, é a assinatura.

### Dois logins do mesmo usuário geram tokens diferentes

Primeira chamada, às 12:42:23:
```
{"token":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIyIiwiZW1haWwiOiJqb2FvQHRlc3RlLmNvbSIsInJvbGUiOiJPcGVyYWRvciIsImV4cCI6MTc5MDI1NTU0NCwiaXNzIjoicGFpbmVsLW1vYmlsaWRhZGUiLCJhdWQiOiJwYWluZWwtbW9iaWxpZGFkZSJ9.Mw2RM2MAjATWwu<truncada>"}
```

Segunda chamada, às 12:49:31:
```
{"token":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIyIiwiZW1haWwiOiJqb2FvQHRlc3RlLmNvbSIsInJvbGUiOiJPcGVyYWRvciIsImV4cCI6MTc5MDI1NTk3MSwiaXNzIjoicGFpbmVsLW1vYmlsaWRhZGUiLCJhdWQiOiJwYWluZWwtbW9iaWxpZGFkZSJ9.oDl2cFS8C0oi3m<truncada>"}
```

**Por que diferem.** O cabeçalho é idêntico nos dois. No corpo, a **única**
claim que mudou foi o `exp`: `1790255544` contra `1790255971` — 427 segundos
de diferença, exatamente o intervalo entre as duas requisições (12:42:23 e
12:49:31). `sub`, `email`, `role`, `iss` e `aud` são iguais.

Não há aleatoriedade envolvida: o `exp` é calculado no momento da emissão com
`DateTime.UtcNow.AddMinutes(30)`. Dois logins **no mesmo segundo** produziriam
tokens byte a byte idênticos, porque as claims e a chave seriam as mesmas. Isso
é o oposto do hash de senha, onde o salt aleatório garante saídas distintas
mesmo para entradas iguais.

A assinatura, porém, mudou **inteira** — não apenas na parte correspondente ao
`exp`. Essa é a propriedade do HMAC que a Issue D vai explorar: alterar um único
caractere da entrada produz uma saída completamente diferente, e é por isso que
um token com a claim `role` adulterada não sobrevive à validação.

### Senha errada — 401

```bash
curl -i -X POST http://localhost:5041/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"joao@teste.com\",\"senha\":\"senhaerrada999\"}"
```

```
HTTP/1.1 401 Unauthorized
Content-Type: application/json; charset=utf-8

{"mensagem":"Email ou senha inválidos."}
```

### Email inexistente — 401 idêntico

```bash
curl -i -X POST http://localhost:5041/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"naoexiste@teste.com\",\"senha\":\"mesmasenha123\"}"
```

```
HTTP/1.1 401 Unauthorized
Content-Type: application/json; charset=utf-8

{"mensagem":"Email ou senha inválidos."}
```

**As duas respostas são idênticas** em status, cabeçalhos e corpo. Um atacante
que mande mil emails ao endpoint não consegue separar os que existem dos que
não existem a partir da resposta.

### `try/catch` no login: exceção deliberada à decisão #20

`Email.Criar` lança `DomainException` para email malformado, e o
`DomainExceptionFilter` traduziria isso para **422**. No login isso abriria um
terceiro comportamento observável:

| Entrada | Sem o `try/catch` | Com o `try/catch` |
|---|---|---|
| `"abc"` (malformado) | 422 | 401 |
| `naoexiste@teste.com` | 401 | 401 |
| senha errada | 401 | 401 |

O 422 diria ao atacante que aquele valor nem chegou a ser consultado — outra
forma de vazar informação. Por isso o controller captura a exceção no ponto:

```csharp
Email email;
try
{
    email = Email.Criar(request.Email);
}
catch (DomainException)
{
    return Unauthorized(new { mensagem = "Email ou senha inválidos." });
}
```

A decisão #20 removeu os `try/catch` dos controllers justamente para não
depender de disciplina. Este é uma exceção consciente: o login é o único
endpoint que precisa **achatar** todos os erros num único status, e isso é o
oposto do que o filtro global faz, que é distinguir tipos de erro.

### Bug encontrado: chave ausente só falha no primeiro login

O primeiro teste do endpoint respondeu 500. O log:

```
fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
      System.ArgumentNullException: Value cannot be null. (Parameter 's')
         at System.Text.Encoding.GetBytes(String s)
         at Mobilidade.Infrastructure.Seguranca.GeradorDeTokenJwt.Gerar(Usuario usuario)
            in GeradorDeTokenJwt.cs:line 29
         at Mobilidade.Api.Controllers.AuthController.Login(LoginRequest request)
            in AuthController.cs:line 46
```

A variável `Jwt__Chave` estava definida em outra janela do terminal, não na que
subiu a API. `_config["Jwt:Chave"]` devolveu `null` e o `GetBytes` estourou.

O `!` (null-forgiving) na linha 29 é uma promessa ao compilador, não uma
verificação em runtime — ele silencia o aviso e não impede nada.

**Dívida identificada:** a aplicação sobe normalmente sem a chave e só quebra
na primeira tentativa de login, com 500 e sem mensagem útil ao cliente. O
correto seria validar a configuração na inicialização e falhar ali, como a
migration `SeedUsuarioAdmin` faz com `ADMIN_SENHA_INICIAL`. Registrado como
pendência.

### Por quê

- **Claim `role` no nome curto, não `ClaimTypes.Role`:** o `ClaimTypes.Role` do
  .NET expande para a URI
  `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`, que é o que
  aparecia no token na primeira versão. A issue pede `role`, e o token fica
  menor e mais legível. O custo é que a validação da Issue C precisa declarar
  `RoleClaimType = "role"` — sem isso, `[Authorize(Roles = "Admin")]` procura
  pela URI, não encontra, e devolve 403 até para quem é Admin.
- **Expiração de 30 minutos:** meio do intervalo permitido pela issue (15 a
  60). Um token roubado vale por no máximo meia hora; menos que isso obrigaria
  reautenticação frequente num sistema que ainda não tem refresh token.
- **`DateTime.UtcNow`, não `DateTime.Now`:** o `exp` do JWT é definido em UTC.
  Usar hora local faria o token expirar 3 horas fora do previsto neste fuso, e
  o erro só apareceria em produção com outro fuso configurado.
- **`LoginRequest` sem `[EmailAddress]` e sem `[StringLength]`:** o
  `CriarUsuarioRequest` tem as duas anotações, mas no login elas gerariam 400
  para entradas malformadas — um terceiro status observável, pelo mesmo motivo
  do `try/catch` acima. O login valida tudo internamente e responde sempre 401.
- **`LoginResponse` carrega só o token:** nada de dados do usuário na resposta.
  Quem precisar deles lê as claims do próprio token.
- **Chave em variável de ambiente, não em `appsettings.Development.json`:** o
  arquivo está no `.gitignore`, mas já foi commitado por engano uma vez neste
  repositório. Variável de ambiente não tem como ser commitada.

### Pendente

- **Validar a presença da chave na inicialização**, em vez de falhar no
  primeiro login.
- **Grep no log após login com senha errada**, provando que a senha digitada
  não aparece — item da Issue D.
- **Rate limiting no login.** Nada limita tentativas hoje; cem requisições
  seguidas com senha errada são todas processadas. Item da Issue D.

  ### Grep no log após login com senha errada — zero ocorrências

O projeto não tem logging em arquivo configurado; o log vai para o console.
Para poder grepá-lo, a API foi subida com a saída redirecionada:

```bash
dotnet run --project src/Mobilidade.Api > log-api.txt 2>&1
```

Login com senha deliberadamente distinta, para que qualquer ocorrência no log
fosse inequívoca:

```bash
curl -i -X POST http://localhost:5041/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"joao@teste.com\",\"senha\":\"SenhaErradaParaTesteDeLog123\"}"
```

```
HTTP/1.1 401 Unauthorized
{"mensagem":"Email ou senha inválidos."}
```

Grep pela senha digitada:

```bash
grep -n "SenhaErradaParaTesteDeLog123" log-api.txt
```

**Saída:** vazia.

Confirmação de que o arquivo não está vazio — sem isso, o grep vazio não
provaria nada:

```bash
grep -c "" log-api.txt
```

```
21
```

**Por que a senha não aparece.** Ela entra no `LoginRequest`, é passada a
`usuario.SenhaConfere` e descartada. Não há `_logger` no `AuthController`, e a
`DomainException` capturada no `try/catch` carrega a mensagem do domínio
("Email em formato inválido"), nunca o valor digitado. As mensagens de
validação do `Usuario.Registrar` seguem a mesma regra: dizem a regra violada,
não o valor.

## Semana 7: Autorização por papel — 401 contra 403 e negado por padrão

### O que se quer provar

Sem token, o servidor não sabe quem é: **401**. Com token válido mas papel
insuficiente, ele sabe quem é e sabe que não pode: **403**. São respostas
diferentes porque as perguntas são diferentes — autenticação é "quem é você",
autorização é "o que você pode fazer".

Endpoints sem `[Authorize]` explícito ficam **negados por padrão**, não
abertos: a política de fallback exige usuário autenticado em qualquer rota que
não tenha declarado o contrário.

Configuração em `src/Mobilidade.Api/Program.cs`. Atributos em
`IncidentesController.cs` e `UsuarioController.cs`.

### As quatro validações do token

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Chave"]!)),

            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Emissor"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audiencia"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            RoleClaimType = "role"
        };
    });
```

Cada validação tem um par: o `Validate...` que liga a checagem, e o `Valid...`
(ou `IssuerSigningKey`) que diz contra o quê comparar. Assinatura, emissor,
audiência e expiração — as quatro exigidas pela issue.

`ClockSkew = TimeSpan.Zero` remove a tolerância padrão do .NET, que é de 5
minutos. Sem isso, um token expirado continuaria sendo aceito por mais cinco
minutos, e o ataque de token expirado da Issue D não provaria nada.

### Negado por padrão

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

A `FallbackPolicy` vale para toda rota que **não** declara sua própria política.
Um endpoint novo criado amanhã sem `[Authorize]` nasce fechado, não aberto.

O custo é que login e health também ficariam trancados, e por isso ambos têm
`[AllowAnonymous]` (no controller) e `.AllowAnonymous()` (no minimal API do
`/health`). O `/api/incidentes/importar` e o `/api/debug/sql` foram
deliberadamente **deixados sem** a liberação: passam a exigir token
autenticado, que é o comportamento correto para um endpoint de escrita e para
um que expõe estrutura interna.

### Ordem dos middlewares

```csharp
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

`UseAuthentication` antes de `UseAuthorization`, e ambos antes de
`MapControllers`. O primeiro lê o token e monta a identidade; o segundo decide
se aquela identidade pode acessar o recurso. Invertidos, o segundo rodaria
antes de existir identidade e trataria todo mundo como anônimo — 401 em tudo,
inclusive com token válido.

### A tabela

Cada célula é uma requisição real feita com `curl` contra a API em
`http://localhost:5041`, com o banco real. Comando usado para capturar apenas
o status:

```bash
curl -s -o nul -w "GET /Incidentes -> %{http_code}\n" http://localhost:5041/api/Incidentes -H "Authorization: Bearer %OP%"
```

| Endpoint | Sem token | Operador | Admin |
|---|---|---|---|
| `GET /api/Incidentes` | **401** | **200** | **200** |
| `GET /api/Incidentes/2` | **401** | **200** | **200** |
| `GET /api/Incidentes/bairro/1` | **401** | **200** | **200** |
| `POST /api/Incidentes` | **401** | **403** | **201** |
| `DELETE /api/Incidentes/999` | **401** | **403** | **404** |
| `POST /api/Usuarios` | **401** | **403** | **201** |

**Como ler.** A coluna "sem token" é uniformemente 401: sem identidade, o
servidor não tem o que autorizar. A coluna do Operador separa leitura de
escrita — 200 nas três consultas, 403 nas três operações de escrita. A coluna
do Admin passa em tudo.

O **404** do Admin no `DELETE /api/Incidentes/999` é resultado positivo: o
incidente 999 não existe, e o 404 só pode ter vindo do controller. Ou seja, a
autorização liberou e o código rodou. Um 403 ali indicaria que o Admin nem
chegou ao método.

### Operador tentando abrir incidente — requisição e resposta completas

```bash
curl -i -X POST http://localhost:5041/api/Incidentes -H "Authorization: Bearer <token do Operador>" -H "Content-Type: application/json" -d "{\"dataHora\":\"2026-09-24T10:00:00\",\"tipo\":0,\"severidade\":2,\"bairroId\":1,\"orgaoResponsavel\":\"Teste\",\"situacao\":\"Em analise\"}"
```

Token usado (assinatura truncada), com `"role":"Operador"` no corpo:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIyIiwiZW1haWwiOiJqb2FvQHRlc3RlLmNvbSIsInJvbGUiOiJPcGVyYWRvciIsImV4cCI6MTc5MDI2NDU5MywiaXNzIjoicGFpbmVsLW1vYmlsaWRhZGUiLCJhdWQiOiJwYWluZWwtbW9iaWxpZGFkZSJ9.<truncada>
```

**Resposta:**
```
HTTP/1.1 403 Forbidden
Content-Length: 0
Server: Kestrel
```

Corpo vazio, e isso é deliberado do ASP.NET: o 403 não explica qual papel
seria necessário. Informar "este endpoint exige Admin" diria ao atacante
exatamente o que ele precisa obter.

O incidente **não** foi criado — o método `Criar` do controller nunca executou,
porque o middleware de autorização interrompeu a requisição antes.

### Bug encontrado: `RoleClaimType` sozinho não bastou

Com `RoleClaimType = "role"` configurado e um token de Admin legítimo, o
`DELETE` respondia **403**:

```bash
curl -i -X DELETE http://localhost:5041/api/Incidentes/999 -H "Authorization: Bearer <token do Admin>"
```

```
HTTP/1.1 403 Forbidden
Content-Length: 0
```

O token estava correto — corpo com `"sub":"5"`, `"email":"admin@mobilidade.local"`,
`"role":"Admin"` — e a assinatura era válida, senão a resposta teria sido 401.

**A causa.** O handler do JWT Bearer, por padrão, traduz nomes curtos de claim
para as URIs longas da Microsoft ao construir a identidade
(`MapInboundClaims = true` é o padrão). Então o `role` do token virava
`http://schemas.microsoft.com/ws/2008/06/identity/claims/role` na identidade, e
o `RoleClaimType = "role"` mandava o `[Authorize]` procurar por uma claim
`role` que já tinha sido renomeada. Nenhum papel encontrado, logo 403.

A decisão #33 previu que trocar `ClaimTypes.Role` pelo nome curto exigiria
`RoleClaimType = "role"` — o que não estava previsto é que essa linha sozinha
não resolve, porque o mapeamento acontece **antes** dela.

**A correção**, uma linha:

```csharp
options.MapInboundClaims = false;
```

Com ela, o nome da claim chega como está no token, e o `RoleClaimType` encontra.
Depois de reiniciar a API, o mesmo `DELETE` respondeu **404** — autorização
liberada, controller executado, incidente inexistente.

### Por quê

- **403 com corpo vazio, não com mensagem explicativa:** dizer qual papel falta
  é dar ao atacante o mapa do que precisa obter. O 403 confirma que o token é
  válido — isso já é informação suficiente para quem tem direito de estar ali.
- **`FallbackPolicy` em vez de `[Authorize]` no controller inteiro:** um
  `[Authorize]` na classe protegeria os métodos existentes, mas não um
  controller novo criado depois. A política de fallback é estrutural: vale para
  tudo que não declarar o contrário, inclusive minimal APIs.
- **`[AllowAnonymous]` só no login e no health:** o login não pode exigir token,
  porque é ele que emite tokens; o health precisa responder a um orquestrador
  que não tem credencial. Qualquer outro endpoint liberado seria decisão a
  justificar, não padrão.
- **Atributos por método, não por controller:** o `IncidentesController` tem
  leitura e escrita no mesmo arquivo, com papéis diferentes. Um atributo na
  classe forçaria o menor denominador comum, e as escritas ficariam abertas
  para Operador.
- **`ClockSkew = TimeSpan.Zero`:** a tolerância padrão de 5 minutos existe para
  compensar relógios dessincronizados entre servidores. Aqui há um servidor só,
  e a tolerância só serviria para tornar a expiração imprecisa — e o ataque de
  token expirado, inconclusivo.

### Pendente

- **Testes de integração para as 9 células que retornam 401 ou 403.** O projeto
  não tem infraestrutura de teste de integração HTTP (`WebApplicationFactory`),
  limitação já registrada na decisão #23. A tabela acima é prova por requisição
  real, não automatizada.

  ## Semana 7: Dívida da Semana 6 — `RegistrarIncidenteAberto()` sem guarda

### O que se quer provar

Na sabatina da Semana 6 ficou registrado que `RegistrarIncidenteAberto()` era
público e sem guarda: chamá-lo antes da persistência gravava um evento
`IncidenteAberto` com `IncidenteId = 0`, sem erro nenhum. Isso contradizia a
decisão #4, que fechou o construtor de `Incidente` justamente para tornar a
invariante estrutural em vez de comportamental.

A guarda escolhida (decisão #36) verifica o `Id` antes de construir o evento.
O ciclo abaixo prova que o teste detecta a **ausência** da guarda, e não passa
por acaso.

### A guarda

```csharp
public void RegistrarIncidenteAberto()
{
    if (Id == 0)
    {
        throw new DomainException("O incidente precisa ter um ID válido antes de gerar o evento.");
    }

    var incidenteAberto = new IncidenteAberto(Id, Severidade, Periodo.Inicio);
    _eventos.Add(incidenteAberto);
}
```

A verificação vem **antes** do `new IncidenteAberto(...)`. Um evento com
`IncidenteId = 0` não chega a existir nem em memória.

### O teste

```csharp
[Fact]
public void RegistrarIncidenteAberto_DeveLancarExcecao_QuandoIdForZero()
{
    var incidente = Incidente.Abrir(DateTime.UtcNow, Severidade.Alta, 1, "teste", "Defesa Civil", "Em analise");

    Assert.Throws<DomainException>(() => incidente.RegistrarIncidenteAberto());
}
```

O cenário não precisa ser forçado: `Incidente.Abrir` cria o agregado em
memória, sem passar pelo banco, então o `Id` nasce `0` — exatamente a condição
que a guarda existe para barrar.

### Comando

```bash
dotnet test --filter IncidenteTests
```

### 1. Com a guarda ativa — passa

```
[xUnit.net 00:00:00.96]   Starting:    Mobilidade.Tests
[xUnit.net 00:00:01.03]   Finished:    Mobilidade.Tests
  Mobilidade.Tests teste net10.0 êxito (1,9s)

Resumo do teste: total: 12; falhou: 0; bem-sucedido: 12; ignorado: 0; duração: 1,9s
```

### 2. Com o `if` e o `throw` comentados — falha

Trecho comentado em `Incidente.cs`:

```csharp
/*if (Id == 0) 
{
    throw new DomainException("O incidente precisa ter um ID válido antes de gerar o evento.");
}*/
```

Saída:

```
[xUnit.net 00:00:00.29]     Mobilidade.Tests.IncidenteTests.RegistrarIncidenteAberto_DeveLancarExcecao_QuandoIdForZero [FAIL]
[xUnit.net 00:00:00.29]       Assert.Throws() Failure: No exception was thrown
[xUnit.net 00:00:00.29]       Expected: typeof(Mobilidade.Domain.Excecoes.DomainException)
[xUnit.net 00:00:00.29]       Stack Trace:
[xUnit.net 00:00:00.29]         IncidenteTests.cs(121,0): at Mobilidade.Tests.IncidenteTests.RegistrarIncidenteAberto_DeveLancarExcecao_QuandoIdForZero()

Resumo do teste: total: 12; falhou: 1; bem-sucedido: 11; ignorado: 0; duração: 1,2s
```

**A leitura que importa.** Falhou **um** teste, e foi exatamente o da guarda.
Os outros 11 continuaram passando — isso confirma que o teste mede essa
invariante específica, e não um efeito colateral de outra regra.

Sem a guarda, o evento foi criado normalmente com `IncidenteId = 0`: nenhuma
exceção, nenhum aviso, e um registro de auditoria apontando para um incidente
que não existe no banco.

### 3. Com a guarda restaurada — passa de novo

```
[xUnit.net 00:00:00.17]   Starting:    Mobilidade.Tests
[xUnit.net 00:00:00.26]   Finished:    Mobilidade.Tests
  Mobilidade.Tests teste net10.0 êxito (1,2s)

Resumo do teste: total: 12; falhou: 0; bem-sucedido: 12; ignorado: 0; duração: 1,2s
```

> Saídas cortadas: removidos os avisos de build (NU1903, CS8618) e a lista de
> projetos compilados, que se repetem nas três execuções.

### Tentativa descartada no caminho: comentar a guarda errada

Na primeira tentativa do ciclo, o trecho comentado foi o do `Encerrar()`, não o
do `RegistrarIncidenteAberto()`. O resultado:

```
Mobilidade.Tests.IncidenteTests.Encerrar_QuandoIncidenteJaEstiverEncerrado_LancaDomainException [FAIL]
Mobilidade.Tests.IncidenteTests.Encerrar_QuandoJaEstaEncerrado_NaoAdicionaSegundoEventoNaLista [FAIL]

Resumo do teste: total: 12; falhou: 2; bem-sucedido: 10
```

Dois testes falharam, e **nenhum deles** era o da guarda em questão — o
`RegistrarIncidenteAberto_DeveLancarExcecao_QuandoIdForZero` continuou passando,
porque a guarda dele não tinha sido tocada.

Vale registrar porque mostra o que o ciclo de fato verifica: não basta que
*algum* teste falhe ao comentar *alguma* regra. O que prova a ligação entre
guarda e teste é **qual** teste falha.

### Por quê

- **A guarda antes da construção do evento, não depois:** verificar o `Id`
  depois de `new IncidenteAberto(...)` deixaria o evento inválido existir em
  memória, ainda que por um instante. A decisão #5 rejeitou esse padrão no
  `Severidade` — valor inválido que existe e depende de alguém corrigir não é
  invariante.
- **`DomainException`, não `InvalidOperationException`:** é violação de regra
  de domínio, e o `DomainExceptionFilter` já traduz para 422 (decisão #19).
- **O teste não força o `Id` para zero:** ele usa o estado natural de um
  agregado recém-criado. Um teste que atribuísse `Id = 0` artificialmente
  provaria menos, porque testaria um cenário construído em vez do cenário real
  de chamada na ordem errada.
- **A guarda melhora o modo de falha, não elimina a falha:** um caso de uso que
  esqueça de chamar `RegistrarIncidenteAberto()` continua compilando. O que
  mudou é que chamá-lo na hora errada agora falha alto, em vez de gravar um
  evento apontando para o incidente de Id 0 (decisão #36).