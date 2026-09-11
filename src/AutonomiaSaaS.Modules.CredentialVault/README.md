# AutonomiaSaaS.Modules.CredentialVault

Fecha a lacuna do cofre de credenciais (token de deploy da Vercel, chaves de API de terceiros)
usando a infraestrutura que o próprio Orchard Core já confia para o mesmo tipo de problema —
sem reinventar criptografia.

## O que reaproveitamos do Orchard Core (não inventamos nada aqui)

- **`Microsoft.AspNetCore.DataProtection` / `IDataProtectionProvider`** — a mesma API que o
  módulo `OrchardCore.Email.Smtp` usa para guardar a senha do SMTP cifrada em repouso (a
  documentação chama isso literalmente de "persisted secrets that need to be decrypted, e.g.
  SMTP passwords"). É o precedente exato do nosso caso.
- **Isolamento por tenant nativo.** A documentação de Data Protection do Orchard Core confirma
  que cada tenant tem sua própria pasta de chaves por padrão
  (`App_Data/Sites/{Tenant}/DataProtection-Keys`). A credencial de um tenant não pode ser
  decifrada com a chave de outro — isso não é algo que este código precisou implementar, é
  comportamento nativo do host.
- **YesSql (`ISession`)** — mesmo mecanismo de persistência que `AgentTaskPartIndex` já usa
  (spec v2.0, seção 3.2). Guardamos o registro da credencial como documento YesSql com um
  índice próprio (`AgentCredentialIndex`), não como Content Type — decisão explicada abaixo.

## Por que documento YesSql, e não Content Type

`AgentTask`/`BusinessContext` são Content Types porque fazem sentido como conteúdo editorial
(aparecem no admin, têm ciclo de vida de publicação). Uma credencial não deveria ter tela de
edição "bonita" nem versionamento de conteúdo — quanto menos superfície de UI em cima de um
segredo, melhor. Um documento YesSql puro, sem Content Type por trás, evita:
- Expor a credencial (ainda que cifrada) na tela genérica de conteúdo do admin
- Precisar de Content Definition/Migration de Content Type só para isto

## Decisão de design: uma "purpose" de proteção por agente

`IDataProtector` é criado via `CreateProtector(purpose, subPurposes...)`. Usamos
`CreateProtector("AutonomiaSaaS.CredentialVault", agentName)` — ou seja, **a credencial só pode
ser decifrada no contexto do agente para o qual foi gravada**. Se por engano o `AgentName` de um
registro for alterado sem regravar o valor, `Unprotect` lança `CryptographicException` em vez de
devolver lixo — falha alto, não silenciosa. Isso é o equivalente, em criptografia, ao princípio
de "ferramentas limitadas ao domínio do agente" que já vale para o sandbox.

## O que foi assumido (sem acesso ao projeto real)

Só tenho a spec e os documentos de arquitetura, não o `AutonomiaSaaS.Modules.BusinessCore` real.
Duas coisas específicas deste módulo que dependem de convenção do Orchard Core e não pude
validar contra o `.sln` real:

- **Registro de `IIndexProvider`** — assumi `services.AddIndexProvider<AgentCredentialIndexProvider>()`
  como a chamada correta (é a convenção usada por outros módulos Orchard Core que geram índice
  próprio). Confirme contra o padrão real usado por `AgentTaskPartIndex` no `BusinessCore`.
- **Descoberta de `DataMigration`** — Orchard Core normalmente descobre classes `DataMigration`
  por convenção dentro do módulo, sem registro explícito em `Startup.cs`. Não adicionei nenhuma
  chamada de registro para a migration por causa disso — se o projeto real usa um padrão
  diferente, ajuste.
- **`YesSqlAgentCredentialRecordStore`** não foi testado contra um `ISession` real (só contra o
  fake `IAgentCredentialRecordStore` nos testes) — a extração dessa interface foi deliberada
  (ver seção de testes) exatamente para isolar essa incerteza da lógica de criptografia, que
  essa sim está testada de ponta a ponta com o `IDataProtectionProvider` real (efêmero, via
  `DataProtectionProvider.Create(...)`), não um fake.

## O que este módulo NÃO faz

- Não gerencia rotação de chave mestra de Data Protection em si (isso é infraestrutura do host,
  já coberta pela seção "Distributed Data Protection" da documentação — Redis ou Azure Blob
  Storage quando escalar para múltiplas instâncias).
- Não expõe UI de admin para cadastrar credenciais — só a API (`ICredentialVault`). Se quiser uma
  tela, é uma camada fina de Controller por cima disto, não uma mudança no vault em si.
