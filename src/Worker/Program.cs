using Microsoft.Extensions.Logging;

using Notifications.Infrastructure.Configuration;
using Notifications.Infrastructure.Messaging;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.ExternalServices;

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


// Infrastructure (DB, Kafka config, etc.)
builder.Services.AddInfrastructureServices(builder.Configuration, "postgresql");
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// Consumer
builder.Services.AddHostedService<KafkaEmailConsumer>();

// Application layer
builder.Services.AddScoped<IEmailSender, EmailSender>();


IHost host = builder.Build();

logger.LogInformation("Services configured. Notifications.Worker is running...");

await host.RunAsync();
