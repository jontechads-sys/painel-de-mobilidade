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

- `appsettings.json`/`appsettings.Development.json` ainda têm a senha
  antiga cravada e precisam ser atualizados (aplicação está quebrada até
  isso ser corrigido).
- Histórico do Git ainda contém a senha antiga em commits passados — ela
  não dá mais acesso a nada, mas o rastro continua.

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