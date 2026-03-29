using Microsoft.EntityFrameworkCore;

using DotNetEnv;
using Serilog;

using Notifications.Api.Middlewares;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;
using Notifications.Infrastructure;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Database;
using Notifications.Infrastructure.Messaging;

try
{
    Env.Load();
    Env.TraversePath().Load();

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .CreateLogger();

    Log.Information("Configuration is starting...");

    builder.Host.UseSerilog();

    // Add Kafka Consumer
    builder.Services.AddHostedService<KafkaEmailConsumer>();

    // Register Database Context
    builder.Services.AddInfrastructureServices(builder.Configuration, "postgresql");

    // Add Application Services
    builder.Services.AddScoped<IEmailSender, EmailSenderFactory>();
    builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
    builder.Services.AddScoped<ISendGridEventHandler, SendGridEventHandler>();

    // Seed filesystem templates into DB on first run
    builder.Services.AddHostedService<EmailTemplateSeedService>();

    builder.Services.AddControllers();

    builder.Services.Configure<HostOptions>(options =>
    {
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    // Add CORS policy
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policyBuilder =>
        {
            policyBuilder.AllowAnyOrigin();
            policyBuilder.AllowAnyMethod();
            policyBuilder.AllowAnyHeader();
        });
    });

    // Add Swagger for development
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    var app = builder.Build();

    Log.Information("Application is starting...");

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    Log.Information("Database migrations applied (if any).");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("CorsPolicy");
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<LogMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{   
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
