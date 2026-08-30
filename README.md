# AutonomiaSaaS.Modules.AgentRuntime

Primeiro módulo implementado da arquitetura (ver `arquitetura-mvp-saas-agentes-v1.6.md`,
seção 6 da especificação técnica). Escolhido por ser o mais independente:
não depende do sistema de Content Types do Orchard Core, não precisa de
banco de dados, não precisa de tenant configurado — só precisa de uma
`AgentRuntimeOptions.ApiKey` válida pra fazer chamadas reais.

## Este ambiente não tem o SDK do .NET instalado

Este código foi escrito e revisado manualmente, mas **não foi compilado nem
executado neste ambiente** (sem `dotnet` CLI, sem acesso ao NuGet). Antes de
seguir para o próximo módulo, rode localmente:

```bash
cd AutonomiaSaaS
dotnet restore
dotnet build
dotnet test
```

Se algo não compilar, é o primeiro lugar a olhar — meu retorno visual daqui
é limitado a leitura de código, não a um compilador real.

## Estrutura

```
src/AutonomiaSaaS.Modules.AgentRuntime/
├── Models/AnthropicModels.cs       # Request/response da API /v1/messages
├── AgentRuntimeOptions.cs           # Configuração (API key nunca em texto puro)
├── IAnthropicClient.cs              # Interface do cliente HTTP fino
├── AnthropicClient.cs               # Implementação HTTP
├── AnthropicApiException.cs         # Erro estruturado da própria API
├── ModelRouting/
│   ├── IModelRouter.cs              # Decide qual modelo por complexidade
│   └── ModelRouter.cs
├── Cost/
│   ├── ICostLogger.cs               # Registro de custo por tarefa/tenant
│   └── InMemoryCostLogger.cs        # Implementação em memória (Fase 1 / testes)
├── IAgentRuntime.cs                 # Fachada de alto nível
├── AgentRuntimeService.cs           # Implementação da fachada
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs

tests/AutonomiaSaaS.Modules.AgentRuntime.Tests/
├── FakeHttpMessageHandler.cs        # Fake de transporte HTTP (sem rede real)
├── FakeAnthropicClient.cs           # Fake de IAnthropicClient
├── AnthropicClientTests.cs
├── ModelRouterTests.cs
├── InMemoryCostLoggerTests.cs
└── AgentRuntimeServiceTests.cs
```

## Decisões que valem explicação

- **`AgentRuntime` não conhece `AgentTask` nem `BusinessContext`.** `TaskId` e
  `TenantId` são passados como `string` simples em `AgentRuntimeRequest`. Isso
  é proposital: o módulo `BusinessCore` (Content Types do Orchard Core) pode
  ser implementado depois, em paralelo, sem que este módulo precise mudar.
- **`ICostLogger` não é registrado dentro de `AddAgentRuntime`.** A
  implementação real (persistida, associada ao `AgentTask` de verdade) só faz
  sentido quando o `BusinessCore` existir. Por enquanto, use
  `AddAgentRuntimeInMemory` para rodar local/testar.
- **Erro de API vira `AnthropicApiException`, não uma exceção genérica.** Isso
  importa pra quando o `RiskGate`/`AcquisitionAgent` decidirem se algo deve
  reexecutar automaticamente (falha de transporte) ou virar uma tarefa em
  status de falha visível no painel (erro reportado pela própria API, como
  rate limit ou chave inválida).
- **`ActivityComplexity` tem só dois valores por enquanto** (`Planning` e
  `Repetitive`), de propósito — é a implementação mínima do roteamento de
  custo da seção 8 do documento de arquitetura. Pode crescer quando a Fase 3
  (Ads) precisar de um terceiro nível.

## Próximo módulo sugerido

`BusinessCore` — feito. Ver seção abaixo.

---

# AutonomiaSaaS.Modules.BusinessCore

Segundo módulo implementado. Diferente de `AgentRuntime`, este módulo
**depende do Orchard Core** (Content Management, Data Migration, YesSql para
índices) — não dá pra evitar isso, já que Content Types são a forma nativa do
framework de persistir dado estruturado. A parte mais valiosa deste módulo,
porém, foi isolada de propósito para não depender de nada disso: a máquina de
estados.

## Estrutura

