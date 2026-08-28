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

`AcquisitionAgent` — agora existem as três peças que ele precisa consumir:
`IAgentRuntime` (chamar o modelo), `ITaskRiskGateway` (propor a ação com risco
já resolvido) e `IAgentTaskStore`/`AgentTaskStateMachine` (acompanhar o ciclo
de vida da tarefa). É o módulo que fecha o loop completo da Fase 1 descrito
na seção 5 da especificação técnica.
