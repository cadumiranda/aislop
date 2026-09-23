using AutonomiaSaaS.Modules.Sandbox.Abstractions;
using AutonomiaSaaS.Modules.Sandbox.Execution;
using AutonomiaSaaS.Modules.Sandbox.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace AutonomiaSaaS.Modules.Sandbox;

/// <summary>
/// Segue a mesma convenção dos demais módulos (StartupBase + registro em ConfigureServices) —
/// ver spec v2.0, seção 2, sobre por que AgentRuntime precisou virar um módulo Orchard Core de
/// verdade para ser resolvido pelo container de DI. Aplicando a mesma lição aqui desde o início.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<DockerSandboxOptions>();

        // NOTA: revisar contra o padrão real de configuração usado no restante da solução
        // (appsettings / User Secrets, ver AgentRuntimeOptions na spec v2.0 seção 7) — aqui uso
        // IConfiguration diretamente por não ter acesso ao padrão de Options real do projeto.
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var options = new DockerSandboxOptions();
            configuration.GetSection("Sandbox:Docker").Bind(options);
            return options;
        });

        services.AddSingleton<ILogger<DockerSandboxRunner>, ILogger<DockerSandboxRunner>>();
        services.AddSingleton<IProcessInvoker, ProcessInvoker>();
        services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();
        services.AddSingleton<ISandboxExecutionQueue, InMemorySandboxExecutionQueue>();
        services.AddSingleton<ISandboxExecutionResultSink, LoggingSandboxExecutionResultSink>();
        services.AddHostedService<SandboxExecutionHostedService>();

        services.AddScoped<IModularTenantEvents, MyStartupTaskService>();
    }
}
