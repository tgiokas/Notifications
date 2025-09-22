using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Configuration;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Messaging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

builder.Logging.SetMinimumLevel(LogLevel.Information);

var logger = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
}).CreateLogger("Startup");

logger.LogInformation("Starting Notifications.Worker...");

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// Infrastructure (DB, Kafka config, etc.)
builder.Services.AddInfrastructureServices(builder.Configuration, "postgresql");

// Consumer
builder.Services.AddHostedService<KafkaEmailConsumer>();

// Application layer
builder.Services.AddScoped<IEmailSender, EmailSender>();

IHost host = builder.Build();

logger.LogInformation("Services configured. Notifications.Worker is running...");

await host.RunAsync();