using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using MongoDBSession.Models;
using System.Text.Json;

namespace MongoDBSession.Endpoints;

public class RedisSession
{
    public string SessionId { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public static class RedisLogin
{
    public static void MapRedisLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/redis/login", async (IMongoDatabase database, IDistributedCache cache) =>
        {
            var randomUser = await MemberService.GetRandomMemberAsync(database);

            var sessionKey = Guid.NewGuid().ToString();

            var session = new RedisSession
            {
                SessionId = sessionKey,
                UserId = randomUser.Id,
                Name = randomUser.Name,
                Email = randomUser.Email,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(Const.ExpiryMinutes)
            };

            var sessionValue = JsonSerializer.Serialize(session);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Const.ExpiryMinutes)
            };

            await cache.SetStringAsync(sessionKey, sessionValue, options);

            return Results.Ok(new
            {
                Token = sessionKey,
                ExpiresInMinutes = Const.ExpiryMinutes,
                User = new
                {
                    Id = randomUser.Id,
                    Name = randomUser.Name,
                    Email = randomUser.Email
                }
            });
        });
    }
}


public static class RedisProfile
{
    public static void MapRedisProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/redis/profile", async (HttpContext httpContext, IDistributedCache cache) =>
        {
            var authHeader = httpContext.Request.Headers["Authorization"].ToString();

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var value = await cache.GetStringAsync(token);
            if (value == null)
                return Results.Json(new { Message = "找不到對應的 Redis Session，或該 Session 已經過期！" }, statusCode: StatusCodes.Status401Unauthorized);

            var session = JsonSerializer.Deserialize<RedisSession>(value);

            return Results.Ok(new
            {
                Message = "恭喜！您已成功通過 Redis 驗證，成功存取受保護的 Profile 路由。",
                User = new
                {
                    Id = session?.UserId,
                    Name = session?.Name,
                    Email = session?.Email
                },
            });
        });
    }
}