```
src/AutonomiaSaaS.Modules.BusinessCore/
├── Domain/                              # Zero dependência de Orchard Core aqui
│   ├── RiskLevel.cs
│   ├── AgentTaskStatus.cs
│   ├── AgentTaskStateMachine.cs         # A peça mais importante do módulo
│   └── InvalidStateTransitionException.cs
├── Parts/
│   ├── BusinessContextPart.cs
│   └── AgentTaskPart.cs
├── Indexes/
│   └── AgentTaskPartIndex.cs            # Índice + IndexProvider
├── Services/
│   ├── IAgentTaskStore.cs / AgentTaskStore.cs
│   └── IBusinessContextStore.cs (+ implementação no mesmo arquivo)
├── Migrations.cs
├── Startup.cs                            # Registro de DI (padrão Orchard Core)
└── Manifest.cs                           # [assembly: Module(...)]

tests/AutonomiaSaaS.Modules.BusinessCore.Tests/
└── AgentTaskStateMachineTests.cs         # 15 testes, cobrindo toda transição
                                            # válida e as inválidas mais perigosas
```

## Por que só a máquina de estados tem teste automatizado aqui

Testar `AgentTaskStore` de verdade exigiria simular `IContentManager` e
`ISession` do Orchard Core, que são interfaces grandes e mais fiéis a um
teste de integração (com SQLite in-memory, por exemplo) do que a um teste
unitário rápido. Isso fica como próximo passo recomendado — um projeto de
teste de integração que sobe um `OrchardCoreBuilder` mínimo — mas não bloqueia
o resto da implementação: o comportamento que **precisa** estar certo desde já
(nenhuma tarefa pula o painel de aprovação por engano) já está coberto pelos
testes de `AgentTaskStateMachine`, que são puros e não pedem infraestrutura.

## Decisões que valem explicação

- **`AgentTaskStore.TransitionAsync` é o único lugar autorizado a mudar
  `AgentTaskPart.Status`.** Ele sempre chama `AgentTaskStateMachine.Validate`
  antes de persistir. Qualquer código (workflow, controller, background task)
  que precise mudar o status de uma tarefa deve passar por aqui — nunca
  chamar `IContentManager.UpdateAsync` diretamente sobre um `AgentTaskPart`.
- **`BusinessContext` é um singleton por tenant**, com `ContentItemId` fixo
  (`business-context-singleton`). Como multi-tenancy já isola os dados entre
  clientes, não existe cenário de "múltiplos contextos de negócio" dentro de
  um mesmo tenant nesta Fase 1.
- **Os Content Types são `Creatable(false)`** — não aparecem no menu "Novo
  conteúdo" do Admin do Orchard Core. `AgentTask` só é criado via
  `AgentTaskStore.CreateAsync`, `BusinessContext` só via
  `BusinessContextStore.UpsertAsync`. Isso evita alguém criar uma tarefa pela
  UI de conteúdo genérica e pular toda a validação de domínio.
- **Não fixei a versão exata dos pacotes `OrchardCore.*`** no `.csproj` — usei
  `2.1.0` como placeholder. Ajuste para a versão que você já está usando (ou
  vai usar) no host `AutonomiaSaaS.Web`, que ainda não existe neste repositório.

## ⚠️ Risco de compilação maior que o módulo anterior

Como não tenho acesso ao NuGet nem ao SDK do Orchard Core neste ambiente,
a superfície de API que usei (`IContentDefinitionManager.AlterPartDefinitionAsync`,
`SchemaBuilder.CreateMapIndexTable`, `IndexProvider<ContentItem>`, etc.) foi
escrita de memória do padrão usual do framework. É a parte do código com
maior chance de precisar de ajuste fino de assinatura de método ao compilar
de verdade — trate isso como um rascunho fiel ao padrão, não como certeza de
compilação limpa na primeira tentativa.

**Atualização, já validado por compilação real:** `SchemaBuilder.CreateMapIndexTable`
e `SchemaBuilder.AlterIndexTable` não são genéricos (`<T>`) como eu tinha
escrito — recebem `typeof(AgentTaskPartIndex)` como primeiro argumento e um
parâmetro extra (`null` no caso, provavelmente o nome de coleção/schema) no
final. `Migrations.cs` já está corrigido para essa assinatura.

`Startup.cs` também corrigido: `services.AddIndexProvider<T>()` não existe
como extension method — o registro correto é
`services.AddSingleton<IIndexProvider, AgentTaskPartIndexProvider>()`.

