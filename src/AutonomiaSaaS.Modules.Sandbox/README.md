# AutonomiaSaaS.Modules.Sandbox

Implementa a peça **[ADIADO]** registrada na seção 4 da especificação técnica v2.0:
isolamento por container para qualquer ação de agente que precise executar
ferramentas (build, comandos de shell, manipulação de arquivo) fora do processo
do host Orchard Core.

## Como ler isto (mesmo estilo de honestidade da spec v2.0)

Este módulo foi escrito **sem acesso ao restante da solução real**
(`AutonomiaSaaS.Modules.AcquisitionAgent`, `BusinessCore`, etc. não foram
anexados nesta conversa — só os dois documentos de arquitetura/spec). Por isso:

- Os nomes de classe e a estrutura seguem exatamente as convenções documentadas
  (`Manifest.cs`, `Startup.cs`, injeção via `IServiceCollection`), mas **não foi
  compilado contra o `.sln` real** — o mesmo motivo pelo qual a v2.0 evitou
  arriscar `IWorkflowManager` sem compilar contra o pacote real se aplica aqui.
  Revise nomes de namespace/projeto contra o `AutonomiaSaaS.sln` real antes de
  colar isso dentro da solução.
- **Não inventei integração direta com `AcquisitionAgentOrchestrator`** porque
  não tenho o arquivo fonte dele. O que a Fase 1 faz hoje (gerar HTML/JSON via
  `AgentRuntime` e mandar deploy pra Vercel/`LocalFakeDeploymentClient`) **não
  executa nenhum código não confiável localmente** — a chamada ao modelo é só
  HTTP, e o deploy é uma chamada de API. Ou seja: **a Fase 1 de Aquisição, hoje,
  não tem nenhum passo que precise de sandbox de verdade.** A seção "Onde isso
  entra de fato" abaixo explica quando isso passa a importar.
- Segui a decisão da v2.0 de não confiar num SDK que eu não consigo validar
  (o motivo pelo qual eles preferiram wrapper HTTP fino ao invés de assumir a
  API de `IWorkflowManager`). Por isso o runner aqui invoca o **CLI do Docker
  via `Process`** em vez de `Docker.DotNet` — é mais fácil de auditar, o
  comportamento é idêntico a rodar `docker run` manualmente no terminal, e não
  depende de eu acertar de memória a superfície de uma SDK.

## Onde isso entra de fato

A arquitetura (seção 2) já reserva lugar para dois agentes que **vão** precisar
disto de verdade:

- **Agente de Produto** (seção 16.3 / futuro "app companion") — quando o
  entregável é código gerado que precisa rodar `npm install`, `dotnet build`,
  ou qualquer step de build antes do deploy. Esse é o primeiro consumidor real.
- **Agente de Ads / Pesquisa de Concorrência** — quando passarem a rodar
  scraping ou scripts de terceiros (ver seção 12 do doc de arquitetura sobre
  termos de uso), rodar isso fora do processo do host deixa de ser opcional.

Para a Fase 1 atual, este módulo fica pronto e testado, mas **sem nenhum
chamador ainda** — exatamente como `AgentTaskPart.WorkflowInstanceId` ficou no
schema sem uso depois que o Elsa/OrchardCore.Workflows foi abandonado. Não é
desperdício: é a peça de segurança que a v2.0 já sinalizou como a mais séria
lacuna em aberto antes de operar com clientes reais e múltiplos tenants.

## Decisões de design (estilo Polsia, adaptado)

O fundador do Polsia relata que, sem contenção, "agentes encontram um jeito de
manipular coisas fora do escopo pretendido conforme o sistema escala" — e que
o `SANDBOX_MODE` do próprio Polsia publicado é só uma flag booleana, não
isolamento de processo real (ver conversa anterior). Este módulo tenta fazer o
isolamento de verdade, não a flag:

| Decisão | Motivo |
|---|---|
| Container efêmero por execução (`--rm`), nunca reaproveitado | Cada tarefa começa de um estado limpo — nenhum resíduo de execução anterior pode vazar para a próxima |
| `--read-only` no rootfs + `--tmpfs /tmp` | O container não pode escrever em si mesmo; só pode escrever no volume de workspace explicitamente montado |
| Um diretório de workspace por execução, montado como único volume gravável | Isola arquivos de uma tarefa de outra — nunca um diretório compartilhado entre tarefas |
| `--network` configurável por chamada, default `none` | Ferramenta de agente só tem rede se a tarefa especificamente precisar (ex: `npm install`); a chamada ao modelo (Anthropic) continua acontecendo no host, fora do container |
| `--memory`, `--cpus`, `--pids-limit` obrigatórios (sem default "ilimitado") | Runaway de um agente não pode afetar o host nem outros tenants — mesmo problema de custo que o Polsia relatou, só que em recurso de máquina em vez de dinheiro de API |
| Timeout de host mata o processo/container | Um agente preso em loop não fica rodando indefinidamente — precisa de teto físico, não só a política do `IModelRouter` |
| Imagem por família de ferramenta, não uma imagem "genérica com tudo" | Ferramentas limitadas ao domínio do agente (seção 3 do doc de arquitetura: sandbox = disco, RAM **e ferramentas** limitados) |
