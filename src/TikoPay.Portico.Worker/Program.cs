using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Worker;
using TikoPay.Portico.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddPorticoPersistence(builder.Configuration);
builder.Services.AddPorticoRealtimeDispatch(builder.Configuration);
builder.Services.Configure<PorticoMessageBusOptions>(
    builder.Configuration.GetSection(PorticoMessageBusOptions.SectionName));
builder.Services.AddSingleton<IPorticoMessageBus, PorticoRabbitMqMessageBus>();

var host = builder.Build();
host.Run();