Ainda não confirmados por compilação: `IContentDefinitionManager` (as chamadas
em `Migrations.cs` fora do `SchemaBuilder`) e `IndexProvider<ContentItem>` em
`AgentTaskPartIndex.cs`. Mesma cautela de antes se aparecer erro neles.

**Terceira correção:** `IAgentTaskStore.cs` estava sem
`using AutonomiaSaaS.Modules.BusinessCore.Parts;`, necessário porque a
interface retorna `AgentTaskPart` em três métodos. Corrigido. Conferi os
outros arquivos do módulo (`IBusinessContextStore.cs`, `AgentTaskStore.cs`) e
eles já tinham o using correto — não era um padrão repetido no resto do código.

## Próximo módulo sugerido

`RiskGate` — feito. Ver seção abaixo.

---

# AutonomiaSaaS.Modules.RiskGate

Terceiro módulo implementado. Depende só de `BusinessCore` (dependência de
mão única, como planejado) — não conhece `AgentRuntime`, porque classificar
risco não tem nada a ver com como uma chamada de modelo é feita.

## Estrutura

```
src/AutonomiaSaaS.Modules.RiskGate/
├── Domain/                          # Tudo puro, sem OrchardCore, exceto RiskClassifier
│   ├── ActionRiskCatalog.cs         # Regra fixa por tipo de ação (seção 4)
│   ├── BudgetCapEvaluator.cs        # Regra dinâmica por magnitude (seção 4)
│   ├── IRiskClassifier.cs           # Records de request/result + interface
│   └── RiskClassifier.cs            # Único ponto que depende de IBusinessContextStore
├── Services/
│   └── TaskRiskGateway.cs           # Fecha o fluxo: classifica → cria → transiciona
├── Startup.cs
└── Manifest.cs

tests/AutonomiaSaaS.Modules.RiskGate.Tests/
├── ActionRiskCatalogTests.cs        # 7 testes
├── BudgetCapEvaluatorTests.cs       # 8 testes
├── RiskClassifierTests.cs           # 6 testes (usa FakeBusinessContextStore)
├── TaskRiskGatewayTests.cs          # 4 testes (usa FakeAgentTaskStore + FakeRiskClassifier)
└── Fakes: FakeBusinessContextStore, FakeAgentTaskStore, FakeRiskClassifier
```

## O princípio mais importante deste módulo: fail closed

Toda situação ambígua ou de dado ausente resolve para **Alto risco**, nunca
para Baixo:

- Ação não catalogada, sem dado de gasto → Alto (`RiskClassifier`).
- Teto de gasto (`budget_caps`) não configurado para a chave pedida → Alto
  (`BudgetCapEvaluator`).
- `BusinessContext` do tenant nem existe ainda → Alto (`RiskClassifier`).
- JSON de `budget_caps` malformado → Alto, sem lançar exceção
  (`BudgetCapEvaluator`).

A lógica inversa (assumir Baixo risco por omissão) seria exatamente o tipo de
"ação autônoma indesejada" que a seção 9 do documento de arquitetura (riscos
identificados) existe para evitar — mais barato pedir uma aprovação a mais do
que deixar passar uma ação que não deveria ter sido automática.

## `TaskRiskGateway`: por que ele existe além do `RiskClassifier`

`RiskClassifier` só responde "qual é o risco". Alguém ainda precisaria:
criar a `AgentTask`, decidir para qual estado transicionar com base no
resultado, e (se for alto risco) gravar o `ApprovalTimeout`. Se cada agente
futuro (`AcquisitionAgent`, etc.) fizesse isso na mão, seria fácil esquecer um
passo — por exemplo, classificar como alto risco mas esquecer de transicionar
para `AguardandoAprovacao`, deixando a tarefa presa em `Proposta` para sempre.

`TaskRiskGateway.ProposeActionAsync` é o único método que um agente deveria
chamar para propor uma ação. Ele garante, num único lugar, que **nenhuma
tarefa criada por este caminho fica parada em `Proposta`** — o teste
`ProposeActionAsync_NuncaDeixaTarefaParadaEmProposta` existe justamente para
travar essa garantia.

## Por que `TimeProvider` em vez de `DateTimeOffset.UtcNow`

