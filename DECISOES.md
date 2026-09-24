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

Não avaliei o risco técnico da integração com antecedência — a decisão foi
tomada sexta de manhã, sem tempo hábil até o prazo de entrega (18h) para
replicar com segurança a mesma cascata de correções que `Severidade`
exigiu na decisão #5.

Diante do prazo apertado e de outras atividades da semana em aberto,
priorizei garantir que o que já estava pronto (Severidade,
DeveriaEscalonar, testes) chegasse íntegro à entrega, em vez de arriscar
quebrar isso tentando encaixar Periodo de última hora.

**O que mudaria de ideia**

Com mais tempo disponível, integrar `Periodo` em `Incidente` (substituindo
`DataHora` e futuramente calculando duração real do incidente a partir de
`Encerrar()`) seria a evolução natural.

**Atualização (Semana 06): Dívida técnica 'Integrar Periodo'**

Dívida quitada. Periodo integrado tanto no domínio quanto no banco de
dados, conforme a issue #33. Ver decisão #10 para os detalhes da
integração.

**Evidência**

`Periodo.cs`, `PeriodoTests.cs` (igualdade, invariante de `Fim < Inicio`,
cálculo de `Duracao`).

---

## 7. Decisão: `BairroId` sem proteção de imutabilidade pós-criação

**O que foi decidido**

Decidido: manter BairroId com setter público, sem proteção contra
alteração pós-criação do Incidente, apesar de reconhecer que isso permite
reescrever o bairro de um incidente já registrado.

**Alternativa considerada**

Poderíamos criar uma validação onde BairroId nascesse a partir da criação
do Incidente sem poder ser alterado após o nascimento — "fechar setter +
criar método de domínio" com validação.

**Por que foi rejeitada**

Avaliei um risco de quebrar EF Core / migrations. Devido ao prazo apertado
de entrega, não deixou margem para expandir escopo por conta própria sem
risco de atrasar o que já estava pronto.

**O que mudaria de ideia**

Em outro cenário, dado início ou fim de semana "se possível", gostaria de
implementar essa ideia e as demais que ficaram vagas nesse arquivo.

**Atualização (Semana 6): dívida paga**

Setter fechado, `MudarBairro(int)` criado como único caminho de alteração.
Ver decisão #24.
---

## 8. Bairro.Nome permanece editável, sem proteção

**O que foi decidido**

Setter de Nome continua público — sem proteção contra
`bairro.Nome = "asdasdaw"`.

**Alternativa considerada**

Criar uma lista de bairros onde tivesse seus ids (exemplo: Centro = 1), e
que os nomes de Bairro pudessem ser chamados a partir dessa lista.

**Por que foi rejeitada**

