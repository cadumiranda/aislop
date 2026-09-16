# `VercelDeploymentClient` — adição ao `AutonomiaSaaS.Modules.AcquisitionAgent`

Fecha o `[PENDENTE]` da spec v2.0, seção 9.1: `VercelDeploymentClient` nunca foi validado contra
a API real. Esta pasta implementa o cliente contra endpoints confirmados da API da Vercel — mas
leia o aviso abaixo antes de qualquer coisa.

## ⚠️ Aviso mais importante desta entrega: eu não consegui validar isto contra a Vercel real

O ambiente onde rodo tem a rede restrita a uma lista de domínios permitidos (registro npm, PyPI,
GitHub, crates.io, API da Anthropic) — **`api.vercel.com` não está nessa lista**, e eu não tenho
como pedir para isso ser adicionado. Ou seja: **nenhuma chamada HTTP deste código foi de fato
executada contra a Vercel por mim.** O item "rodar o fluxo completo contra um projeto de teste"
do plano original (passo 5) não pode ser feito por mim — só por vocês, no ambiente de vocês, com
um token real. O que eu fiz foi:

1. Confirmar os endpoints e verbos via busca na documentação oficial da Vercel (não inventados).
2. Escrever o cliente contra esses endpoints, com testes unitários que verificam a URL, o verbo,
   o header de autorização e o tratamento de resposta — usando um `HttpMessageHandler` fake, não
   uma chamada de rede real.
3. Deixar um projeto de teste de integração separado (`IntegrationTests/`), **desabilitado por
   padrão** (`[Fact(Skip = ...)]`), que só roda se você definir `VERCEL_TEST_TOKEN` e
   `VERCEL_TEST_PROJECT_ID` no ambiente — para vocês rodarem isso vocês mesmos, contra o projeto
   de teste isolado que já discutimos (passo 1 do plano original).

## Endpoints confirmados (via documentação oficial `vercel.com/docs/rest-api`)

| Operação | Método | Endpoint |
|---|---|---|
| Criar deployment (staging/preview) | `POST` | `/v13/deployments` |
| Consultar status de um deployment | `GET` | `/v13/deployments/{idOrUrl}` |
| Promover deployment para produção | `POST` | `/v10/projects/{projectId}/promote/{deploymentId}` (não reconstrói o build) |
| Reverter produção para deployment anterior | `POST` | `/v1/projects/{projectId}/rollback/{deploymentId}` |

O `readyState` de um deployment transiciona `QUEUED → INITIALIZING → BUILDING → READY` (sucesso)
ou `ERROR` — confirmado na documentação de criação de deployment.

## ⚠️ Segundo aviso: o schema exato do corpo de `POST /v13/deployments` é parcialmente inferido

A documentação que consegui buscar confirma os campos de alto nível (`name`, `files`, `target`,
`projectSettings`) e que arquivos pequenos podem ser embutidos diretamente no corpo da requisição
("inline small files directly in the request body"), mas não me trouxe o schema JSON completo
campo a campo. Implementei o caminho de "arquivo pequeno embutido" (`files: [{ file, data }]`),
adequado para uma landing page de um único HTML — **arquivos maiores precisam do fluxo de upload
por SHA (endpoint separado de upload de arquivo), que não foi implementado aqui.** Antes de usar
isso contra o projeto de teste, rode manualmente um `curl` ou o teste de integração incluído e
confira a resposta real contra o que o código espera — é exatamente para isso que o teste de
integração existe.

## Como o cofre de credenciais entra aqui — primeiro uso real do `ICredentialVault`

`VercelAuthenticationHandler` (um `DelegatingHandler`) busca o token via
`ICredentialVault.TryGetAsync("acquisition_agent", "vercel_deploy_token")` a cada requisição —
não fica em `appsettings.json` nem é fixado uma vez no `HttpClient` na inicialização. Isso
significa: gravar esse token pela UI de admin do cofre (`/Admin/AgentCredentialsAdmin/Create`)
é literalmente o primeiro passo funcional para este cliente funcionar — não tem outro jeito de
configurá-lo.

Se `TryGetAsync` devolver `null` (token não cadastrado, ou cofre não conseguiu decifrar), o
handler lança `InvalidOperationException` imediatamente, antes de qualquer chamada de rede —
falha alta e clara, não uma chamada 401 confusa vinda da Vercel.

## O que foi assumido sobre `IDeploymentClient`

Mesma situação de sempre: a spec confirma que a interface existe e cita três métodos por nome
(`DeployToStagingAsync`, `CheckDeploymentAsync`, `RollbackToPreviousAsync`) mas não a assinatura
completa. `Shims/AcquisitionAgentShims.cs` contém o contrato inferido — inclui um quarto método
(`PromoteToProductionAsync`) que a spec não nomeia explicitamente para o *client* (só para o
*orquestrador*, que é uma classe diferente) mas que precisa existir em algum lugar para o passo
"promove de fato" (seção 5, item 2) funcionar. Apague o shim e ajuste contra o `IDeploymentClient`
real ao integrar.

## Timeout e polling de `CheckDeploymentAsync`

A Vercel não tem webhook síncrono embutido nesta chamada — checar "ficou pronto" é poll do `GET
/v13/deployments/{id}` até `READY`/`ERROR`/`CANCELED` ou até estourar um timeout configurável
(`VercelApiOptions.HealthCheckTimeout`, default 3 minutos, com intervalo de 3 segundos entre
tentativas). Isso é uma escolha de design, não algo documentado pela Vercel — ajuste os valores
conforme o tempo de build real dos seus projetos.