`TaskRiskGateway` recebe `TimeProvider` injetado (registrado como
`TimeProvider.System` no `Startup.cs`) em vez de chamar
`DateTimeOffset.UtcNow` diretamente. Isso é o que permite
`TaskRiskGatewayTests` fixar "agora" em uma data exata e verificar que o
`ApprovalTimeout` calculado bate exatamente com "agora + 48 horas", sem
depender do relógio real da máquina que roda o teste (o que tornaria o teste
não-determinístico, ainda que só por um instante de diferença).

## ⚠️ Pontos ainda não confirmados por compilação

- `Manifest.cs` usa `Dependencies = new[] { "AutonomiaSaaS.Modules.BusinessCore" }`
  no atributo `[assembly: Module]` — não tenho certeza se o nome esperado
  aqui é o nome do assembly, o `Id` do módulo, ou algo definido em
  `BusinessCore`'s próprio `Manifest.cs` (que atualmente não define um `Id`
  explícito). Se der erro ou o carregamento de módulo não respeitar a ordem,
  esse é o primeiro lugar a revisar.
- `OrchardCore.Modules.Abstractions` como único pacote do `.csproj` — pode
  ser que `StartupBase` exija um pacote diferente ou adicional dependendo da
  versão real do Orchard Core que você está usando.

## Próximo módulo sugerido

`AcquisitionAgent` — feito. Ver seção abaixo. Fecha o loop completo da Fase 1.

---

# AutonomiaSaaS.Modules.AcquisitionAgent

Quarto e último módulo da Fase 1 (roadmap, seção 11 do documento de
arquitetura). É o único que depende dos três anteriores ao mesmo tempo —
por isso foi implementado por último, na ordem de menor para maior
dependência que seguimos desde o início.

## Decisão mais importante deste módulo

A seção 5 da especificação técnica original propunha modelar os 8 passos da
Fase 1 como um **Workflow Type** do `OrchardCore.Workflows`, com atividades
customizadas (`TaskActivity`). Depois de já ter corrigido 3 erros reais de
API do Orchard Core nos módulos anteriores, decidi **não** escrever a
integração com o motor de Workflows agora — a API de suspender/retomar
execução (`IWorkflowExecutionContext`, `IWorkflowManager.ResumeWorkflowAsync`)
é a superfície que eu tenho menos confiança de acertar de memória, e é
justamente a parte mais cara de errar silenciosamente (uma tarefa que deveria
ficar suspensa aguardando aprovação, mas não fica de verdade).

Em vez disso, `AcquisitionAgentOrchestrator` implementa a sequência
determinística inteira como métodos C# comuns, testável sem nenhuma
dependência de Workflows:

- `GenerateLandingPageAsync` — passos 1 a 6: recebe a descrição do negócio,
  gera a landing page (`IAgentRuntime`), propõe a ação (`ITaskRiskGateway`,
  baixo risco, auto-executa), faz deploy em staging, verifica saúde, e propõe
  a promoção para produção (sempre alto risco, fica aguardando aprovação).
- `PromoteToProductionAsync` — passos 7 e 8: chamado depois que um humano
  aprova a tarefa no painel (fora deste módulo). Promove de fato, ou reverte
  (`rollback_action`) se a promoção falhar ou o deploy ficar `Unhealthy`.

**O que fica pendente:** ligar esses dois métodos a atividades visuais de
Workflow (para o `IWorkflowManager` suspender de verdade a execução entre
`GenerateLandingPageAsync` e `PromoteToProductionAsync`, em vez de dois
métodos públicos chamados separadamente por, por exemplo, um controller do
Admin) é uma camada fina de próximo passo — a lógica que ela vai chamar já
está pronta e testada.

## Estrutura

```
src/AutonomiaSaaS.Modules.AcquisitionAgent/
├── Domain/
│   ├── LandingPagePlan.cs / DeploymentResult.cs
│   └── GenerateLandingPageResult.cs / PromoteToProductionResult.cs
├── Services/
│   ├── IDeploymentClient.cs              # Abstrai o provedor de deploy
│   ├── VercelDeploymentClient.cs         # ⚠️ Ver aviso abaixo
│   └── AcquisitionAgentOrchestrator.cs   # A peça mais importante do módulo
├── Startup.cs
└── Manifest.cs

tests/AutonomiaSaaS.Modules.AcquisitionAgent.Tests/
├── AcquisitionAgentOrchestratorTests.cs   # 9 testes, cobrindo os dois
│                                           # caminhos de falha e o caminho feliz
└── Fakes: FakeAgentRuntime, FakeTaskRiskGateway, FakeAgentTaskStore, FakeDeploymentClient
```

