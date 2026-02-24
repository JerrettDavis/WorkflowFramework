using System.Text;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WorkflowFramework.Dashboard.Api;
using WorkflowFramework.Dashboard.Api.Hubs;
using WorkflowFramework.Dashboard.Api.Plugins;
using WorkflowFramework.Dashboard.Api.Plugins.BuiltInPlugins;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSignalR();
builder.Services.AddSingleton<WorkflowExecutionNotifier>();
builder.Services.AddSingleton<WorkflowValidator>();
builder.Services.AddWorkflowDashboardApi();

// Opt-in to EF Core + SQLite persistence (replaces in-memory stores)
if (builder.Configuration.GetValue("Dashboard:UsePersistence", true))
{
    builder.Services.AddDashboardPersistence(
        builder.Configuration.GetConnectionString("DashboardDb"));
}

// Auth configuration
var requireAuth = builder.Configuration.GetValue("Dashboard:RequireAuth", false);

if (requireAuth)
{
    var jwtSecret = builder.Configuration["Dashboard:JwtSecret"]
        ?? "WorkflowFramework_Development_JWT_Secret_Key_That_Is_At_Least_64_Bytes_Long!";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "MultiScheme";
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = "WorkflowFramework.Dashboard",
            ValidateAudience = true,
            ValidAudience = "WorkflowFramework.Dashboard",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        // Support JWT in SignalR query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null)
    .AddPolicyScheme("MultiScheme", "JWT or API Key", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.ContainsKey("X-Api-Key"))
                return "ApiKey";
            return JwtBearerDefaults.AuthenticationScheme;
        };
    });

    builder.Services.AddAuthorization();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

    // Rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("authenticated", context =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10
            });
        });
        options.AddPolicy("execution", context =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter($"exec-{userId}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            });
        });
    });
}
else
{
    // Anonymous mode — no auth required
    builder.Services.AddSingleton<ICurrentUserService, AnonymousCurrentUserService>();
}

var app = builder.Build();

// Initialize database if persistence is enabled
if (app.Configuration.GetValue("Dashboard:UsePersistence", true))
{
    await app.Services.InitializeDashboardDatabaseAsync();
}

if (requireAuth)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
}

app.MapDefaultEndpoints();

// Register plugins
var pluginRegistry = app.Services.GetRequiredService<PluginRegistry>();
pluginRegistry.Register(new EmailStepPlugin());

app.MapHub<WorkflowExecutionHub>("/hubs/execution");
app.MapWorkflowDashboardApi();
app.MapAuthEndpoints();

app.MapGet("/health", () => "ok");

app.Run();
