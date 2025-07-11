using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using RabbitMQ.Client;


using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Configuration;
using Notifications.Infrastructure.Messaging;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplicationServices();

//builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
//builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Register Database Context
builder.Services.AddInfrastructureServices(builder.Configuration, "postgresql");

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configuration binding
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMqSettings"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaSettings"));

// RabbitMQ publisher
//builder.Services.AddSingleton<RabbitMqPublisher>(); // Needed to call InitializeAsync()
//builder.Services.AddSingleton<IRabbitMqPublisher>(provider =>
//{
//    var publisher = provider.GetRequiredService<RabbitMqPublisher>();
//    publisher.InitializeAsync().GetAwaiter().GetResult();
//    return publisher;
//});

// KafkaPublisher
builder.Services.AddSingleton<KafkaPublisher>();
builder.Services.AddSingleton<IKafkaPublisher>(provider => provider.GetRequiredService<KafkaPublisher>());

// Register Kafka producer service
builder.Services.AddSingleton<KafkaProducerService<string, string>>();

// Register Kafka consumer service
builder.Services.AddSingleton<MessageStoreService<string>>();
builder.Services.AddHostedService<KafkaConsumerService<string, string>>();


// Application layer
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();

// Email sender (choose option 1 or 2 depending on where IEmailSender lives)
builder.Services.AddScoped<IEmailSender, EmailSender>();

// Hosted background services (RabbitMQ consumers per channel)
//builder.Services.AddHostedService<RabbitMqEmailConsumer>();

builder.Services.AddControllers();

//builder.Services.AddSwaggerGen();

//builder.Services.AddKeycloakAuthentication(Config);

//builder.Services.AddSingleton<IKeycloakUserManagement, KeycloakUserManagement>();

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

// Configure Authentication & Keycloak JWT Bearer
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options =>
//    {
//        options.Authority = builder.Configuration["Keycloak:Authority"];
//        options.Audience = builder.Configuration["Keycloak:ClientId"];
//        options.RequireHttpsMetadata = false; // Only for local dev
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = true,
//            ValidIssuer = builder.Configuration["Keycloak:Authority"],            
//            ValidateAudience = true,
//            ValidAudiences = new[] { "dms-auth-client", "dms-service-client", "dms-admin-client"}, // Allow multiple audiences
//            ValidateLifetime = true,
//            ValidateIssuerSigningKey = true,
//            //RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
//            //RoleClaimType = "realm_access.roles",
//            //NameClaimType = "preferred_username"
//        };

//        // Extract roles from `realm_access` JSON object using System.Text.Json
//        options.Events = new JwtBearerEvents
//        {
//            OnTokenValidated = context =>
//            {
//                MapKeycloakRolesToRoleClaims(context);
//                return Task.CompletedTask;
//            }
//        };

//    });

////Ensure Role-Based Access Control (RBAC) uses the correct claim mapping
//builder.Services.AddAuthorizationBuilder()
//    .AddPolicy("AdminOnly", policy => policy.RequireRole("admin"))
//    .AddPolicy("UserOnly", policy => policy.RequireRole("user"))
//    .AddPolicy("AdminOrUser", policy => policy.RequireRole("admin", "user"));

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