## Garantias que os testes travam

- **Deploy em produção nunca é proposto se staging não ficou saudável**
  (`GenerateLandingPageAsync_StagingNaoSaudavel_ReverteTarefaENaoProdePromocao`)
  — é a implementação de "deploys sempre em staging antes de produção" da
  seção 5 do documento de arquitetura.
- **A tarefa de produção nunca sai de `AguardandoAprovacao` sozinha** —
  `GenerateLandingPageAsync` propõe a ação mas nunca a promove; só
  `PromoteToProductionAsync` faz isso, e só depois de checar que o Status já
  é `Aprovada` (defesa em profundidade, mesmo que quem chame tenha esquecido
  de checar antes).
- **Os dois caminhos de falha na promoção (exceção lançada vs. resultado
  retornado como `Unhealthy`) levam ao mesmo desfecho**: chamar
  `RollbackToPreviousAsync` e marcar a tarefa como `Revertida` — nenhum dos
  dois deveria deixar produção num estado indefinido nem a tarefa "presa" em
  `Executando`.

## ⚠️ `VercelDeploymentClient`: não confie nisso sem checar a documentação da Vercel

Diferente do resto do módulo, esta classe não foi escrita com o mesmo nível
de confiança — não confirmei os endpoints reais da Vercel Deployments API
(formato de payload, nomes de campo, endpoint exato de promoção). Os testes
do orquestrador não dependem dela (usam `IDeploymentClient` fake), então isso
não bloqueia validar o resto do módulo, mas trate esta classe como esqueleto,
não como integração pronta para produção.

## Resumindo os quatro módulos da Fase 1

| Módulo | Depende de | Testes | Risco de compilação |
|---|---|---|---|
| `AgentRuntime` | nenhum (class library pura) | 12 | Baixo |
| `BusinessCore` | Orchard Core | 15 (só a máquina de estados) | Médio-alto (3 erros já corrigidos) |
| `RiskGate` | `BusinessCore` | 25 | Baixo (pouca API nova do Orchard Core) |
| `AcquisitionAgent` | os três anteriores | 9 | Baixo no orquestrador; alto se/quando ligar a Workflows de verdade |

## Próximo passo sugerido

`AutonomiaSaaS.Web` — feito. Ver seção final abaixo.

---

# AutonomiaSaaS.Web (host)

O executável de verdade. Referencia os quatro módulos; o Orchard Core os
descobre automaticamente (via `[assembly: Module]`) só por estarem
referenciados no `.csproj` — não precisa registrar nada manualmente aqui.

## 🐛 Bug real encontrado só ao montar o host

`AgentRuntime` **não tinha** `Manifest.cs`/`Startup.cs` como os outros três
módulos — só os métodos de extensão `AddAgentRuntime`/`AddAgentRuntimeInMemory`,
que precisam ser chamados por alguém. `AcquisitionAgent.Startup` registra
`IAcquisitionAgentOrchestrator` (que depende de `IAgentRuntime` no
construtor) mas nunca chamava `AddAgentRuntime` — ou seja, o container de DI
ia falhar ao tentar resolver `IAcquisitionAgentOrchestrator` em runtime, um
erro que só aparece ao rodar de verdade, não ao compilar cada módulo
isoladamente. Corrigido: `AgentRuntime` agora tem `Manifest.cs` e `Startup.cs`
próprios, chamando `AddAgentRuntimeInMemory` (a versão com `ICostLogger` em
memória — a persistente ainda não existe, ver aviso no módulo `AgentRuntime`).

Isso é exatamente o tipo de erro que só aparece quando as peças são montadas
juntas — nenhum dos quatro módulos isoladamente "sabia" que faltava essa
peça.

## Como rodar localmente

```bash
cd AutonomiaSaaS
dotnet restore
dotnet user-secrets set "AgentRuntime:ApiKey" "sk-ant-sua-chave-aqui" --project src/AutonomiaSaaS.Web
dotnet run --project src/AutonomiaSaaS.Web
```

Na primeira execução, o Orchard Core abre o assistente de setup no navegador
(`https://localhost:7139`). Escolha SQLite como banco para rodar local sem
precisar de infraestrutura externa — isso grava a configuração do tenant em
`src/AutonomiaSaaS.Web/App_Data/` (já no `.gitignore`, nunca comitar).

