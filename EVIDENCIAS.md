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

## Invariante 2: Severidade obrigatória e válida na criação

### O que se quer provar

<uma frase sua: qual é a regra>

Regra protegida em `src/Mobilidade.Domain/Modelos/Incidente.cs`, dentro do
construtor (verificações de `NaoTriada` e de valor fora do enum).
Testes em `src/Mobilidade.Tests/IncidenteTests.cs`.

### Comando

```bash
dotnet test --filter IncidenteTests
```

### 1. Com a proteção ativa — passa
```
[xUnit.net 00:00:00.21] Starting: Mobilidade.Tests
[xUnit.net 00:00:01.60] Finished: Mobilidade.Tests
Mobilidade.Tests teste net10.0 êxito (2,6s)

Resumo do teste: total: 4; falhou: 0; bem-sucedido: 4; ignorado: 0; duração: 2,6s
```

### 2. Com as duas verificações comentadas — falha
```
[xUnit.net 00:00:00.85] Mobilidade.Tests.IncidenteTests.Criar_ComSeveridadeNaoTriada_LancaArgumentException [FAIL]
[xUnit.net 00:00:00.86] Assert.Throws() Failure: No exception was thrown
[xUnit.net 00:00:00.86] Expected: typeof(System.ArgumentException)

[xUnit.net 00:00:00.88] Mobilidade.Tests.IncidenteTests.Criar_ComSeveridadeForaDoEnum_LancaArgumentException [FAIL]
[xUnit.net 00:00:00.88] Assert.Throws() Failure: No exception was thrown
[xUnit.net 00:00:00.88] Expected: typeof(System.ArgumentException)

Resumo do teste: total: 4; falhou: 2; bem-sucedido: 2; ignorado: 0; duração: 3,1s
```


Essa falha prova que o Incidente não pode nascer sem um valor atribuido em Severidade.

### 3. Com a proteção restaurada — passa de novo
``` 
[xUnit.net 00:00:00.21] Starting: Mobilidade.Tests
[xUnit.net 00:00:01.60] Finished: Mobilidade.Tests
Mobilidade.Tests teste net10.0 êxito (2,6s)

Resumo do teste: total: 4; falhou: 0; bem-sucedido: 4; ignorado: 0; duração: 2,6s
```

> Saídas cortadas: removidos os avisos de build (NU1903, CS8618) e a lista
> de projetos compilados, que se repetem nas execuções.

### Por quê

O construtor de Incidente é oq define o nascimento dele e essas verficações e validações precisam está dentro dele já que o mesmo nescessita de severidades válidas para nascer.
NaoTriada é rejeitada por ser um 'Severidade = null' disfarçado com valor.
Valor fora do enum não passa, por ser lixo estrutural (Não é um valor de negócio e sim corrupção de dado).
