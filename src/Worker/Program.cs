using DotNetEnv;

using Serilog;

using Notifications.Application.Interfaces;
using Notifications.Application.Services;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Messaging;

Env.Load();
Env.TraversePath().Load();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

Log.Information("Starting Notifications.Worker...");

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// Consumer
builder.Services.AddHostedService<KafkaEmailConsumer>();

builder.Services.AddScoped<IEmailSender, EmailSenderFactory>();

builder.Services.AddScoped<ITemplateService, TemplateService>();

IHost host = builder.Build();

Log.Information("Services configured. Notifications.Worker is running...");  

await host.RunAsync();