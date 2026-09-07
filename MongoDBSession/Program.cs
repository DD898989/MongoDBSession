using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Driver;
using MongoDBSession.Endpoints;
using MongoDBSession.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
builder.Logging.AddFilter("Microsoft", LogLevel.Critical);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter JWT Bearer token to authorize requests"
        });
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", context.Document),
                    new List<string>()
                }
            });
        }
        return Task.CompletedTask;
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = JWT.Issuer,
        ValidAudience = JWT.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JWT.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis:6379";
});


var mongoSettings = MongoClientSettings.FromConnectionString("mongodb://mongodb:27017");
mongoSettings.MaxConnectionPoolSize = 500;
mongoSettings.WaitQueueSize = 5000;
mongoSettings.WaitQueueTimeout = TimeSpan.FromSeconds(30);

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoSettings));
builder.Services.AddSingleton<IMongoDatabase>(sp => 
    sp.GetRequiredService<IMongoClient>().GetDatabase("SessionDb"));

var app = builder.Build();


var database = app.Services.GetRequiredService<IMongoDatabase>();
await DatabaseSeeder.SeedMembersAsync(database);

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "MongoDBSession API v1");
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok("API is online and healthy!"));
app.MapJWTLogin();
app.MapJWTProfile();
app.MapRedisLogin();
app.MapRedisProfile();
app.MapMongoDbLogin();
app.MapMongoDbProfile();

app.Run();

