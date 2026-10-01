using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AutonomiaSaaS.IntegrationTests;

public sealed class OrchardCoreMinimalHostFixture : IDisposable
{
    public IHost Host { get; }
    public OrchardCoreMinimalHostFixture()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseTestServer();
                web.UseStartup<AutonomiaSaaS.TestStartup>();
            });

        Host = builder.Start();
    }

    public void Dispose()
    {
        Host.Dispose();
    }
}