## O que ainda falta para a Fase 1 funcionar de ponta a ponta

Mesmo com o host de pé, ainda faltam duas coisas que a especificação técnica
menciona mas nenhum dos quatro módulos implementa ainda:

1. **Painel de aprovação (Admin UI)** — seção 8 da especificação técnica.
   Hoje `PromoteToProductionAsync` existe no `AcquisitionAgentOrchestrator`,
   mas nada no sistema chama `AgentTaskStore.TransitionAsync(..., Aprovada)`
   a partir de um clique real de um humano — não existe página nem endpoint
   ainda.
2. **`IBackgroundTask` de expiração de aprovação** — seção 5 da especificação
   técnica. `AgentTaskStore.GetExpiredApprovalsAsync` já existe e funciona,
   mas nada chama isso em ciclo ainda.
3. **Ligação com `OrchardCore.Workflows` de verdade** — já registrado como
   pendência no README do `AcquisitionAgent`.

## ⚠️ Maior incerteza deste arquivo: `OrchardCore.Application.Cms.Targets`

Usei esse nome de pacote e o padrão mínimo de `Program.cs`
(`AddOrchardCms()` / `UseOrchardCore()`) com razoável confiança — é o padrão
documentado oficialmente pelo Orchard Core há várias versões — mas não tenho
como confirmar a versão exata do pacote (`2.1.0`, o mesmo placeholder usado
nos módulos) nem se `OrchardCore.Application.Cms.Targets` continua sendo o
nome atual do pacote "tudo incluso". Se o `dotnet restore` falhar em
encontrar esse pacote, esse é o primeiro lugar a checar na documentação
oficial atual do Orchard Core.

---

# Painel de Aprovação (Admin UI) — seção 8 da especificação técnica

Última peça da Fase 1. Vive dentro do módulo `RiskGate` (mesmo lugar proposto
na especificação técnica original), e é de longe a parte com mais superfície
nova de API do Orchard Core desta sessão inteira: Controllers MVC, Views
Razor, menu de Admin (`INavigationProvider`) e permissões
(`IPermissionProvider`). Depois de já ter corrigido erros reais nos módulos
anteriores, tratei esta parte com o maior grau de cautela — ver avisos abaixo.

## Dois bugs reais encontrados só ao desenhar esta peça (antes mesmo de compilar)

1. **Faltava o elo entre "aprovar" e "executar de verdade".**
   `AgentTaskStore.TransitionAsync(taskId, Aprovada)` só muda o `Status` —
   nada disparava `AcquisitionAgentOrchestrator.PromoteToProductionAsync`
   depois disso. Resolvido com um padrão de despacho: `IApprovedTaskExecutor`
   (interface nova em `BusinessCore`) + `ApprovedTaskDispatcher`, que resolve
   o executor certo pelo `AgentName` da tarefa. Isso existir em `BusinessCore`
   — não em `RiskGate` nem na própria UI do Admin — é o que evita uma
   dependência circular: `AcquisitionAgent` já depende de `RiskGate`, então
   `RiskGate`/Admin UI não podem depender de volta de `AcquisitionAgent`.
2. **`PromoteToProductionAsync` precisa do `stagingDeploymentId`, mas
   `AgentTaskPart` não tinha onde guardar isso.** Adicionei `PayloadJson`
   (bag genérico de dados extras específicos do agente) ao `AgentTaskPart` —
   e, como parts do Orchard Core são serializadas como JSON dentro do
   próprio `ContentItem`, isso **não exigiu nenhuma migração de banco nova**,
   só a propriedade em C#.

## Um terceiro bug, de runtime, pego antes mesmo de rodar

`AcquisitionAgentOrchestrator` grava `PayloadJson` serializando um objeto
anônimo com a propriedade em **camelCase** (`stagingDeploymentId`).
`AcquisitionAgentApprovedTaskExecutor` desserializava isso numa classe C#
com a propriedade em **PascalCase** (`StagingDeploymentId`) sem
`PropertyNameCaseInsensitive = true` — `System.Text.Json` é case-sensitive
por padrão, então isso silenciosamente retornaria `StagingDeploymentId`
vazio, e a promoção pra produção falharia sempre, sem exceção clara
apontando a causa. Corrigido, e travado com um teste
(`ExecuteApprovedTaskAsync_PayloadComCamelCase_ExtraiStagingDeploymentIdCorretamente`).

