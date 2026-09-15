# Persistência de `ICostLogger` — adição ao `AutonomiaSaaS.Modules.AgentRuntime`

Fecha o `[PENDENTE]` da spec v2.0, seção 6: `ICostLogger`/`InMemoryCostLogger` existem, mas
perdem todo o histórico a cada restart. Esta pasta contém a implementação persistente — não o
resto do módulo `AgentRuntime` (`IAnthropicClient`, `IModelRouter`, `IAgentRuntime`), que já
existe e não foi anexado nesta conversa.

## O que foi assumido

`ICostLogger` já existe e funciona (v0.1 in-memory) — mas sua assinatura exata não está
documentada na spec, só o nome e o papel. `Shims/AgentRuntimeShims.cs` contém uma assinatura
**inferida**, com o formato mínimo que faz sentido dado o que a spec diz sobre o campo
`EstimatedCostTokens` (tokens, não dólares) e sobre "teto de custo por tarefa e por ciclo do
orquestrador" (arquitetura, seção 8). **Apague o shim e aponte pro `ICostLogger` real** assim
que integrar — se a assinatura real for diferente, só a implementação de `PersistentCostLogger`
precisa mudar, o resto (pricing, storage, migration) continua válido.

## Duas responsabilidades que ficaram separadas de propósito

1. **Registrar tokens gastos** (`LogAsync`) — grava o consumo bruto (input/output tokens) por
   tarefa e por modelo, sem opinar sobre preço.
2. **Converter tokens em custo** (`IModelPricingProvider`) — separado do registro em si porque
   preço por modelo muda com o tempo (a Anthropic já revisou preço de modelo várias vezes) e
   porque uma tarefa registrada em janeiro deveria manter o custo calculado com o preço de
   janeiro, não ser recalculada com o preço de hoje. Por isso `EstimatedCostUsd` é gravado no
   momento do `LogAsync`, não computado on-the-fly na leitura.

## ⚠️ Sobre os valores de preço incluídos

`ModelPricingCatalog.cs` vem com valores de exemplo por modelo (USD por milhão de tokens,
input/output). Eu confirmei esses números via busca na web nesta conversa (múltiplas fontes
convergindo, mas nenhuma delas era a página oficial `docs.claude.com/en/docs/about-claude/pricing`
carregada diretamente) — **trate como ponto de partida, não como preço contratual**. Antes de
usar isso para bloquear execução de verdade (teto de custo), confirme os valores atuais na
documentação oficial: https://docs.claude.com/en/docs/about-claude/pricing. Um preço errado aqui
não quebra a aplicação, mas pode deixar o teto de custo da seção 8 da arquitetura frouxo ou
rígido demais — o catálogo é `IModelPricingProvider`, uma interface, exatamente para poder
substituir por configuração externa (appsettings, ou uma tela de admin) sem tocar no resto.

## Por que documento YesSql, de novo (mesmo padrão do cofre e do sandbox)

Um log de custo é escrita frequente, append-heavy, sem necessidade de tela de edição de
conteúdo — mesma justificativa do `AgentCredentialRecord`. `CostLogEntryRecord` +
`CostLogEntryIndex` seguem exatamente essa forma.

## O que este módulo NÃO resolve

- **Não é o `BudgetCapEvaluator`.** Este código só registra e soma custo — decidir "bloquear a
  tarefa porque passou do teto" é lógica separada (mencionada na arquitetura seção 8 como "teto
  de custo por tarefa e por ciclo do orquestrador", ainda sem uma classe nomeada na spec). As
  duas queries que esse futuro avaliador provavelmente vai precisar
  (`GetTotalCostForTaskAsync`, `GetTotalCostSinceAsync`) já estão prontas em `ICostLogger`.
- **Não faz limpeza/arquivamento de histórico antigo.** Um log de custo cresce indefinidamente;
  em algum momento vai precisar de uma política de retenção (ex: agregar por dia após 90 dias),
  mas isso não foi implementado aqui.
