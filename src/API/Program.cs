using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Configuration;
using Notifications.Infrastructure.Messaging;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;


var builder = WebApplication.CreateBuilder(args);

//builder.Host.ConfigureHostOptions(o =>
//{
//    // Make sure an unexpected exception in a BackgroundService
//    // doesn't bring the whole host down.
//    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
//});


// Add services to the container.
//builder.Services.AddApplicationServices();

// Register Database Context
builder.Services.AddInfrastructureServices(builder.Configuration, "postgresql");

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuration binding
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaSettings"));


// Register of KafkaPublisher and IKafkaPublisher
builder.Services.AddScoped<IKafkaPublisher<string, string>, KafkaPublisher<string, string>>();

builder.Services.AddSingleton<KafkaPublisher0<string, string>>();
//builder.Services.AddSingleton<KafkaEmailPublisher1>();
//builder.Services.AddSingleton<IKafkaPublisher<string, NotificationRequestDto>, KafkaPublisher<string, NotificationRequestDto>>();


//builder.Services.AddScoped<IKafkaPublisher<string, string>, KafkaPublisherImplementation>();


// Register of KafkaConsumer
builder.Services.AddHostedService<KafkaEmailConsumer<string, string>>();
//builder.Services.AddHostedService<KafkaEmailConsumer1>();

// Application layer
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddControllers();

//builder.Services.AddSwaggerGen();

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

builder.WebHost.UseUrls("http://0.0.0.0:80");

var app = builder.Build();

app.UseCors("CorsPolicy");

if (app.Environment.IsDevelopment())
{
    //    app.UseSwagger();
    //    app.UseSwaggerUI();
    //using var scope = app.Services.CreateScope();
    //var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    //dbContext.Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


