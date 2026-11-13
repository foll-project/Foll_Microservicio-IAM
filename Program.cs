using System.Text;
using foll_iam.IAM.Application.Internal.CommandServices;
using foll_iam.IAM.Application.Internal.QueryServices;
using foll_iam.IAM.Application.OutboundServices;
using foll_iam.IAM.Domain.Repositories;
using foll_iam.IAM.Domain.Services;
using foll_iam.IAM.Infrastructure.Hashing;
using foll_iam.IAM.Infrastructure.Persistence.EFC.Repositories;
using foll_iam.IAM.Infrastructure.Tokens;
using foll_iam.Shared.Domain.Repositories;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_iam.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using foll_iam.Shared.Infrastructure.Swagger;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Foll IAM Microservice",
        Version = "v1",
        Description = "Microservicio IAM"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegue SOLO el JWT, sin escribir Bearer."
    });

    options.OperationFilter<AuthorizeOperationFilter>();
});

var jwtSecret = builder.Configuration["JwtSettings:Secret"];

if (!string.IsNullOrEmpty(jwtSecret))
{
    var key = Encoding.ASCII.GetBytes(jwtSecret);

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        });
}

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IHashingService, BcryptHashingService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IUserCommandService, UserCommandService>();
builder.Services.AddScoped<IUserQueryService, UserQueryService>();
builder.Services.AddScoped<IUserEventService, foll_iam.IAM.Infrastructure.Messaging.Outbox.OutboxUserEventService>();
builder.Services.AddOptions<foll_iam.IAM.Infrastructure.Messaging.Configuration.IntegrationMessagingOptions>()
    .Bind(builder.Configuration.GetSection("IntegrationMessaging"))
    .Validate(o => o.PollIntervalSeconds > 0 && o.BatchSize > 0 && o.BatchSize <= 100,
        "PollIntervalSeconds debe ser positivo y BatchSize debe estar entre 1 y 100.")
    .ValidateOnStart();
builder.Services.AddHostedService<foll_iam.IAM.Infrastructure.Messaging.Outbox.OutboxPublisherBackgroundService>();

var app = builder.Build();

var applyMigrations = builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup", true);

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();

    if (applyMigrations)
    {
        context.Database.Migrate();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else if (builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
