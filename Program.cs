using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentAPI;
using PaymentAPI.Models;
using PaymentAPI.Services;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Allow specific origins or all
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
// Set TLS versions
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 |
                                       SecurityProtocolType.Tls12 |
                                       SecurityProtocolType.Tls13;

// Get the connection string from appsettings.json
var databaseProvider = builder.Configuration["DatabaseProvider"];
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<LogService>();

// JWT setup
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true, // checks expiry
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                // ✅ tell framework we handled this
                context.HandleResponse();

                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 200; // always return 200
                    context.Response.ContentType = "application/json";

                    var result = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        code = 200,
                        status = "Unauthorized",
                        message = "Token is missing, invalid, or expired"
                    });

                    return context.Response.WriteAsync(result);
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                context.NoResult(); // ✅ stop default

                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";

                    var result = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        code = 200,
                        status = "Unauthorized",
                        message = "Authentication failed: " + context.Exception.Message
                    });

                    return context.Response.WriteAsync(result);
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Only register implemented DB service
builder.Services.AddScoped<PostgresDatabaseService>();

var app = builder.Build();

app.UsePathBase("/getepaylink");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS
app.UseCors("AllowAll");

// Ensure authentication middleware runs before authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();