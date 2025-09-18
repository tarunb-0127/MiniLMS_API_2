using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using Mini_LMS.Helpers;
using Mini_LMS.Models;
using Mini_LMS.Services;

var builder = WebApplication.CreateBuilder(args);

// 1?? Register Helpers, Services, DbContext
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<BlobService>();

// At the top, before AddDbContext
var isTesting = builder.Environment.IsEnvironment("Testing")
                || builder.Configuration.GetValue<bool>("UseInMemory");

if (isTesting)
{
    builder.Services.AddDbContext<MiniLMSContext>(opts =>
        opts.UseInMemoryDatabase("MiniLMS_Test"));
}
else
{
    builder.Services.AddDbContext<MiniLMSContext>(opts =>
        opts.UseMySql(
            builder.Configuration.GetConnectionString("Default"),
            ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))
        )
    );
}



builder.Services.AddDbContext<MiniLMSContext>(opts =>
    opts.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))
    )
);

// 2?? Configure Controllers & JSON options
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.WriteIndented = true;
    });

// 3?? Enable Swagger with Bearer support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "MiniLMS API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (prefixed with 'Bearer ')"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 4?? Configure JWT Authentication & Claim Mapping
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAud = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = Encoding.UTF8.GetBytes(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAud,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            RequireExpirationTime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"JWT Error: {ctx.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

// 5?? Configure Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TrainerOnly", policy => policy.RequireRole("Trainer"));
});

// 6?? CORS for React
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 7?? Middleware pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// 8?? Serve static files from uploads folder
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");

// Create folder if not exists
if (!Directory.Exists(uploadsPath))
    Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    ServeUnknownFileTypes = true, // serve mp4, pdf, etc.
    DefaultContentType = "application/octet-stream",
});

// Optional: Log uploaded files for debugging
Console.WriteLine("Uploads folder mapped to: " + uploadsPath);

// 9?? Map controllers
app.MapControllers();

app.Run();