## Estrutura nova

```
src/AutonomiaSaaS.Modules.RiskGate/
├── Controllers/ApprovalController.cs   # Index, Approve, Reject
├── Views/
│   ├── _ViewImports.cshtml             # Tag Helpers (asp-action, etc.)
│   └── Approval/Index.cshtml
├── ViewModels/ApprovalViewModels.cs    # Nunca expor AgentTaskPart na View
├── AdminMenu.cs                        # INavigationProvider
└── Permissions.cs                      # IPermissionProvider

src/AutonomiaSaaS.Modules.BusinessCore/
└── Services/IApprovedTaskExecutor.cs   # Interface + ApprovedTaskDispatcher (novo)

src/AutonomiaSaaS.Modules.AcquisitionAgent/
└── Services/AcquisitionAgentApprovedTaskExecutor.cs   # Implementação concreta

tests/AutonomiaSaaS.Modules.BusinessCore.Tests/
└── ApprovedTaskDispatcherTests.cs      # 3 testes

tests/AutonomiaSaaS.Modules.AcquisitionAgent.Tests/
└── AcquisitionAgentApprovedTaskExecutorTests.cs   # 3 testes, incluindo o bug de casing
```

## ⚠️ Avisos de risco de compilação, do mais para o menos grave

1. **`.csproj` do `RiskGate` mudou de `Sdk="Microsoft.NET.Sdk"` para
   `Sdk="Microsoft.NET.Sdk.Razor"`**, com `OrchardCore.Module.Targets` como
   pacote novo. Esse é o padrão usual de projeto de módulo Orchard Core com
   Admin UI, mas é a mudança de maior risco desta sessão inteira — não tenho
   como confirmar sem compilar que o nome do pacote e a versão (`2.1.0`,
   mesmo placeholder de sempre) estão certos.
2. **`ApprovalController.MapToViewModel` usa `part.ContentItem.ContentItemId`**
   para obter o `TaskId` a partir de um `AgentTaskPart`. Tenho boa confiança
   que `ContentPart` expõe uma propriedade `ContentItem` de volta para o item
   dono, mas não confirmei o nome exato dela nesta versão do Orchard Core.
3. **`[Admin]` vem de `OrchardCore.Admin.Abstractions`** e o roteamento por
   área (`area = "AutonomiaSaaS.Modules.RiskGate"` no `AdminMenu`) assume a
   convenção de que cada módulo Orchard Core já é automaticamente uma MVC
   Area nomeada como o próprio módulo — não adicionei um atributo `[Area]`
   explícito no controller porque, pelo padrão usual do framework, isso não
   deveria ser necessário, mas não é algo que eu tenha validado por
   compilação real.
4. **`IStringLocalizer<T>` e o padrão `S["texto"]`** para textos localizáveis
   é o padrão idiomático do Orchard Core, mas os textos aqui estão direto em
   português — funciona para um único idioma sem nenhum arquivo `.po`
   adicional, só não aproveita a localização de verdade ainda.

## Como testar o fluxo completo depois de compilar

1. Rodar o host, criar um tenant, ativar os módulos.
2. Chamar `AcquisitionAgentOrchestrator.GenerateLandingPageAsync` (hoje só
   via código/teste manual — não existe endpoint público ainda para o
   usuário final disparar isso digitando a descrição do negócio).
3. Acessar `/Admin` → "Autonomia SaaS" → "Aprovações" e ver a tarefa de
   `deploy_producao` esperando.
4. Clicar "Aprovar" e confirmar, no log, que
   `AcquisitionAgentApprovedTaskExecutor.ExecuteApprovedTaskAsync` foi
   chamado e a tarefa terminou em `Concluida` (ou `Revertida`, se o
   `VercelDeploymentClient` falhar — lembrando que essa classe ainda não foi
   validada contra a API real da Vercel).

## O que ainda falta depois disso

- ~~Endpoint/UI para o usuário final descrever o negócio e disparar
  `GenerateLandingPageAsync`~~ — feito. Ver seção abaixo.
- `IBackgroundTask` de expiração de aprovação (usa
  `GetExpiredApprovalsAsync`, que já existe e funciona, mas nada chama isso
  em ciclo).
