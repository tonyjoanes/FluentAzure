using System.Text;
using FluentAzure;
using FluentAzure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebApi.Example.Configuration;
using WebApi.Example.Data;
using WebApi.Example.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure FluentAzure with strongly-typed configuration
var buildResult = await FluentConfig
    .Create()
    .FromJsonFile("appsettings.json")
    .FromEnvironment()
    .FromKeyVault(builder.Configuration["KeyVault:Url"])
    .Required("ConnectionStrings:DefaultConnection")
    .Required("ConnectionStrings:StorageConnection")
    .Required("ConnectionStrings:ServiceBusConnection")
    .Required("Jwt:SecretKey")
    .Required("Jwt:Issuer")
    .Required("Jwt:Audience")
    .Optional("Logging:LogLevel:Default", "Information")
    .Optional("AllowedHosts", "*")
    .Optional("Cors:AllowedOrigins", "http://localhost:3000")
    .BuildAsync();

var configResult = buildResult.Bind<WebApiConfiguration>();

var config = configResult.Match(
    success =>
    {
        builder.Services.AddSingleton(success);
        return success;
    },
    errors =>
    {
        var errorMessage = string.Join(", ", errors);
        throw new InvalidOperationException($"Configuration failed: {errorMessage}");
    }
);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Web API Example",
            Version = "v1",
            Description = "Example Web API demonstrating FluentAzure configuration",
        }
    );

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
        }
    );

    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

// Configure Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(config.Database.ConnectionString)
);

// Configure Authentication
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config.Jwt.Issuer,
            ValidAudience = config.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config.Jwt.SecretKey)
            ),
        };
    });

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowedOrigins",
        policy =>
        {
            policy
                .WithOrigins(config.Cors.AllowedOrigins.Split(','))
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    );
});

// Register application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();

    if (config.Telemetry.EnableTelemetry)
    {
        logging.AddApplicationInsights();
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web API Example v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowedOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Log configuration summary
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ðŸš€ Web API started with configuration:");
logger.LogInformation("Database: {Database}", config.Database.Name);
logger.LogInformation("Storage: {Storage}", config.Storage.AccountName);
logger.LogInformation("Service Bus: {ServiceBus}", config.ServiceBus.Namespace);
logger.LogInformation("JWT Issuer: {Issuer}", config.Jwt.Issuer);
logger.LogInformation("CORS Origins: {Origins}", config.Cors.AllowedOrigins);

app.Run();
