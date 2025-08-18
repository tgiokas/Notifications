using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Configuration;
using Notifications.Infrastructure.Messaging;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;


var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IKafkaPublisher<string, string>, KafkaPublisher<string, string>>();
//builder.Services.AddSingleton<KafkaEmailPublisher1>();
//builder.Services.AddSingleton<IKafkaPublisher<string, NotificationRequestDto>, KafkaPublisher<string, NotificationRequestDto>>();

// Register of KafkaConsumer
builder.Services.AddHostedService<KafkaEmailConsumer<string, string>>();
//builder.Services.AddHostedService<KafkaEmailConsumer1>();

// Application layer
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

// Hosted background services (RabbitMQ consumers per channel)
//builder.Services.AddHostedService<RabbitMqEmailConsumer>();

builder.Services.AddControllers();

//builder.Services.AddSwaggerGen();

//builder.Services.AddSwaggerGen(x =>
//{
//    x.SwaggerDoc("v1", new OpenApiInfo { Title = "Api", Version = "v1" });
//    x.AddSecurityDefinition("Bearer ", new OpenApiSecurityScheme
//    {
//        Description = "JWT Authorization header using the Bearer scheme.",
//        Name = "Authorization",
//        In = ParameterLocation.Header,
//        Type = SecuritySchemeType.ApiKey,
//        Scheme = "Bearer "
//    });
//    x.AddSecurityRequirement(new OpenApiSecurityRequirement
//                {
//                    {
//                        new OpenApiSecurityScheme
//                        {
//                            Reference = new OpenApiReference
//                            {
//                                Type = ReferenceType.SecurityScheme,
//                                Id = "Bearer "
//                            },
//                            Scheme = "oauth2",
//                            Name = "Bearer ",
//                            In = ParameterLocation.Header
//                        },
//                        new List<string>()
//                    }
//                });
//});


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