- Ligação com `OrchardCore.Workflows` de verdade, se algum dia fizer sentido
  trocar os dois métodos públicos do orquestrador por atividades visuais
  suspensas/retomadas.

---

# Endpoint público: `LandingPageController` (módulo AcquisitionAgent)

Fecha o critério de sucesso da Fase 1 (seção 10 do documento de arquitetura):
"usuário descreve o negócio → recebe uma landing page publicada e funcional
sem tocar em código". Diferente de `ApprovalController` (módulo `RiskGate`,
com `[Admin]`), este é voltado para o usuário final do SaaS — usa
`[Authorize]` simples, sem permissão dedicada.

## Por que `[Authorize]` sem uma Permission específica

Simplificação deliberada da Fase 1: qualquer usuário autenticado no tenant
pode gerar uma landing page para o próprio negócio. Como cada tenant já é um
cliente isolado do SaaS (seção 1 da especificação técnica), não existe
"negócio de outra pessoa" para vazar dentro do mesmo tenant ainda. Uma
permissão dedicada (como `RiskGatePermissions.ManageApprovals`) passa a fazer
sentido a partir do momento em que um tenant tiver mais de um usuário com
papéis diferentes — não é o caso da Fase 1.

## Estrutura nova

```
src/AutonomiaSaaS.Modules.AcquisitionAgent/
├── Controllers/LandingPageController.cs   # Index (form) + Generate (POST)
├── ViewModels/LandingPageViewModels.cs
├── Views/
│   ├── _ViewImports.cshtml
│   └── LandingPage/
│       ├── Index.cshtml                    # Formulário de descrição do negócio
│       └── Result.cshtml                   # Link de staging + aviso de aprovação pendente
└── MainMenu.cs                             # INavigationProvider do menu "main" (não Admin)
```

## Decisão de tratamento de erro

`Generate` tem um `try/catch` em volta da chamada ao orquestrador que **não
existe** no `ApprovalController`. Motivo: uma falha aqui (ex: chave de API da
Anthropic ausente) acontece **antes** de qualquer `AgentTask` existir — não
há tarefa nenhuma para apontar no painel de erros, então a mensagem precisa
aparecer direto pro usuário nesta tela, não só no log. Já uma falha dentro do
fluxo de aprovação sempre tem uma tarefa existente para registrar o que
aconteceu (`Revertida`), por isso o tratamento lá é mais simples.

## Mesmos avisos de risco de compilação do `RiskGate`

O `.csproj` mudou para `Sdk="Microsoft.NET.Sdk.Razor"` com
`AddRazorSupportForMvc` e `OrchardCore.Module.Targets`, pelo mesmo motivo e
com o mesmo nível de incerteza já registrado na seção do Painel de Aprovação.
Um ponto novo aqui: `LandingPageController` injeta `ShellSettings` (de
`OrchardCore.Environment.Shell`) para obter o nome do tenant atual como
`TenantId` — tenho confiança razoável de que esse tipo existe e se comporta
assim, mas não confirmei se ele já vem transitivamente com
`OrchardCore.Module.Targets` ou se precisaria de um pacote adicional
explícito.

## Fluxo completo agora, de ponta a ponta

1. Usuário logado acessa "Gerar landing page" no menu principal do site.
2. Descreve o negócio, clica em gerar.
3. `AcquisitionAgentOrchestrator.GenerateLandingPageAsync` roda os passos 1-6
   (seção 5 da especificação técnica): gera a landing page, propõe a tarefa
   de baixo risco (auto-executa), faz deploy em staging, verifica saúde, e
   propõe a tarefa de alto risco de promoção para produção.
4. Usuário vê o link de staging e um aviso claro: falta aprovação humana
   para publicar de verdade.
5. Um administrador acessa `/Admin` → "Autonomia SaaS" → "Aprovações" e
   aprova ou rejeita.
6. Se aprovar, `ApprovedTaskDispatcher` chama de volta
   `AcquisitionAgentApprovedTaskExecutor`, que chama
   `PromoteToProductionAsync` — promove de verdade, ou reverte em caso de
   falha.

Esse é o loop inteiro da Fase 1 fechado, do texto digitado pelo usuário até a
decisão humana de publicar. O que resta (expiração automática, Workflows
visuais) são refinamentos de robustez, não bloqueadores do fluxo principal.
