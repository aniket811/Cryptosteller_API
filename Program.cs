using CryptostellerAPI.Data;
using CryptostellerAPI.Services;  // ← add this using
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile("cryptostellernew01-firebase-adminsdk-fbsvc-f557faceba.json")
});
// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SQL"))
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());

// ── Passkey ───────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();

builder.Services.AddScoped<CryptostellerAPI.Repository.IPasskeyRepository,
                            CryptostellerAPI.Repository.PasskeyRepository>();

builder.Services.AddScoped<PasskeyService>();  // ← THIS WAS MISSING

// ── Fido2 ─────────────────────────────────────────────────────
var fido2Config = new Fido2NetLib.Fido2Configuration
{
    ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? builder.Environment.ApplicationName,
    ServerName = builder.Configuration["Fido2:ServerName"] ?? builder.Environment.ApplicationName,
    Origins = new HashSet<string> { builder.Configuration["Fido2:Origin"] ?? "http://localhost:4200" }
};
builder.Services.AddSingleton<Fido2NetLib.IFido2>(_ => new Fido2NetLib.Fido2(fido2Config));

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

// ── Firebase JWT ──────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://securetoken.google.com/cryptostellernew01";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/cryptostellernew01",
            ValidateAudience = true,
            ValidAudience = "cryptostellernew01",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
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
                // ← ADD THIS — shows exactly why 401 is happening
                Console.WriteLine("CHALLENGE ERROR: " + context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddSingleton(fido2Config);
builder.Services.AddAuthorization();  // ← THIS WAS MISSING

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors("DefaultCorsPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers(); 

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");
app.Run();