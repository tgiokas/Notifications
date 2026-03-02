using DotNetEnv;
using Serilog;

using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Messaging;
using Notifications.Application.Services;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Api.Middlewares;

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

// Add Application Services
builder.Services.AddScoped<IEmailSender, EmailSenderFactory>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ISendGridEventHandler, SendGridEventHandler>();

builder.Services.AddControllers();

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