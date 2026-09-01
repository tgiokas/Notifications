using DotNetEnv;
using Serilog;

using Notifications.Application;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
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

// Add Application services
builder.Services.AddApplicationServices();

// Add Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration, "postgresql");

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

// No migration tooling in this pipeline; EnsureCreated is sufficient for the
// single outbox table and is a no-op once the schema already exists.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();