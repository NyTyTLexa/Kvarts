using ProcurementSystem.Infrastructure;
using ProcurementSystem.Infrastructure.Observability;
using ProcurementSystem.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Наблюдаемость (Этап 6): те же логи/трассы, что и в Api — общий код в Infrastructure
builder.AddObservability("procurement-worker");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
