# Imagem de exemplo para o domínio "Agente de Produto" (build de código gerado em Node/React).
# Deliberadamente mínima e específica de domínio — o doc de arquitetura (seção 3) é explícito que
# sandbox significa disco, RAM e FERRAMENTAS limitados ao domínio do agente, não uma imagem
# "canivete suíço" com toda ferramenta disponível para qualquer agente.
#
# Um domínio diferente (ex: futuro agente de scraping/pesquisa de concorrência) deveria ter sua
# própria imagem (ex: agent-sandbox-python.Dockerfile com só requests/beautifulsoup), não esta.

FROM node:20-slim

# Sem shell interativo, sem ferramentas de rede além do necessário para npm install
# (a rede em si só é habilitada por execução via SandboxExecutionRequest.NetworkMode).
RUN apt-get update \
    && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*

# Usuário não-root — mesmo com --read-only e --cap-drop ALL no `docker run`, rodar como root
# dentro do container é uma camada de defesa a menos sem necessidade.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin sandboxuser
USER sandboxuser

WORKDIR /workspace
