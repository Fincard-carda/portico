using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Worker;
using TikoPay.Portico.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddPorticoPersistence(builder.Configuration);
builder.Services.AddPorticoRealtimeDispatch(builder.Configuration);

var host = builder.Build();
host.Run();
