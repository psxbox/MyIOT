using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MyIOT.Api.Auth;
using MyIOT.Api.Data;
using MyIOT.Api.Endpoints;
using MyIOT.Api.Mqtt;
using MyIOT.Api.Repositories;
using MyIOT.Api.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ───────────────────── Configuration ─────────────────────

var configuration = builder.Configuration;

// ──────────────────── Service Defaults ─────────────────────

builder.AddServiceDefaults();

// ───────────────────── Database (PostgreSQL + TimescaleDB) ─────────────────────

// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

builder.AddNpgsqlDbContext<AppDbContext>("myiotdb", configureDbContextOptions: options =>
{
    options.UseSnakeCaseNamingConvention();
});

// ───────────────────── Redis ─────────────────────

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConnection);
});

// ───────────────────── JWT Authentication ─────────────────────

builder.Services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddSingleton<JwtTokenService>();

var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ───────────────────── Repositories ─────────────────────

builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<ITelemetryRepository, TelemetryRepository>();
builder.Services.AddScoped<IAttributeRepository, AttributeRepository>();

// ───────────────────── Services ─────────────────────

builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddScoped<IAttributeService, AttributeService>();
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// ───────────────────── MQTT Broker ─────────────────────

builder.Services.AddSingleton<MqttMessageHandler>();
builder.Services.AddHostedService<MqttServerHostedService>();

// ───────────────────── Swagger / OpenAPI ─────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MyIOT API",
        Version = "v1",
        Description = "IoT platform backend — device management, telemetry & attributes"
    });

    // JWT Bearer auth in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    options.AddSecurityRequirement((doc) => []);
});

// ───────────────────── CORS (for future Blazor WASM client) ─────────────────────

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});


// ═══════════════════════════════════════════════════════════
var app = builder.Build();
// ═══════════════════════════════════════════════════════════

// ───────────────────── Middleware Pipeline ─────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyIOT API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowBlazorClient");
app.UseAuthentication();
app.UseAuthorization();

// ───────────────────── Minimal API Endpoints ─────────────────────

var api = app.MapGroup("/api");

api.MapAuthEndpoints();
api.MapDeviceEndpoints();
api.MapTelemetryEndpoints();
api.MapAttributeEndpoints();

// ───────────────────── Default Endpoints ─────────────────────
app.MapDefaultEndpoints();

// ───────────────────── Database Migration on Startup ─────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();

        // Create TimescaleDB hypertable if not already created
        // This is idempotent — TimescaleDB ignores if hypertable already exists
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM timescaledb_information.hypertables
                    WHERE hypertable_name = 'telemetry'
                ) THEN
                    PERFORM create_hypertable('telemetry', 'timestamp');
                END IF;
            END $$;
        ");
        logger.LogInformation("TimescaleDB hypertable ensured for 'telemetry' table");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration");
        throw;
    }
}

// ───────────────────── Run ─────────────────────

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
