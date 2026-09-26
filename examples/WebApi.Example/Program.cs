using System.Text;
using Azure.Identity;
using FluentAzure;
using FluentAzure.Guard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebApi.Example.Configuration;
using WebApi.Example.Data;
using WebApi.Example.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration is loaded with Microsoft's providers: WebApplication.CreateBuilder already reads
// appsettings.json and environment variables, and secrets come from Key Vault when KeyVault:Url is set.
// Key Vault secret names use "--" for ":" (e.g. Database--ConnectionString).
var keyVaultUrl = builder.Configuration["KeyVault:Url"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
}

// FluentAzure guards it: if a setting is missing or invalid, startup fails listing every problem by key
// (never by value), and the health check re-runs the same rules after configuration reloads.
builder.Services.AddFluentAzureGuard(guard => guard
    .Required(
        "Database:ConnectionString",
        "Storage:ConnectionString",
        "ServiceBus:ConnectionString",
        "Jwt:SecretKey",
        "Jwt:Issuer",
        "Jwt:Audience")
    .Validate("Jwt:SecretKey", value => value.Length >= 32, "must be at least 32 characters")
    .Validate(
        "Jwt:Issuer",
        value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
        "must be an absolute https URI"));
builder.Services.AddHealthChecks().AddFluentAzureGuard();

// Bind the settings the app uses at startup. The guard runs before the app serves traffic.
var config = builder.Configuration.Get<WebApiConfiguration>() ?? new WebApiConfiguration();
builder.Services.AddSingleton(config);

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
app.MapHealthChecks("/health");

// Log configuration summary
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Web API started with configuration:");
logger.LogInformation("Database: {Database}", config.Database.Name);
logger.LogInformation("Storage: {Storage}", config.Storage.AccountName);
logger.LogInformation("Service Bus: {ServiceBus}", config.ServiceBus.Namespace);
logger.LogInformation("JWT Issuer: {Issuer}", config.Jwt.Issuer);
logger.LogInformation("CORS Origins: {Origins}", config.Cors.AllowedOrigins);

// A full view of the configuration, with secrets masked (never log GetDebugView() itself)
logger.LogDebug(
    "{Configuration}",
    app.Services.GetRequiredService<ConfigurationGuard>().GetRedactedDebugView((IConfigurationRoot)app.Configuration));

app.Run();
