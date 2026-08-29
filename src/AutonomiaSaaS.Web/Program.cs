var builder = WebApplication.CreateBuilder(args);

// AddOrchardCms() é o ponto de entrada padrão do Orchard Core: ele descobre
// automaticamente todo assembly referenciado que tenha um [assembly: Module]
// (Manifest.cs) e chama o ConfigureServices de cada Startup.cs correspondente
// — é assim que os quatro módulos (AgentRuntime, BusinessCore, RiskGate,
// AcquisitionAgent) entram no sistema, só por serem referenciados no .csproj
// deste projeto. Não precisa registrar nada manualmente aqui.
builder.Services.AddOrchardCms();

var app = builder.Build();

app.UseOrchardCore();

app.Run();
