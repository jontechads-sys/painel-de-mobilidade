# Glossário de Linguagem Ubíqua — Painel de Mobilidade

Este documento estabelece os termos formais do domínio de negócio. O código-fonte deve seguir rigorosamente estes nomes. Se houver divergência entre o termo de negócio e o código, o código está errado e deve ser refatorado.

---

## 1. Incidente
* **Termo de Negócio:** Incidente
* **Nome no Código:** `Incidente`
* **O que significa:** Um evento imprevisto ou perturbação na via pública (ex: acidente, semáforo quebrado, alagamento) que impacta a fluidez ou a segurança do trânsito.
* **O que não significa:** Não é uma falha interna do software (bug) e não é um chamado genérico de suporte de TI. É a raiz do nosso agregado principal.

## 2. Atendimento
* **Termo de Negócio:** Atendimento
* **Nome no Código:** `Atendimento`
* **O que significa:** O registro das ações operacionais ou o envio de recursos/equipes de campo para sinalizar, mitigar ou resolver os impactos de um Incidente.
* **O que não significa:** Não existe de forma isolada (só existe dentro de um Incidente), não possui repositório próprio e não é um atendimento de SAC por telefone.

## 3. Severidade
* **Termo de Negócio:** Severidade
* **Nome no Código:** `Severidade`
* **O que significa:** O grau de impacto que o incidente gera na mobilidade das vias (Baixa, Média, Alta ou Não Triada).
* **O que não significa:** Não se refere à prioridade de tarefas no Jira/sistema dos desenvolvedores.

## 4. Bairro
* **Termo de Negócio:** Bairro
* **Nome no Código:** `Bairro`
* **O que significa:** A região geográfica cadastrada na cidade onde o incidente ocorreu, usada para agrupar métricas de criticidade.
* **O que não significa:** Não se confunde com o endereço completo ou com coordenadas de latitude/longitude.

## 5. Órgão Responsável
* **Termo de Negócio:** Órgão Responsável
* **Nome no Código:** `OrgaoResponsavel`
* **O que significa:** A entidade pública ou concessionária (ex: CET, SPTrans) acionada para resolver o problema na via.
* **O que não significa:** Não se refere à empresa de desenvolvimento de software que criou o painel.

## 6. Período
* **Termo de Negócio:** Período
* **Nome no Código:** `Periodo`
* **O que significa:** O intervalo de tempo exato que compreende desde o momento da abertura (Início) até o encerramento (Fim) do incidente.
* **O que não significa:** Não é o horário de expediente dos operadores do painel.

## 7. Situação
* **Termo de Negócio:** Situação
* **Nome no Código:** `Situacao`
* **O que significa:** O estado atual em que se encontra o tratamento do atendimento na via (ex: Em Andamento, Concluído).
* **O que não significa:** Não deve ser confundido com o Status geral do Incidente em si, pertencendo estritamente ao ciclo do atendimento.

## 8. Descrição
* **Termo de Negócio:** Descrição
* **Nome no Código:** `Descricao`
* **O que significa:** Descrião de Incidente do tipo string aberta para relatar do que se trata. Ex: (Ocorrido uma colisão de carros, deixando feridos).
* **O que não significa:** Não é se trata de dados gerais de Incidente como data hora e local..

## 9. Tipo de Incidente
* **Termo de Negócio:** Tipo de Incidente
* **Nome no Código:** `TipoIncidente`
* **O que significa:** A categorização exata do evento na via que ajuda a definir qual equipe deve ser despachada (ex: Alagamento, Semáforo Quebrado, Acidente Grave).
* **O que não significa:** Não se refere ao nível de gravidade ou impacto do evento, que é papel exclusivo da Severidade.

## 10. Data e Hora
* **Termo de Negócio:** Data e Hora
* **Nome no Código:** `DataHora`
* **O que significa:** O registro exato de quando o evento começou na via urbana, utilizado para medir os tempos operacionais.
* **O que não significa:** Não pode ser uma data/hora no futuro e não deve ser confundida com o carimbo de data/hora (timestamp) do banco de dados de quando o registro foi salvo.
---

## Histórico de Alinhamento de Domínio (Refatoração Exigida)

Para eliminar termos puramente técnicos ou de arquitetura das pastas do domínio:

* **Commit:** `635499b`
  * **Nome Anterior (Técnico):** `Mobilidade.Domain.Modelos` (Namespace estrutural técnico)
  * **Nome Atual (Domínio):** `Mobilidade.Domain.Entity` e `Mobilidade.Domain.ValueObjects` (ou simplificado para `Mobilidade.Domain`)
  * **Justificativa:** O sufixo/pasta `.Modelos` é um jargão genérico técnico (frequentemente confundido com MVC Models ou Anêmicos). Para respeitar o DDD exigido na Issue #24, os conceitos foram segregados de acordo com sua semântica de domínio.