As issues da Semana 05 não solicitavam catalogar bairros — o escopo pedido
era outro. Mesmo reconhecendo o valor de fechar esse buraco (mesma lógica
da decisão #5, aplicada a Bairro.Nome), o prazo apertado de entrega não
deixou margem para expandir escopo por conta própria sem risco de atrasar
o que já estava pronto.

**O que mudaria de ideia**

Quando surgir necessidade real de Bairro.Nome ser um catálogo — por
exemplo, onde exatamente ficaria registrado esse incidente (estado,
cidade, etc.).

**Atualização (Semana 6): dívida já estava paga**

Verificado por busca no código: `Nome` e `Id` já têm `private set`. Ver
decisão #25.

---

## 9. Escalonamento não considera `NaoTriada`

**O que foi decidido**

`DeveriaEscalonar` não irá atingir severidades `NaoTriada`.

**Alternativa considerada**

Incidentes com Severidade = NaoTriada deveriam escalonar, por não terem
uma severidade válida atribuída ainda.

**Justificativa (para a alternativa)**

Como NaoTriada é um incidente sem severidade, pode ocorrer casos em que o
usuário se esqueceu de marcar uma severidade, e pensando no pior caso
poderia ser um incidente que mereceria severidade Alta.

**Por que foi rejeitada**

Ausência de triagem é, ela mesma, um risco operacional — um incidente sem
severidade definida pode ser grave (ex: erro na criação que deveria ter
sido marcado como Alta) e ficar invisível indefinidamente se não
escalonar.

Porém, como já mencionado neste arquivo, incidentes não podem mais nascer
sem uma severidade válida definida — o escalonamento serviria apenas para
os dados legados no banco que são tachados como não triados. Decidido que
isso deveria ser um serviço mais operacional/administrativo do que de
desenvolvimento.

**Status**

[Fechado]

---

## 10. Integração do `Periodo` cobrindo o lugar de `DataHora`

**O que foi decidido**

`Periodo` é integrado ao `Incidente`, substituindo `DataHora` (o momento
de abertura) e `Encerrado` (o bool de estado). `Periodo` passa a ser a
única fonte de verdade sobre o tempo de um incidente: `Inicio` é fixado na
abertura, e `Fim` (inicialmente `null`) é preenchido quando `Encerrar()`
é chamado — substituindo o `Periodo` inteiro por um novo, já que o VO é
imutável.

**Alternativa considerada**

Manter `DataHora` e `Encerrado` como estavam, sem integrar `Periodo`.

**Por que foi rejeitada**

Manter os dois métodos anteriores poderia duplicar o estado de
"Encerrado" do Incidente, além de dessincronizar o estado — alguém
atualiza um e esquece o outro. Isso acabaria gerando um certo risco para
os incidentes (dado inconsistente entre `Encerrado` e a informação real de
quando o incidente terminou).

**O que mudaria de ideia**

No caso de Incidente precisar de múltiplos registros além de
aberto/fechado — ex: abriu incidente, fechou, reabriu de novo, fechou
novamente — um único `Periodo` não seria suficiente; seria necessária uma
coleção de períodos.

**Evidência**

`Incidente.cs` (Periodo substitui DataHora/Encerrado). Teste
`Abrir_DeveTerPeriodoComInicioIgualDataHora` em `IncidenteTests.cs`.
Migration `IntegraPeriodoNoIncidente`. `EVIDENCIAS.md`, seção "Semana 6:
Integração de Periodo no Incidente".

---

## 11. Mapeamento de `Periodo` via `OwnsOne`, não `Property()`

**O que foi decidido**

`Periodo` é mapeado via `OwnsOne` no EF Core, com `Inicio` obrigatório e
`Fim` opcional.

**Alternativa considerada**

Mapear `Periodo` diretamente com `Property()`, como se fosse um valor
escalar simples (igual `DataHora` era antes).

**Por que foi rejeitada**

`Property()` compila normalmente — o C# não sabe, em tempo de compilação,
que o tipo é inadequado para o provider. Mas falha em runtime: ao rodar
`dotnet ef migrations add`, o EF Core devolveu erro dizendo que o tipo
`Periodo` não pode ser mapeado, porque o provider do banco não suporta
esse tipo diretamente (Periodo tem dois campos internos, `Inicio` e
`Fim`, não é um valor escalar único).

**O que mudaria de ideia**

Enquanto `Periodo` continuar sendo um Value Object com mais de um campo
(Inicio, Fim), `Property()` nunca vai funcionar — essa decisão é
praticamente definitiva. Só mudaria se `Periodo` fosse simplificado para
um único valor escalar, deixando de ser um VO composto.

**Evidência**

`IncidenteConfiguration.cs` (configuração real com `OwnsOne`). Comando
`dotnet ef migrations add` e a saída de erro completa da tentativa com
`Property()`, documentados no `EVIDENCIAS.md`, seção "Semana 6: Integração
de Periodo no Incidente" → "Tentativa rejeitada: mapear Periodo com
Property() direto".

---

## Sugestão de estrutura (referência, decisões 12+ ainda a formalizar)

Decisão: RegistrarAtendimento não valida "data não pode ser futura"
Alternativa considerada e rejeitada: validar no construtor de Atendimento,
como regra universal (mesmo padrão usado em Incidente.Abrir())
Por quê rejeitada: atendimento pode representar agendamento futuro (ex:
equipe programada pra atuar no dia seguinte); bloquear isso restringiria
um cenário legítimo do negócio
Consequência: diferente de Incidente, que não pode nascer no futuro,
Atendimento pode.

## 12. Rotação de credencial exposta (senha do SQL Server local)

**O que foi decidido**

A senha do usuário `sa` do SQL Server local foi vazada publicamente no
Git (commitada em `EVIDENCIAS.md`, `appsettings.json` e como valor default
em `docker-compose.yml`). A senha foi trocada via `ALTER LOGIN`, sem
perda de dados. `docker-compose.yml` passou a usar um placeholder
não-funcional (`changeme`) como default, forçando a senha real a vir
exclusivamente do `.env` (já ignorado pelo Git).

**Alternativa considerada**

Deixar a senha antiga e só reescrever o histórico do Git para removê-la
dos commits.

**Por que foi rejeitada**

Reescrever histórico não invalida uma credencial que já pode ter sido
vista, clonada ou indexada por qualquer ferramenta — só troca visual,
não segurança real. A senha precisava parar de funcionar, não só parar
de aparecer.

**O que mudaria de ideia**

Nenhum cenário razoável — credencial vazada sempre deve ser trocada,
independente de reescrever histórico ou não.

**Pendências**

- ~~`appsettings.json`/`appsettings.Development.json` com a senha antiga~~
  — **resolvido na Semana 6**: connection string atualizada, aplicação
  voltou a conectar (ver `EVIDENCIAS.md`, seção "Semana 6: Handler de
  evento", onde o POST passou a gravar na tabela `Auditoria`).
- Histórico do Git ainda contém a senha antiga em commits passados — ela
  não dá mais acesso a nada, mas o rastro continua. **Em aberto.**

**Evidência**

Confirmado via `sqlcmd`: senha antiga rejeitada com "Login failed for
user 'sa'" após a troca; senha nova autentica normalmente e os 24
incidentes existentes permanecem intactos.

## 13. Onde morar Registro de Eventos?

**Oque foi decidido**

Criação da pasta Eventos dentro de Mobilidade.Domain, contendo os três
records de evento (IncidenteAberto, AtendimentoRegistrado,
IncidenteEncerrado).

**Alternativa considerada**
Colocar os records de evento (IncidenteAberto, AtendimentoRegistrado, IncidenteEncerrado) em Mobilidade.Application em vez de Mobilidade.Domain.

**Por que foi rejeitada**
Se IncidenteAberto reside na Application e `Incidente.cs` tentasse criar um IncidenteAberto, Domain estaria tentando usar uma camada que fica acima dele na hierarquia de dependencia - O que é proibido, pela razão que inverteria a seta.
(Domain que não depende de ninguém teria que passar a depender da Application)

**O que mudaria de ideia**
Mudaria de ideia se o evento precisasse carregar dado de infraestrutura,
(ex: informação de transporte HTTP, fila) dentro dele — nesse caso, deixaria de ser um fato puro de domínio

**Evidência**
```
dotnet build, saída completa: "Construir êxito(s) com 7 aviso(s) em 6,1s",
com Mobilidade.Domain, Application, Infrastructure, Tests e Api
compilando sem erro, eventos já referenciados em Incidente.cs.
```

## 14. Tempo de emissão de IncidenteAberto

**O que foi decidido**
IncidenteAberto não é emetido dentro de `Abrir()` e sim via metodo separado - `RegistrarIncidenteAberto()` (chamado depois de `Salvar()` )

**Alternativa considerada**
Salvar dentro de Abrir (sem passar Id de Incidente).
Criar o Id de Incidente via Guid (independente do banco).
Handler montar IncidenteAberto sozinho, direto do command, sem passar
pela lista _eventos do agregado.

**Por que foi rejeitada**
Salvar dentro de `Abrir()` exigiria que `Domain` chamasse o repositório,
mas a direção da dependência entre Domain e Infrastructure/repositório
seria violada (Domain não depende de nada, Infrastructure depende de
Domain — a seta não pode inverter). Mesmo que fosse possível, salvar o
incidente antes de criar o atendimento reabriria o problema que a
decisão #4 resolveu: se `new Atendimento(...)` falhasse depois do
incidente já salvo, ficaria um incidente no banco sem atendimento —
exatamente o cenário que o construtor privado existe pra impedir.

Trocar int por Guid é migration que mexe em toda FK, no Atendimento, na
Auditoria nova, nos DTOs, nos testes — custo desproporcional ao problema
pontual do momento de emissão de um único evento.

Se o handler montasse IncidenteAberto sozinho, usando os dados do
command (ex: cmd.Severidade) em vez dos dados do próprio agregado, o
handler passaria a decidir o que conta como fato de domínio — invertendo
quem é dono dessa responsabilidade. Isso é arriscado porque cmd.Severidade
não passa pela validação que o domínio aplica dentro de Abrir() (o
if (severidade == Severidade.NaoTriada) throw ...); um evento montado
direto do command poderia divergir do que o Incidente de fato validou e
guardou. Além disso, o handler precisaria conhecer campos que hoje são
privados dentro de Incidente, quebrando o encapsulamento que protege o
agregado.


**O que mudaria de ideia**
Se o projeto crescesse ao ponto do custo migrar para `Guid` deixar de ser desproporcional ou se surgisse uma nescesseidade real de gerar o Id sem depender do banco

**Evidência**

dotnet build (saída completa abaixo) — confirma que o método compila e
integra corretamente com os três eventos e o restante do domínio.

Saída do build: 
```                                                                                                                                                                                                                                                                                                            
Restaurar êxito(s) com 1 aviso(s) em 0,9s                                                                                                                                                                                                                                                                                   
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Api\Mobilidade.Api.csproj : warning NU1903: O pacote 'Microsoft.OpenApi' 2.0.0 tem uma alta vulnerabilidade de gravidade conhecida, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc                                                       
  Mobilidade.Domain net10.0 êxito(s) com 3 aviso(s) (0,8s) → src\Mobilidade.Domain\bin\Debug\net10.0\Mobilidade.Domain.dll                                                                                                                                                                                                  
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Domain\Entity\Atendimento.cs(14,12): warning CS8618: O propriedade não anulável 'Incidente' precisa conter um valor não nulo ao sair do construtor. Considere adicionar o modificador "obrigatório" ou declarar o propriedade como anulável.
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Domain\Entity\Incidente.cs(20,13): warning CS8618: O propriedade não anulável 'Periodo' precisa conter um valor não nulo ao sair do construtor. Considere adicionar o modificador "obrigatório" ou declarar o propriedade como anulável.    
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Domain\Entity\Incidente.cs(20,13): warning CS8618: O propriedade não anulável 'Severidade' precisa conter um valor não nulo ao sair do construtor. Considere adicionar o modificador "obrigatório" ou declarar o propriedade como anulável. 
  Mobilidade.Application net10.0 êxito(s) com 2 aviso(s) (0,5s) → src\Mobilidade.Application\bin\Debug\net10.0\Mobilidade.Application.dll                                                                                                                                                                                   
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Application\Consultas\RelatorioConsultas.cs(26,83): warning CS8602: Desreferência de uma referência possivelmente nula.                                                                                                                     
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Application\Consultas\RelatorioConsultas.cs(42,27): warning CS8602: Desreferência de uma referência possivelmente nula.                                                                                                                     
  Mobilidade.Infrastructure net10.0 êxito (1,7s) → src\Mobilidade.Infrastructure\bin\Debug\net10.0\Mobilidade.Infrastructure.dll                                                                                                                                                                                            
  Mobilidade.Tests net10.0 êxito (0,7s) → src\Mobilidade.Tests\bin\Debug\net10.0\Mobilidade.Tests.dll                                                                                                                                                                                                                       
  Mobilidade.Api net10.0 êxito(s) com 1 aviso(s) (2,1s) → src\Mobilidade.Api\bin\Debug\net10.0\Mobilidade.Api.dll                                                                                                                                                                                                           
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Api\Mobilidade.Api.csproj : warning NU1903: O pacote 'Microsoft.OpenApi' 2.0.0 tem uma alta vulnerabilidade de gravidade conhecida, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc                                                       
                                                                                                                                                                                                                                                                                                                            
Construir êxito(s) com 7 aviso(s) em 6,1s                                                                                                                                                                                                                                                                                   
```

**Atualização — teste de integração confirmando o comportamento em runtime:**

Teste: Executar_IncidenteValido_RegistraIncidenteAbertoComIdReal()
Local: IncidenteTests.cs

Saída do test:

```
λ dotnet test --filter Executar_IncidenteValido_RegistraIncidenteAbertoComIdReal
Restaurar êxito(s) com 1 aviso(s) em 0,7s
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Api\Mobilidade.Api.csproj : warning NU1903: O pacote 'Microsoft.OpenApi' 2.0.0 tem uma alta vulnerabilidade de gravidade conhecida, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
  Mobilidade.Domain net10.0 êxito (0,1s) → src\Mobilidade.Domain\bin\Debug\net10.0\Mobilidade.Domain.dll
  Mobilidade.Application net10.0 êxito (0,1s) → src\Mobilidade.Application\bin\Debug\net10.0\Mobilidade.Application.dll
  Mobilidade.Infrastructure net10.0 êxito (0,1s) → src\Mobilidade.Infrastructure\bin\Debug\net10.0\Mobilidade.Infrastructure.dll
  Mobilidade.Tests net10.0 êxito(s) com 1 aviso(s) (0,6s) → src\Mobilidade.Tests\bin\Debug\net10.0\Mobilidade.Tests.dll
    C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Tests\IncidenteTests.cs(76,9): warning xUnit2013: Do not use Assert.Equal() to check for collection size. Use Assert.Single instead. (https://xunit.net/xunit.analyzers/rules/xUnit2013)
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v3.1.4+50e68bbb8b (64-bit .NET 10.0.12)
[xUnit.net 00:00:00.83]   Discovering: Mobilidade.Tests
[xUnit.net 00:00:00.87]   Discovered:  Mobilidade.Tests
[xUnit.net 00:00:00.90]   Starting:    Mobilidade.Tests
[xUnit.net 00:00:02.04]   Finished:    Mobilidade.Tests
  Mobilidade.Tests teste net10.0 êxito (4,2s)

Resumo do teste: total: 1; falhou: 0; bem-sucedido: 1; ignorado: 0; duração: 4,2s
Construir êxito(s) com 2 aviso(s) em 6,0s
```

Confirma que RegistrarIncidenteAberto(), chamado após Criar(), grava o
evento IncidenteAberto com o IncidenteId igual ao Id real gerado — não
mais 0. Resolve a lacuna que esta decisão havia deixado pendente.

## 15. FK de Auditoria com Restrict, não Cascade

**O que foi decidido**
A FK de Auditoria para Incidente usa DeleteBehavior.Restrict. Na prática,
isso impede que um Incidente com registros de auditoria seja deletado.

**Alternativa considerada**
Usar Cascade, mesmo comportamento aplicado ao Atendimento — deletar o
Incidente deletaria junto seus registros de auditoria.

**Por que foi rejeitada**
Auditoria existe para registrar que um incidente grave foi aberto, e
para ser consultada depois. Com Cascade, deletar o incidente apagaria
esse registro junto — ninguém conseguiria mais saber que aquele
incidente existiu e foi auditado. Isso derrota o propósito da tabela.
Diferente do Atendimento, onde Cascade faz sentido: atendimento sem
incidente não significa nada.

**O que mudaria de ideia**
Se a tabela de Auditoria fosse um log temporário, sem propósito de
rastreabilidade permanente — nesse caso, deletar o registro junto com
o Incidente seria desejável, e Cascade faria sentido.

**Evidência**

SQL da migration gerada:
```
 Executed DbCommand (17ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT 1
info: Microsoft.EntityFrameworkCore.Migrations[20411]
      Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (18ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      DECLARE @result int;
      EXEC @result = sp_getapplock @Resource = '__EFMigrationsLock', @LockOwner = 'Session', @LockMode = 'Exclusive';
      SELECT @result
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (3ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
      BEGIN
          CREATE TABLE [__EFMigrationsHistory] (
              [MigrationId] nvarchar(150) NOT NULL,
              [ProductVersion] nvarchar(32) NOT NULL,
              CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
          );
      END;
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT 1
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT OBJECT_ID(N'[__EFMigrationsHistory]');
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (13ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT [MigrationId], [ProductVersion]
      FROM [__EFMigrationsHistory]
      ORDER BY [MigrationId];
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20260917193445_CriaTabelaAuditoria'.
Applying migration '20260917193445_CriaTabelaAuditoria'.
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (11ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      CREATE TABLE [Auditoria] (
          [Id] int NOT NULL IDENTITY,
          [IncidenteId] int NOT NULL,
          [Severidade] int NOT NULL,
          [Data] datetime2 NOT NULL,
          CONSTRAINT [PK_Auditoria] PRIMARY KEY ([Id]),
          CONSTRAINT [FK_Auditoria_Incidentes_IncidenteId] FOREIGN KEY ([IncidenteId]) REFERENCES [Incidentes] ([Id]) ON DELETE NO ACTION
      );
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (3ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      CREATE INDEX [IX_Auditoria_IncidenteId] ON [Auditoria] ([IncidenteId]);
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (12ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
      VALUES (N'20260917193445_CriaTabelaAuditoria', N'10.0.11');
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (2ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      DECLARE @result int;
      EXEC @result = sp_releaseapplock @Resource = '__EFMigrationsLock', @LockOwner = 'Session';
      SELECT @result
Done.
```
**LINHA RELEVANTE**

'CONSTRAINT [FK_Auditoria_Incidentes_IncidenteId] FOREIGN KEY ([IncidenteId]) REFERENCES [Incidentes] ([Id]) ON DELETE NO ACTION'


Saída dotnet ef database aplicada no banco:
```
λ dotnet ef migrations add CriaTabelaAuditoria --project src/Mobilidade.Infrastructure --startup-project src/Mobilidade.Api
C:\Users\Dell\Trabalhos\MOBILIDADE\painel-mobilidade\backend\src\Mobilidade.Api\Mobilidade.Api.csproj : warning NU1903: O pacote 'Microsoft.OpenApi' 2.0.0 tem uma alta vulnerabilidade de gravidade conhecida, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
Build started...
Build succeeded.
The Entity Framework tools version '10.0.10' is older than that of the runtime '10.0.11'. Update the tools for the latest features and bug fixes. See https://aka.ms/AAc1fbw for more information.
Done. To undo this action, use 'ef migrations remove'
```
## 16. `Auditoria` sem propriedade de navegação para `Incidente`

**O que foi decidido**

A entidade `Auditoria` guarda `IncidenteId` como `int`, sem propriedade
`Incidente Incidente { get; set; }`. A FK existe no banco — configurada
explicitamente via `HasOne<Incidente>().WithMany()` em
`AuditoriaConfiguration` — mas não há navegação nenhuma no modelo, nos
dois sentidos: `Auditoria` não conhece `Incidente`, e `Incidente` não tem
coleção de auditorias.

A entidade também não expõe nenhum método de alteração: todas as
propriedades têm `private set`, e o único caminho de criação é a factory
`Auditoria.RegistrarAuditoria(...)`. O `IAuditoriaRepository` tem um
método só, `Criar` — sem `Atualizar`, sem `Remover`.

**Alternativa considerada**

Adicionar a navegação nos dois lados, como já existe entre `Incidente` e
`Atendimento` (`_atendimentos` no agregado, `Incidente` no `Atendimento`),
permitindo carregar as auditorias de um incidente com `Include`.

**Por que foi rejeitada**

`Atendimento` faz parte do agregado `Incidente` — a invariante 1 diz que
todo incidente nasce com um atendimento, então o agregado precisa
enxergá-los. `Auditoria` é o oposto: é o registro de que algo foi
observado sobre o incidente, escrito por um handler que o agregado não
sabe que existe. Dar navegação ao `Incidente` significaria que carregar
um incidente traria junto a auditoria dele, e que o agregado passaria a
ter uma coleção que nenhum método seu manipula — estado pendurado, sem
dono.

A ausência de `Atualizar`/`Remover` no repositório segue o mesmo
raciocínio da decisão #2 aplicada à auditoria: registro de auditoria que
pode ser alterado depois deixa de servir como auditoria.

**O que mudaria de ideia**

Se surgisse uma consulta real que precisasse listar as auditorias de um
incidente com frequência, a navegação (só do lado da `Auditoria`, nunca
do `Incidente`) passaria a valer o custo. Hoje não existe essa consulta —
a tabela é escrita e consultada diretamente, não navegada a partir do
agregado.

**Nota sobre o escopo da tabela**

`Auditoria` hoje tem apenas `IncidenteId`, `Severidade` e `Data` — não há
coluna de ação ou tipo de evento, porque existe um único handler
escrevendo nela, e ele só reage a `IncidenteAberto`. Se um segundo handler
passar a auditar outra transição (encerramento, por exemplo), a tabela
precisará de uma coluna que distinga os registros, e de uma migration
nova.

**Evidência**

`AuditoriaConfiguration.cs` (`HasOne<Incidente>().WithMany()`, sem
navegação declarada). Entidade `Auditoria.cs` (construtor privado, factory
estática, `private set` em todas as propriedades).
`IAuditoriaRepository.cs` (um único método). SQL da migration
`CriaTabelaAuditoria` na decisão #15, mostrando a FK criada sem que exista
navegação no modelo.

---

## 17. Despachador de eventos escrito à mão, sem MediatR

**O que foi decidido**

O despacho de eventos é feito por um `Despachador` próprio, em
`Mobilidade.Application/Eventos/`, com um `switch` sobre o tipo concreto
do evento e um método genérico privado que resolve os handlers pelo
`IServiceProvider`. O contrato dos handlers é
`IManipuladorDeEvento<TEvento>`, síncrono, restrito a
`TEvento : IEventoDeDominio`.

A interface marcadora `IEventoDeDominio` (vazia) vive em
`Mobilidade.Domain/Eventos/`, junto dos eventos; `IManipuladorDeEvento<T>`
e o `Despachador` vivem em `Mobilidade.Application`. Nenhuma biblioteca de
mediação foi adicionada — a única dependência nova é
`Microsoft.Extensions.DependencyInjection.Abstractions`, no `Application`.

**Alternativa considerada**

1. MediatR (ou biblioteca equivalente), que resolve handlers por reflection
   e já traz pipeline, notificações e registro automático por assembly.
2. Despachador próprio, mas resolvendo o tipo por reflection
   (`MakeGenericType` + `Invoke`), dispensando o `switch`.
3. Despachador recebendo os handlers por construtor
   (`IEnumerable<IManipuladorDeEvento<IncidenteAberto>>`, e assim por
   diante), sem tocar no `IServiceProvider`.

**Por que foi rejeitada**

MediatR resolve um problema de escala que este projeto não tem: são três
eventos e um handler. O `switch` inteiro tem vinte linhas e é legível de
cima a baixo — trazer uma dependência externa, com seu próprio ciclo de
versões e licenciamento, para substituir vinte linhas seria pagar caro por
pouco.

A reflection própria (alternativa 2) trocaria o `switch` por código que só
falha em runtime: um evento sem handler registrado, ou um erro de
montagem do tipo genérico, só apareceria com a aplicação rodando. O
`switch` faz o compilador verificar cada caso, e o `default` que lança
`InvalidOperationException` garante que um quarto evento criado sem entrada
no despachador estoure imediatamente, em vez de ser ignorado em silêncio.

A alternativa 3 seria a mais pura — `Application` não conheceria DI de
forma alguma — mas com três eventos já exigiria três parâmetros no
construtor, e cada evento novo mudaria a assinatura. O custo aceito em
troca é que o `Despachador` usa `IServiceProvider`, o que é service
locator; é um antipadrão reconhecido, e está concentrado numa única classe
cuja razão de existir é justamente resolver handlers cujo tipo só se
conhece a cada `case`.

**Sobre o handler ser síncrono**

`IManipuladorDeEvento<T>.Manipular` retorna `void`, não `Task`, porque
`IIncidenteRepository` e `IAuditoriaRepository` são síncronos — todo o
projeto é. Um handler `async` sem nada para aguardar geraria o aviso
CS1998 e `async` de enfeite. Se um handler futuro precisar de I/O real
(HTTP, fila), a interface muda para `Task` e os existentes acompanham.

**O que mudaria de ideia**

Se o número de eventos crescesse a ponto do `switch` virar manutenção
recorrente — na casa de uma dezena ou mais, com handlers de vários tipos
por evento — o custo de MediatR passaria a se justificar. O contrato
`IManipuladorDeEvento<T>` foi desenhado para que essa troca seja possível
sem mexer no agregado nem nos handlers: só o `Despachador` e o registro no
DI mudariam.

**Evidência**

`Despachador.cs`, `IDespachadorDeEventos.cs`, `IManipuladorDeEvento.cs`,
`IEventoDeDominio.cs`. Registro no `Program.cs`
(`AddScoped<IDespachadorDeEventos, Despachador>()` e
`AddScoped<IManipuladorDeEvento<IncidenteAberto>, RegistrarAuditoriaAoAbrirIncidenteGrave>()`).
`EVIDENCIAS.md`, seção "Semana 6: Handler de evento — auditoria de
incidente grave": ciclo comentar/descomentar do despacho, e a linha
gravada na tabela `Auditoria` pelo caminho real da API, provando que o
handler foi resolvido pelo container.

## 18. Value Object não atravessa a fronteira HTTP

**O que foi decidido**

`CriarIncidenteRequest` recebe `int Severidade`, não o Value Object
`Severidade`. A conversão acontece no controller, via `Severidade.DeInt`,
na fronteira entre HTTP e domínio.

**Alternativa considerada**

Manter `Severidade` (o VO) no DTO e anotar seu construtor privado com
`[JsonConstructor]`, ou escrever um `JsonConverter` customizado.

**Por que foi rejeitada**

`[JsonConstructor]` abriria o construtor privado que a decisão #5 fechou
de propósito — qualquer `valor` poderia ser desserializado, incluindo um
fora da lista fechada, furando o `DeInt`. O `JsonConverter` customizado
resolveria isso corretamente, mas resolve o sintoma: o DTO continuaria
acoplado ao tipo de domínio, e o contrato da API continuaria mudando
sempre que o VO mudasse.

Receber `int` deixa a fronteira explícita: o JSON carrega um número, o
domínio carrega um Value Object, e a conversão (com validação) acontece
num ponto único e visível.

**O que mudaria de ideia**

Se a API precisasse aceitar a severidade por nome (`"Alta"`) além do
número, o `TryParse` que já existe no `Severidade` cobriria isso — ainda
recebendo `string` no DTO, sem expor o VO.

**Evidência**

`EVIDENCIAS.md`, seção "Semana 6: Handler de evento" → "Bug encontrado no
caminho", com a `NotSupportedException` completa do
`System.Text.Json` e o antes/depois do DTO e do controller.

## 19. Exceção de domínio própria (`DomainException`), não exceções do .NET

**O que foi decidido**

As quatro invariantes do `Incidente` passam a lançar `DomainException`
(em `Mobilidade.Domain/Excecoes/`) em vez de `ArgumentException` e
`InvalidOperationException`. As mensagens foram reescritas na linguagem
do negócio — "Severidade Ausente" virou "Incidente não pode ser aberto
sem severidade definida", porque essa mensagem chega ao cliente da API
como o `detail` de um 422.

**Alternativa considerada**

Manter `ArgumentException` e `InvalidOperationException` e fazer o filtro
da API traduzir esses dois tipos para 422.

**Por que foi rejeitada**

`InvalidOperationException` é lançada por dezenas de pontos da BCL —
`First()` numa coleção vazia, LINQ sobre sequência inválida, `DbContext`
em estado inconsistente. Um filtro que traduzisse esse tipo para 422
transformaria qualquer bug de programação em "regra de negócio violada",
escondendo erro real atrás de uma resposta que diz ao cliente que ele
errou. Um tipo próprio significa que só o que o domínio declarou como
regra chega como 422; qualquer outra coisa continua sendo 500, que é o
que de fato é.

**O que mudaria de ideia**

Se o domínio precisasse distinguir categorias de violação (conflito de
estado versus dado inválido, por exemplo) com status HTTP diferentes,
uma única `DomainException` não bastaria — seria uma hierarquia, com o
filtro mapeando cada subtipo.

**Evidência**

`Incidente.cs` (quatro `throw new DomainException`). Testes
`Criar_ComDataHoraFutura_LancaDomainException`,
`Criar_ComSeveridadeNaoTriada_LancaDomainException`,
`Encerrar_QuandoIncidenteJaEstiverEncerrado_LancaDomainException`,
`RegistrarAtendimento_QuandoIncidenteEstaEncerrado_LancaDomainException`
em `IncidenteTests.cs`. `EVIDENCIAS.md`, seção "Semana 6: Validação em
duas camadas".

---

## 20. Tradução de exceção em filtro, não em `try/catch` no controller

**O que foi decidido**

Um `DomainExceptionFilter` (`IExceptionFilter`, registrado globalmente em
`AddControllers`) traduz `DomainException` para 422 e
`KeyNotFoundException` para 404, ambos como `ProblemDetails`. Os
`try/catch` que existiam em `Encerrar` e `RegistrarAtendimento` no
controller foram removidos.

**Consequência: mudança de contrato.** Encerrar um incidente já encerrado
respondia **409 Conflict** e agora responde **422**. É breaking change
para qualquer cliente existente. A missão pede 422 para regra de negócio
violada, e o 409 anterior tratava a violação como conflito de
concorrência, que não é o caso — o incidente não está sendo disputado por
duas requisições, ele está num estado que não permite a operação.

**Alternativa considerada**

Manter o `try/catch` em cada método do controller, trocando apenas o tipo
capturado e o status devolvido.

**Por que foi rejeitada**

O `try/catch` por método é uma regra que depende de disciplina: todo
endpoint novo precisa lembrar de capturar. Isso já tinha falhado —
depois da decisão #19, os `catch (InvalidOperationException)` viraram
código morto silenciosamente, porque o domínio passou a lançar outro
tipo, e o resultado era 500 em vez de 409. O filtro global não tem esse
modo de falha: qualquer endpoint, existente ou futuro, é coberto sem
precisar lembrar de nada.

**O que mudaria de ideia**

Se um endpoint específico precisasse responder algo diferente para a
mesma exceção, o filtro global deixaria de bastar — mas o caso certo aí
seria um filtro por ação, não voltar ao `try/catch`.

**Evidência**

`DomainExceptionFilter.cs`. Registro em `Program.cs`
(`options.Filters.Add<DomainExceptionFilter>()`). Testes
`OnException_ComDomainException_Retorna422ComMensagemDoDominio`,
`OnException_ComKeyNotFound_Retorna404` e
`OnException_ComExcecaoDesconhecida_NaoTrata` em
`DomainExceptionFilterTests.cs`. Requisição 422 real no `EVIDENCIAS.md`.

---

## 21. `[Range(0, 3)]` na severidade aceita `NaoTriada` de propósito

**O que foi decidido**

A borda aceita severidade de 0 a 3, incluindo `NaoTriada` (valor 3), e
deixa o domínio rejeitá-la com 422.

**Alternativa considerada**

`[Range(0, 2)]`, barrando `NaoTriada` já na borda com 400.

**Por que foi rejeitada**

A pergunta que separa as camadas é quem é dono da regra. A borda sabe que
`3` é um valor válido de `Severidade` — `Severidade.DeInt(3)` devolve
`NaoTriada` sem erro, e o banco tem registros legados com esse valor. O
que a borda não sabe é que um incidente **novo** não pode nascer sem
triagem; isso é a decisão #1, e vive no domínio. Barrar na borda
duplicaria a regra em dois lugares: se amanhã a triagem virar etapa do
fluxo e `NaoTriada` passar a ser aceita na criação, seria preciso mudar o
`Incidente` e lembrar de mudar o DTO junto.

**O que mudaria de ideia**

Se `NaoTriada` deixasse de existir como valor de `Severidade` — sendo
substituída por ausência real (`null`) — o range da borda passaria a ser
0 a 2 por definição do tipo, não por escolha.

**Evidência**

Teste `CriarIncidenteRequest_Severidade_AceitaNaoTriadaParaQueODominioDecida`
em `ValidacaoDeBordaTests.cs`. Requisição com `"severidade": 3` no
`EVIDENCIAS.md`, que passa a borda e recebe 422 do domínio.

---

## 22. Anotações de validação no parâmetro do record, não na propriedade

**O que foi decidido**

As anotações de `CriarIncidenteRequest` ficam nos parâmetros do construtor
primário (`[Required] DateTime? DataHora`), sem o prefixo `property:`.

**Alternativa considerada**

Usar `[property: Required]`, que direciona o atributo para a propriedade
gerada pelo compilador em vez do parâmetro.

**Por que foi rejeitada**

Não é preferência: o ASP.NET Core recusa. Com `[property:]`, toda
requisição ao endpoint devolve 500 com
`InvalidOperationException: Record type '...' has validation metadata
defined on property 'Situacao' that will be ignored. 'Situacao' is a
parameter in the record primary constructor and validation metadata must
be associated with the constructor parameter.`

A alternativa foi tentada porque `Validator.TryValidateObject` — usado
num primeiro esboço do teste de borda — só enxerga atributos em
propriedades, e devolvia coleção vazia de erros com as anotações no
parâmetro. As duas exigências são incompatíveis, e o comportamento HTTP
real tem precedência sobre a conveniência do teste.

**Consequência para o teste**

O teste de borda passou a ler os atributos por reflection sobre os
parâmetros do construtor, verificando que as anotações estão declaradas e
que os ranges aceitam ou rejeitam os valores certos. Isso verifica a
configuração da validação, não a validação executando — a prova de que o
400 acontece de fato são as requisições HTTP no `EVIDENCIAS.md`.

**O que mudaria de ideia**

Se o DTO deixasse de ser record posicional e virasse classe com
propriedades, as anotações iriam naturalmente para as propriedades, o
`Validator` funcionaria e o teste poderia exercitar a validação de
verdade.

**Evidência**

Saída completa do 500 com `[property:]` e do 400 correto sem ele, lado a
lado, no `EVIDENCIAS.md`, seção "Semana 6: Validação em duas camadas" →
"Tentativa rejeitada".

---

## 23. Testes de validação sobre as peças, não sobre a API em execução

**O que foi decidido**

A validação em duas camadas é testada por unidade: o `DomainExceptionFilter`
recebe um `ExceptionContext` montado à mão e devolve o status esperado; o
DTO tem suas anotações verificadas por reflection. O comportamento ponta a
ponta (400, 422, 201) é provado pelas três requisições HTTP reais coladas
no `EVIDENCIAS.md`.

**Alternativa considerada**

`WebApplicationFactory<Program>` (pacote `Microsoft.AspNetCore.Mvc.Testing`),
subindo a aplicação inteira em memória e fazendo as três requisições dentro
do teste.

**Por que foi rejeitada**

Essa é uma decisão de escopo e tempo, e é escrita como tal — não há
argumento técnico que torne a alternativa pior. `WebApplicationFactory`
provaria mais: o pipeline inteiro, incluindo o `[ApiController]` gerando
o 400, que os testes atuais não exercitam. O custo era montar a
infraestrutura de teste de integração no mesmo dia da entrega, com o
agravante conhecido de que esses testes escrevem no banco real sem
cleanup — limitação já registrada na Semana 4 e ainda não resolvida.

**O que fica em aberto**

O 400 não tem teste que o produza; ele é provado apenas pela requisição
HTTP no `EVIDENCIAS.md`. Essa é a lacuna real desta decisão, e some
quando a infraestrutura de teste de integração existir.

**O que mudaria de ideia**

Assim que houver base de teste de integração com banco isolado e cleanup
automático, os três cenários devem migrar para lá — é onde a prova fica
completa.

**Evidência**

`ValidacaoDeBordaTests.cs` (7 casos), `DomainExceptionFilterTests.cs`
(3 casos). `EVIDENCIAS.md` com as três requisições e respostas completas.

## 24. `BairroId` fechado, com `MudarBairro` como único caminho

**O que foi decidido**

`Incidente.BairroId` passa a ter `private set`. A alteração acontece por
`MudarBairro(int)`, que recusa a operação se o incidente estiver encerrado:

```csharp
public void MudarBairro(int bairroId)
{
    if (!Periodo.EstaEmAberto)
        throw new DomainException("Incidente encerrado não pode ter o bairro alterado.");

    BairroId = bairroId;
}
```

Isto paga a dívida da decisão #7, que havia deixado o setter público por
prazo.

**Alternativa considerada**

Fechar o setter sem método de substituição, removendo `BairroId` do
`AtualizarIncidenteRequest` — tratando o bairro como fato consumado, do
mesmo modo que a decisão #2 fez com `DataHora` e `Severidade`.

**Por que foi rejeitada**

Bairro digitado errado no cadastro é cenário real e corrigível, diferente
de `DataHora` e `Severidade`: corrigir o bairro não reescreve o que
aconteceu, corrige onde o sistema diz que aconteceu. O que a decisão #2
protege é a impossibilidade de reescrever o fato sem rastro — e aqui o
rastro é o próprio método nomeado, que só funciona enquanto o incidente
está aberto. Incidente encerrado volta a ser imutável nesse campo.

**O que mudaria de ideia**

Se a correção de bairro precisasse ser auditável (quem mudou, quando, de
qual bairro para qual), `MudarBairro` passaria a emitir um evento de
domínio — `BairroCorrigido` — em vez de só trocar o valor.

**Consequência encontrada ao aplicar**

Fechar o setter quebrou o build em dois lugares que ninguém tinha notado:
`RepositorioIncidentesEfCore.Atualizar` e `RepositorioIncidentesEmMemoria.Atualizar`
atribuíam `existente.BairroId = incidente.BairroId` diretamente. Os dois
repositórios escreviam no agregado sem passar por método nenhum — exatamente
o buraco que a decisão #7 tinha deixado aberto, e que só apareceu quando o
compilador passou a proibir.

**Evidência**

Erro de compilação `CS0200: A propriedade ou o indexador "Incidente.BairroId"
não pode ser atribuído, pois é somente leitura`, apontando as duas linhas,
no `EVIDENCIAS.md`. Testes `MudarBairro_IncidenteAberto_AlteraOBairro` e
`MudarBairro_IncidenteEncerrado_LancaDomainException` em `IncidenteTests.cs`.

---

## 25. Dívida da decisão #8 (`Bairro.Nome`) já estava paga

**O que foi decidido**

Nada a fazer: `Bairro.Nome` já tem `private set`, e `Bairro.Id` também. O
construtor exige os dois, e não há setter público em nenhum dos campos.

A decisão #8 registrava esse buraco como aberto por prazo na Semana 5. A
correção aconteceu em algum momento entre a Semana 5 e a Semana 6 — muito
provavelmente durante a integração do `Periodo` (decisão #10), que reescreveu
partes do domínio — sem ter sido registrada como pagamento da dívida.

**Alternativa considerada**

Nenhuma. O item foi verificado, não decidido.

**Por que isso vale registro**

Dívida paga sem registro é indistinguível de dívida esquecida: a decisão #8
continuava dizendo que o setter era público, e essa era a informação
disponível para quem lesse o repositório. A verificação foi feita por busca
direta no código, não por memória.

**Evidência**

```bash
grep -rn "Nome" --include=*.cs backend/src/Mobilidade.Domain/
```
```
backend/src/Mobilidade.Domain/Entity/Bairro.cs:6: public string Nome { get; private set; }
backend/src/Mobilidade.Domain/Entity/Bairro.cs:11: Nome = nome;
backend/src/Mobilidade.Domain/ValueObjects/Severidade.cs:11: public string Nome { get; }
backend/src/Mobilidade.Domain/ValueObjects/Severidade.cs:16: Nome = nome;
backend/src/Mobilidade.Domain/ValueObjects/Severidade.cs:33: public override string ToString() => Nome;
```

As duas ocorrências em `Bairro.cs` são a declaração com `private set` e a
atribuição no construtor. Não há outra classe `Bairro` no domínio.

## 26. Incidentes legados sem atendimento: migração com atendimento sintético

**O que foi decidido**

A migration `AtendimentoSinteticoParaIncidentesLegados` cria um `Atendimento`
para cada `Incidente` que não tinha nenhum, marcado explicitamente como
sintético:

```sql
INSERT INTO Atendimento (IncidenteId, OrgaoResponsavel, DataHora, Situacao)
SELECT i.Id,
       'Importacao (dado legado)',
       i.Periodo_Inicio,
       'Sem registro de atendimento'
FROM Incidentes i
WHERE NOT EXISTS (SELECT 1 FROM Atendimento a WHERE a.IncidenteId = i.Id)
```

Seis incidentes foram corrigidos. O `Down` remove apenas os registros com
esse `OrgaoResponsavel` e essa `Situacao`, sem tocar em atendimento real.

Isto paga a dívida reconhecida na sabatina da Semana 5: incidentes que
violam a invariante 1 em repouso.

**Quem eram os seis**

Não são um conjunto arbitrário. O Id 2 é o registro legado que a migration
`SeveridadeObrigatoria` converteu de `Severidade = NULL` para `NaoTriada`.
Os Ids 13, 19, 21, 23 e 25 foram criados entre 08 e 09 de setembro, antes
de o construtor de `Incidente` ser fechado (decisão #4) — são exatamente o
cenário que aquela decisão passou a tornar impossível. Ou seja, os órfãos
são o rastro de antes da invariante existir, não dado corrompido.

Um sétimo incidente (Id 59) também aparecia como órfão, mas era artefato de
teste inserido manualmente por `sqlcmd` durante a evidência da migration do
`Periodo`. Foi deletado antes da migração, e não recebeu atendimento
sintético — dar atendimento a um registro que nunca foi incidente de
negócio seria acumular ficção sobre ficção.

**Alternativas consideradas**

1. Estado de legado na entidade: `Incidente` ganharia noção de "veio de
   antes da invariante", e a regra de "todo incidente tem atendimento"
   passaria a ter exceção declarada para esses casos.
2. Documentação formal como dívida aceita: registrar os seis, não corrigir,
   e assumir que a invariante vale só para incidentes novos.

**Por que foram rejeitadas**

A alternativa 1 custa caro de forma permanente: a invariante deixaria de ser
"todo incidente tem atendimento" e passaria a ser "todo incidente tem
atendimento, exceto quando não tem" — e todo código que consulta atendimento
precisaria conhecer a exceção. Seis registros não justificam carregar essa
bifurcação no domínio para sempre.

A alternativa 2 é a que o prazo favorecia, e é por isso mesmo que foi
descartada: com seis registros e um `INSERT ... SELECT` de seis linhas
resolvendo, "dívida aceita" seria uma decisão de conveniência vestida de
decisão técnica. A invariante 1 é a base do agregado (decisão #4); deixar
dados que a violam em repouso significa que qualquer consulta futura que
assuma "todo incidente tem pelo menos um atendimento" quebra sem aviso.

**O que se perdeu ao escolher a migração**

O atendimento sintético não representa atendimento que aconteceu. Ele diz
"não há registro", o que é verdade, mas ocupa a mesma tabela dos
atendimentos reais. Qualquer relatório que conte atendimentos vai contar
esses seis. O `OrgaoResponsavel` explícito permite filtrá-los, mas isso
exige que quem escreve o relatório saiba que eles existem.

**Sobre a data escolhida**

`Periodo_Inicio` — o momento de abertura do próprio incidente. A migration
do `Periodo` (decisão #10) enfrentou escolha parecida e usou `GETUTCDATE()`,
porque lá não havia data honesta disponível. Aqui há: o atendimento
sintético não aconteceu em instante nenhum, então usá-lo com a data da
abertura evita inventar um momento que não existiu.

**O que mudaria de ideia**

Se a tabela `Atendimento` passasse a ter uma coluna que distinga origem
(real, sintético, importado), o filtro por `OrgaoResponsavel` viraria
gambiarra e a marcação correta seria essa coluna.

**Evidência**

`EVIDENCIAS.md`, seção "Semana 6: Incidentes legados sem atendimento", com
o `SELECT` dos seis órfãos antes, o SQL executado pela migration, e a
contagem zerada depois.

## 27. MediatR reavaliado para commands e queries, mantido fora por prazo

**O que foi decidido**

Commands e queries continuam sem biblioteca de mediação. Os commands são os
casos de uso existentes (`AbrirIncidente`, `RegistrarAtendimento`,
`EncerrarIncidente`), invocados diretamente pelo controller; as queries são
handlers próprios que recebem o `DbContext` por injeção.

**Por que esta decisão é separada da #17**

A #17 rejeitou MediatR para despacho de eventos de domínio. Este é outro uso
da mesma biblioteca, com outra conta: lá era um handler, aqui são seis. Citar
a #17 como resposta seria reaproveitar um argumento que não foi feito para
este caso.

**Alternativa considerada**

MediatR com `IRequest`/`IRequestHandler`, o controller injetando `ISender` em
vez dos casos de uso, e `IPipelineBehavior` para o que hoje está repetido.

**Por que foi rejeitada — e o que a favorecia**

A alternativa é mais forte aqui do que era na #17, e vale registrar o que ela
resolveria:

- Os três casos de uso repetem o mesmo bloco de despacho de eventos
  (`foreach` + `LimparEventos()`). Um `IPipelineBehavior` colocaria isso num
  lugar só, executado depois de todo command.
- Seis handlers já é escala onde o registro manual no `Program.cs` começa a
  incomodar.

Contra, do lado técnico: o projeto já tem um mecanismo de mediação próprio
para eventos (decisão #17). Adotar MediatR só para commands e queries deixaria
dois mecanismos convivendo, e unificar exigiria reescrever o despachador de
eventos também. MediatR passou a ter licença comercial a partir da v12 acima
de certo uso, o que é considerável num projeto que pode crescer.

Mas o motivo real da rejeição é prazo, e é escrito como tal: a decisão foi
tomada às 14h30 da sexta de entrega, com a Parte 2 inteira por fazer.
Converter seis handlers, trocar a injeção no controller e descobrir o que
quebra consumiria o tempo que a prova exigida pelo enunciado — a comparação
entre o SQL da query com projeção e o SQL de carregar o agregado — precisa.
Não há argumento técnico que torne a alternativa pior; há um relógio.

**O que mudaria de ideia**

Duas coisas, independentes:

1. Tempo. Sem o prazo de hoje, a adoção seria experimentada, medindo quanto
   do código atual o `IPipelineBehavior` de fato elimina.
2. Um terceiro uso de mediação aparecer (por exemplo, comandos vindos de fila
   ou de job agendado). Com três mecanismos diferentes, escrever o próprio
   deixa de compensar.

**Evidência**

Ausência do pacote em `Mobilidade.Application.csproj` e
`Mobilidade.Api.csproj`. Registro manual dos casos de uso e handlers em
`Program.cs`.

## 28. Handlers de query em `Infrastructure`, DTOs em `Application`

**O que foi decidido**

Os DTOs de leitura (`IncidenteResumoDto`, `IncidenteDetalheDto`) ficam em
`Mobilidade.Application/Consultas/`. Os handlers que os produzem
(`ListarIncidentesHandler`, `BuscarIncidentePorIdHandler`,
`IncidentesPorBairroHandler`) ficam em `Mobilidade.Infrastructure/Consultas/`,
recebendo `IncidentesDbContext` por injeção. O controller injeta os handlers
concretos, sem interface no meio.

**O problema que força a escolha**

`Mobilidade.Application` referencia apenas `Mobilidade.Domain` — é o que a
decisão #13 estabeleceu, e a direção da seta é a base do desenho de camadas.
Uma query precisa do `DbContext`, que vive em `Infrastructure`. Colocar o
handler em `Application` exigiria que `Application` passasse a referenciar
`Infrastructure`, invertendo essa seta.

**Alternativas consideradas**

1. `Application` referencia `Infrastructure`, e os handlers ficam junto dos
   DTOs.
2. Interface `IConsultaDeIncidentes` em `Application`, implementação em
   `Infrastructure` — mantendo a direção e dando ao controller um contrato
   abstrato.

**Por que foram rejeitadas**

A alternativa 1 quebra a única regra estrutural que o projeto sustenta desde a
Semana 5. Resolveria em um minuto e custaria o desenho inteiro.

A alternativa 2 é mais defensável, e foi a mais difícil de descartar. O que
pesou contra: a interface teria um método por query, cresceria a cada consulta
nova, e o controller passaria a depender de uma abstração que existe só para
esconder o `DbContext`. Isso é a definição de um repositório de leitura — e o
enunciado desta missão diz explicitamente que a query "não passa pelo
repositório de escrita, vai direto ao `DbContext`". Recriar um repositório com
outro nome contraria o que se queria demonstrar.

**O custo assumido**

`Mobilidade.Api` passa a depender de tipos concretos de `Infrastructure` para
leitura. Não é dependência nova — o `Program.cs` já registra
`RepositorioIncidentesEfCore` e `IncidentesDbContext` — mas é mais uso dela.

O que isso impede: trocar o EF Core por outra coisa sem tocar no controller.
Mas as queries *são* EF Core; o `Select` que vira SQL é o ponto do exercício.
E impede testar o controller sem banco — custo que hoje não se materializa,
porque a decisão #23 já registrou que não há testes de integração.

**Por que os DTOs ficam em `Application`**

Eles são o contrato de leitura que a camada de aplicação oferece, e não
dependem de nada de infraestrutura. Se um dia as queries mudarem de tecnologia,
os DTOs continuam valendo — é a parte estável.

Eles também são deliberadamente separados dos DTOs de entrada em `Api/Dtos/`:
aqueles são contratos HTTP com anotações de validação, que mudam quando o
cliente muda. Os de leitura mudam quando a tela precisa de outro campo. Ciclos
de mudança independentes.

**O que mudaria de ideia**

Se surgisse um segundo consumidor das queries além da API — um worker, um job
de relatório — a interface da alternativa 2 passaria a valer, porque aí o
contrato serviria a mais de um chamador em vez de existir só para satisfazer
uma regra de camadas.

**Evidência**

`Mobilidade.Application.csproj`, que referencia apenas `Mobilidade.Domain`.
Os três handlers em `Mobilidade.Infrastructure/Consultas/`, os DTOs em
`Mobilidade.Application/Consultas/`. `EVIDENCIAS.md`, seção "Semana 6: CQRS",
com o SQL gerado e as três queries respondendo.

## 29. `PasswordHasher` do ASP.NET Core Identity (PBKDF2), não SHA256

**O que foi decidido**

O hash de senha usa `PasswordHasher<object>` do ASP.NET Core Identity, que
aplica PBKDF2 com HMAC-SHA512, salt aleatório de 128 bits por senha e
100.000 iterações (padrão da v3 do formato). A implementação
`HasherDeSenhaPbkdf2` vive em `Mobilidade.Infrastructure/Seguranca/`,
atrás da interface `IHasherDeSenha`, declarada em `Mobilidade.Domain/Interfaces/`.

O `object` no genérico existe porque a API do Identity espera um tipo de
usuário como primeiro parâmetro de `HashPassword`, mas não o utiliza no
cálculo — o projeto não adota o Identity inteiro, só o algoritmo.

**Alternativa considerada**

1. `SHA256` ou `MD5`, disponíveis no `System.Security.Cryptography` sem
   pacote nenhum.
2. BCrypt, via `BCrypt.Net-Next`.
3. Argon2, vencedor do Password Hashing Competition.

**Por que foram rejeitadas**

SHA256 e MD5 são hashes de propósito geral, projetados para serem rápidos —
e velocidade é exatamente a propriedade errada aqui. Uma GPU comum calcula
bilhões de SHA256 por segundo, o que torna a força bruta sobre um banco
vazado viável em vez de proibitiva. Eles também não têm salt embutido: duas
contas com a mesma senha produziriam o mesmo hash, e tabelas pré-calculadas
(rainbow tables) resolveriam as senhas comuns sem nenhum cálculo novo.
Nenhuma das duas falhas se corrige "usando SHA256 com cuidado" — o
algoritmo não tem custo ajustável.

Argon2 é o mais forte dos três candidatos legítimos, por consumir memória
além de tempo, o que penaliza ataque com GPU de forma que PBKDF2 não
penaliza. Foi rejeitado por dependência: em .NET não há implementação de
primeira parte, e os pacotes disponíveis são mantidos por terceiros com
cadência irregular — numa semana cujo tema é segurança, adicionar
dependência menos auditada para a peça mais sensível do sistema é troca
ruim.

BCrypt é tecnicamente adequado e teria servido. O desempate foi
procedência: `Microsoft.Extensions.Identity.Core` vem do mesmo fornecedor
do runtime, já alinhado com a versão 10.0.11 usada no resto do projeto, e
não acrescenta cadeia de manutenção nova. BCrypt também tem o limite de 72
bytes na senha, que trunca em silêncio — não é problema hoje, mas é um
comportamento surpreendente a menos.

**O que mudaria de ideia**

Se surgisse implementação de Argon2id mantida pela Microsoft ou equivalente
em confiabilidade, a troca valeria: a interface `IHasherDeSenha` existe
justamente para que isso custe uma classe nova na `Infrastructure`, sem
tocar no `Usuario`. O formato do `PasswordHasher` também carrega a versão
dentro do próprio hash e devolve `SuccessRehashNeeded` quando o custo está
defasado, o que permite migrar senha por senha no login, sem obrigar todo
mundo a trocar de senha.

**Evidência**

`IHasherDeSenha.cs` (Domain), `HasherDeSenhaPbkdf2.cs` (Infrastructure).
Testes em `UsuarioTests.cs`:
`Registrar_MesmaSenhaEmDoisUsuarios_GeraHashesDiferentes` prova o salt
aleatório (mesma senha, mesmo hasher, hashes distintos);
`SenhaConfere_ComSenhaCerta_RetornaTrue` e
`SenhaConfere_ComSenhaErrada_RetornaFalse` provam a verificação nos dois
sentidos. Saída completa em `EVIDENCIAS.md`.

## 30. Chave JWT em variável de ambiente, não em `appsettings`

**O que foi decidido**

A chave de assinatura do JWT vem da variável de ambiente `Jwt__Chave` (duplo
underscore é como o ASP.NET mapeia a hierarquia `Jwt:Chave`). O código conhece
apenas o nome da configuração:

```csharp
var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Chave"]!));
```

`Jwt__Emissor` e `Jwt__Audiencia` seguem o mesmo caminho, por consistência,
ainda que não sejam segredo.

**Alternativa considerada**

1. `appsettings.Development.json`, que já está no `.gitignore` e já guarda a
   connection string local.
2. .NET User Secrets (`dotnet user-secrets`), que mantém o arquivo fora da
   pasta do projeto, no perfil do usuário.

**Por que foram rejeitadas**

A alternativa 1 é a mais prática, e quase foi escolhida — mas este repositório
já commitou esse arquivo por engano uma vez. A remoção do rastreamento
aconteceu em commit anterior e foi justamente o que quebrou o `dotnet ef`
nesta semana (ver `EVIDENCIAS.md`, seção da migration `CriaTabelaUsuarios`).
Um arquivo no `.gitignore` depende de o `.gitignore` estar correto e de
ninguém usar `git add -f`; uma variável de ambiente não tem como ser
commitada, porque não é arquivo.

User Secrets é a recomendação oficial da Microsoft para desenvolvimento e
seria tecnicamente superior à variável de ambiente para este caso. Foi
rejeitada por tempo: exigiria um passo de setup a mais na máquina e no README,
no penúltimo dia da semana. Registrado como melhoria.

**O custo assumido**

Variável de ambiente definida com `set` vale só para a janela do terminal onde
foi executada. Isso já custou um 500 durante o desenvolvimento: a API subiu numa
janela sem a variável e o `GetBytes` recebeu `null` (evidência completa no
`EVIDENCIAS.md`). Em produção a variável viria do orquestrador e o problema não
existiria, mas em desenvolvimento é uma fonte real de confusão.

**O que mudaria de ideia**

Adotar User Secrets em desenvolvimento, mantendo variável de ambiente em
produção — que é o desenho recomendado. Isso elimina o problema da janela do
terminal sem reintroduzir o risco de commit acidental.

**Evidência**

`GeradorDeTokenJwt.cs`, que só referencia nomes de configuração. `Program.cs`,
com o mesmo padrão em `TokenValidationParameters`. Ausência de qualquer chave
em arquivo versionado.

---

## 31. Expiração do token em 30 minutos

**O que foi decidido**

`expires: DateTime.UtcNow.AddMinutes(30)` no `GeradorDeTokenJwt`. Sem refresh
token: expirou, o usuário faz login de novo.

**Alternativa considerada**

15 minutos (o mínimo permitido pela issue) ou 60 (o máximo).

**Por que foram rejeitadas**

A conta é direta: um token roubado vale até expirar, e não há como revogá-lo
antes disso — é essa a natureza de um token sem estado, que o servidor valida
pela assinatura sem consultar nada. Menos tempo reduz a janela de dano; mais
tempo reduz o atrito para o usuário.

15 minutos seria a escolha se houvesse refresh token: o usuário não perceberia
as renovações. Sem ele, cada expiração é uma tela de login no meio do trabalho,
e um operador registrando atendimentos seria interrompido quatro vezes por
hora.

60 minutos dobra a janela de exposição sem ganho proporcional de conveniência —
quem vai trabalhar uma hora seguida provavelmente vai trabalhar duas.

30 é o meio do intervalo e a escolha defensável enquanto não há renovação.

**O que mudaria de ideia**

Implementar refresh token. Com ele, o token de acesso cairia para 15 minutos ou
menos, e o refresh (esse sim revogável, porque fica no banco) assumiria a
duração longa da sessão.

**Evidência**

`GeradorDeTokenJwt.Gerar`. Claim `exp` visível no token decodificado no
`EVIDENCIAS.md`, seção "Semana 7: Login e JWT". `ClockSkew = TimeSpan.Zero` no
`Program.cs` garante que os 30 minutos sejam exatos — o padrão do .NET tolera 5
minutos além do `exp`.

---

## 32. Login responde 401 idêntico para email inexistente e senha errada

**O que foi decidido**

`POST /api/auth/login` devolve o mesmo status e o mesmo corpo
(`{"mensagem":"Email ou senha inválidos."}`) em três situações distintas: email
malformado, email que não existe, e senha errada. A resposta não permite
distinguir qual delas ocorreu.

**Alternativa considerada**

Mensagens específicas — "Usuário não encontrado" contra "Senha inválida" —, que
é o que a maioria das interfaces faz e o que um usuário legítimo preferiria ao
digitar errado.

**Por que foi rejeitada**

Mensagem específica transforma o endpoint de login num oráculo de existência de
contas. Um atacante com uma lista de emails manda todos ao endpoint e separa,
pela resposta, quais estão cadastrados. Isso não quebra nenhuma senha, mas
entrega metade do trabalho: a partir daí ele sabe exatamente contra quais
contas vale a pena tentar força bruta, e quais endereços pertencem a usuários
daquele sistema — informação que tem valor próprio para phishing.

O custo é real e aceito: quem esqueceu qual email cadastrou não descobre pelo
login.

**Consequência: o `try/catch` no controller**

`Email.Criar` lança `DomainException` para email malformado, e o
`DomainExceptionFilter` (decisão #20) traduziria isso para 422 — um terceiro
comportamento observável, que diria ao atacante que aquele valor nem chegou a
ser consultado. Por isso o `AuthController` captura a exceção no ponto e
devolve o mesmo 401.

Isso contraria deliberadamente a decisão #20, que removeu `try/catch` dos
controllers. A diferença: o filtro global existe para **distinguir** tipos de
erro, e o login é o único endpoint que precisa **achatar** todos num único
status. Não é regressão, é requisito oposto.

Pelo mesmo motivo, `LoginRequest` não tem `[EmailAddress]` nem
`[StringLength(MinimumLength = 8)]`, que o `CriarUsuarioRequest` tem: as duas
gerariam 400 na borda para entradas malformadas, criando outra resposta
distinguível — e o `MinimumLength` revelaria a regra de tamanho de senha do
sistema.

**O que fica em aberto**

A resposta é idêntica em status e corpo, mas **não em tempo**. Email
inexistente retorna antes de qualquer cálculo de hash; senha errada só retorna
depois do PBKDF2 com 100.000 iterações. Essa diferença é medível e permite
enumeração por temporização. A correção usual é calcular um hash descartável no
caminho do email inexistente, igualando o custo. Não implementado — registrado
como dívida na Issue D.

**Evidência**

`EVIDENCIAS.md`, seção "Semana 7: Login e JWT", com as duas respostas 401 lado
a lado e a tabela do `try/catch`.

---

## 33. Claim de papel como `role`, não `ClaimTypes.Role`

**O que foi decidido**

```csharp
new Claim("role", usuario.Papel.Nome)
```

E, na validação, `RoleClaimType = "role"` dentro de
`TokenValidationParameters`.

**Alternativa considerada**

`new Claim(ClaimTypes.Role, usuario.Papel.Nome)`, que é o padrão do .NET e foi
a primeira versão implementada.

**Por que foi rejeitada**

`ClaimTypes.Role` não é a string `"role"`: é a constante que expande para
`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`. O token gerado
na primeira tentativa carregava essa URI inteira como nome da claim, o que
funciona com o `[Authorize(Roles = ...)]` sem configuração extra, mas:

1. a issue pede explicitamente a claim `role`;
2. a URI ocupa 60 caracteres em todo token emitido, sem acrescentar informação;
3. qualquer cliente que não seja .NET precisaria conhecer essa convenção da
   Microsoft para ler o papel.

**O custo assumido**

Com o nome curto, o ASP.NET não encontra mais a claim pelo caminho padrão. Sem
`RoleClaimType = "role"` na validação, `[Authorize(Roles = "Admin")]` procura
pela URI, não acha, e responde **403 inclusive para um Admin legítimo** — com
token válido e assinatura correta. É uma falha silenciosa: nada no log indica
que o problema é o nome da claim.

**O que mudaria de ideia**

Nada dentro deste escopo. A troca já foi feita e a linha de configuração que
ela exige está no `Program.cs`.

**Evidência**

Token decodificado antes e depois da troca, no `EVIDENCIAS.md`, seção "Semana
7: Login e JWT" — o corpo passa de
`"http://schemas.microsoft.com/ws/2008/06/identity/claims/role":"Operador"`
para `"role":"Operador"`.

---

## 34. `UsuarioResponse` como DTO de saída, em vez de devolver o agregado

**O que foi decidido**

`POST /api/usuarios` devolve `UsuarioResponse(int Id, string Email, string
Papel)`. O agregado `Usuario` nunca é serializado para a resposta HTTP.

**Alternativa considerada**

Devolver o `Usuario` direto, como o `IncidentesController.Criar` faz com o
`Incidente`.

**Por que foi rejeitada**

O `Usuario` tem a propriedade `SenhaHash`. Serializá-lo colocaria o hash em
toda resposta de criação de usuário. Hash não é senha — não dá para revertê-lo
—, mas entregá-lo a qualquer cliente que crie uma conta é expor sem motivo o
material que um ataque offline usaria, junto com a informação de qual algoritmo
e quantas iterações o sistema usa (o prefixo `AQAAAAIAAYagAAAAE` codifica
exatamente isso).

O padrão do `IncidentesController` mostra o problema na prática: como ele
devolve a entidade, as respostas carregam `"eventos":[]` e objetos aninhados
com `"incidente":null` — campos internos do agregado vazando no contrato HTTP
porque ninguém decidiu o contrário.

**Por que não é `[JsonIgnore]` no `SenhaHash`**

Seria mais barato, mas funciona por omissão: protege o campo que alguém lembrou
de anotar. Uma propriedade nova adicionada ao agregado amanhã sai na resposta
por padrão. Com DTO de saída, o padrão é o inverso — só sai o que foi declarado.

**O que mudaria de ideia**

Nada aqui. O caminho oposto seria aplicar a mesma correção ao
`IncidentesController`, que é dívida existente e não escopo desta semana.

**Evidência**

`UsuarioResponse.cs`. Respostas 201 no `EVIDENCIAS.md`, seção "Semana 7:
Registro de usuário", sem `senhaHash` no corpo.

---

## 35. Seed do Admin na migration, com falha explícita sem a variável

**O que foi decidido**

O usuário Admin inicial nasce na migration `SeedUsuarioAdmin`, que lê
`ADMIN_SENHA_INICIAL` do ambiente, calcula o hash com o mesmo
`HasherDeSenhaPbkdf2` da aplicação, e insere via `migrationBuilder.InsertData`.
Sem a variável definida, a migration **lança** `InvalidOperationException` com
mensagem explicativa, em vez de criar um admin com senha vazia ou previsível.

**Alternativa considerada**

1. Seed no start da aplicação: um trecho no `Program.cs` que verifica se o
   admin existe e o cria se não existir.
2. Seed na migration, mas pulando em silêncio quando a variável não estivesse
   definida.

**Por que foram rejeitadas**

A alternativa 1 é o padrão mais comum e tem vantagens reais — a senha não passa
pelo SQL da migration, e não há exigência de variável de ambiente para rodar
`dotnet ef database update`. Foi rejeitada porque a issue pede seed **na
migration**, e porque o seed na migration garante que o admin exista assim que
o banco está migrado, sem depender de a API ter subido alguma vez.

A alternativa 2 é a que o prazo favorecia e é justamente por isso que foi
descartada: seed pulado em silêncio deixa o banco sem nenhum admin, e a
descoberta vem na primeira tentativa de login — possivelmente em outro
ambiente, possivelmente por outra pessoa. Falhar no momento em que o problema
acontece é mais barato do que descobrir depois.

**O custo assumido**

O `InsertData` do EF emite o hash como valor literal no SQL. Quem rodar a
migration vê o hash completo do admin no log. Hash não é senha, mas é material
que não precisaria estar ali — consequência direta de semear pela migration em
vez de pelo código da aplicação.

Além disso, qualquer pipeline de CI que rode `dotnet ef database update`
precisa definir `ADMIN_SENHA_INICIAL`, ou a migration falha. Isso é o
comportamento desejado, mas é um acoplamento novo entre o processo de migração
e a configuração de ambiente.

**O que mudaria de ideia**

Se o projeto passasse a ter múltiplos ambientes com processos de deploy
distintos, o seed migraria para um comando administrativo separado — nem
migration, nem start da aplicação — executado deliberadamente uma vez por
ambiente.

**Evidência**

Migration `20260923225151_SeedUsuarioAdmin`. SQL executado e grep com saída
vazia no `EVIDENCIAS.md`, seção "Semana 7: Registro de usuário".