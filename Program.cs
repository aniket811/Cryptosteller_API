using CryptostellerAPI.Data;
using CryptostellerAPI.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ───────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger (.NET 8 replacement for AddOpenApi) ───────────────
builder.Services.AddEndpointsApiExplorer();
// ── Firebase Admin SDK ────────────────────────────────────────
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile("cryptostellernew01-firebase-adminsdk-fbsvc-f557faceba.json")
});

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SQL"))
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());

// ── Passkey Services ──────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();

builder.Services.AddScoped<CryptostellerAPI.Repository.IPasskeyRepository,
                           CryptostellerAPI.Repository.PasskeyRepository>();

builder.Services.AddScoped<PasskeyService>();

// ── FIDO2 Configuration ───────────────────────────────────────
var fido2Config = new Fido2NetLib.Fido2Configuration
{
    ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? builder.Environment.ApplicationName,
    ServerName = builder.Configuration["Fido2:ServerName"] ?? builder.Environment.ApplicationName,
    Origins = new HashSet<string>
    {
        builder.Configuration["Fido2:Origin"] ?? "http://localhost:4200"
    }
};

builder.Services.AddSingleton<Fido2NetLib.IFido2>(_ => new Fido2NetLib.Fido2(fido2Config));
builder.Services.AddSingleton(fido2Config);

// ── CORS ─────────────────────────────────────────────────────
var allowedOrigin = builder.Configuration["Fido2:Origin"] ?? "http://localhost:4200";

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── Firebase JWT Authentication ───────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/cryptostellernew01",
            ValidateAudience = true,
            ValidAudience = "cryptostellernew01",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                var client = new HttpClient();
                var json = client.GetStringAsync(
                    "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com"
                ).Result;

                var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return keys!.Select(k =>
                {
                    var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                        System.Text.Encoding.UTF8.GetBytes(k.Value));
                    return new Microsoft.IdentityModel.Tokens.X509SecurityKey(cert)
                    {
                        KeyId = k.Key
                    };
                });
            }
        };

        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("AUTH FAILED: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("TOKEN VALIDATED for: " +
                    context.Principal?.FindFirst("email")?.Value);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine("CHALLENGE ERROR: " + context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Build App ─────────────────────────────────────────────────
var app = builder.Build();

// ── Swagger UI ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
}

// ── Middleware ───────────────────────────────────────────────
app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Render Port Configuration ─────────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

app.Run();