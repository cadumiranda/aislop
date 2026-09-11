# Expiração automática de aprovação — adição ao `AutonomiaSaaS.Modules.RiskGate`

Fecha a lacuna registrada na spec v2.0 (seção 5): `AgentTaskStore.GetExpiredApprovalsAsync`
já existe e funciona, mas nenhum `IBackgroundTask` chamava isso em ciclo. Esta pasta contém
só a peça que faltava — não o resto do módulo `RiskGate`, que já existe e não foi anexado
nesta conversa.

## O que foi assumido (sem acesso ao `BusinessCore` real)

Só tenho a especificação, não o código-fonte de `AutonomiaSaaS.Modules.BusinessCore`. A pasta
`Shims/` contém **contratos mínimos inferidos** a partir do que a spec v2.0 documenta
textualmente (seções 3.2 e 5):

- `IAgentTaskStore.GetExpiredApprovalsAsync` — dito como já existente e funcional
- `IAgentTaskStore.TransitionAsync` — dito como "único ponto de escrita autorizado"
- `AgentTaskStatus.Expirada` — enum já documentado na seção 3.2
- Um tipo `ExpiredApprovalTask(string TaskId, AgentTaskPart Part)` — **este eu inventei**,
  porque a spec não diz o formato exato de retorno de `GetExpiredApprovalsAsync`. Pode ser
  que o método real já devolva só `IEnumerable<AgentTaskPart>` com o ID acessível de outro
  jeito (ex: `ContentItem.ContentItemId`) — ajuste isso pela assinatura real antes de compilar.

**Antes de integrar:** apague `Shims/` inteiro e troque as referências para o namespace real
de `AutonomiaSaaS.Modules.BusinessCore`. Os shims existem só para este código ficar legível
e testável isoladamente nesta conversa.

## Decisão de design: por que `IBackgroundTask` do Orchard Core, e não `BackgroundService`

O módulo de sandbox (conversa anterior) usa `BackgroundService` do .NET genérico porque roda
fora do contexto de tenant do Orchard Core. Aqui é o oposto: `AgentTask` é multi-tenant nativo
(cada tenant tem seu próprio banco, seção 3.1 da spec), e o `IBackgroundTask` do Orchard Core
já roda uma vez **por tenant ativo** automaticamente — é o mecanismo certo para não ter que
escrever manualmente um loop "para cada tenant, verifique aprovações expiradas".

## O que essa peça NÃO resolve (de propósito)

- **Configuração do intervalo via Admin UI.** O `[BackgroundTask(Schedule = "...")]` do Orchard
  Core já vem com suporte nativo a override de schedule pela UI de Admin (tela de "Background
  Tasks" do próprio Orchard Core) — não precisei construir nada para isso, só usar o atributo.

## Atualização: notificação real via `OrchardCore.Notifications` + lock distribuído

Duas mudanças em cima da primeira entrega:

1. **`OrchardNotificationApprovalExpirationNotifier`** substitui o
   `LoggingApprovalExpirationNotifier` como implementação registrada por padrão. Usa
   `INotificationService.SendAsync(IUser, Notification, CancellationToken)` — API confirmada
   contra a documentação oficial do módulo `OrchardCore.Notifications`, não inventada. Precisa
   das features `OrchardCore.Notifications` e `OrchardCore.Notifications.Email` habilitadas no
   tenant (ver `Startup.ApprovalExpiration.snippet.cs` para o comentário completo).

   **Novo shim, mesmo aviso de sempre:** `AdministratorRoleRecipientResolver` (quem recebe a
   notificação) é um placeholder — notifica todo mundo com role Administrator no tenant, porque
   a spec não documenta onde fica guardado "quem é o dono desta aprovação" no `AgentTaskPart`.
   Assim que esse campo existir, troque o resolver.

   **Risco de compilação a verificar:** `FakeUser : IUser` nos testes assume que a interface
   real `OrchardCore.Users.IUser` só exige `UserId`/`UserName`/`Email`. Se a interface real do
   projeto tiver mais membros obrigatórios, o fake não compila como está — ajuste conforme o
   `IUser` real.

2. **`LockTimeout`/`LockExpiration`** adicionados ao `[BackgroundTask(...)]` (5s / 60s). Isso só
   previne execução duplicada da tarefa entre múltiplas instâncias do host se um provedor de
   lock distribuído estiver configurado (ex: `OrchardCore.Redis`) — é decisão de infra, não algo
   que este código resolve sozinho. Com uma instância só, essa mudança não muda nada na prática,
   mas evita um bug real quando escalar horizontalmente.
