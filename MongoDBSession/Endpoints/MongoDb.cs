using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDBSession.Models;

namespace MongoDBSession.Endpoints;

public class MongoDbSession
{
    [BsonId]
    public string SessionId { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public static class MongoDbLogin
{
    public static void MapMongoDbLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/mongodb/login", async (IMongoDatabase database) =>
        {
            var randomUser = await MemberService.GetRandomMemberAsync(database);

            var sessionKey = Guid.NewGuid().ToString();

            var sessionsCollection = database.GetCollection<MongoDbSession>("Sessions");

            var keys = Builders<MongoDbSession>.IndexKeys.Ascending(doc => doc.ExpiresAt);
            var options = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero };
            var indexModel = new CreateIndexModel<MongoDbSession>(keys, options);
            await sessionsCollection.Indexes.CreateOneAsync(indexModel);

            var doc = new MongoDbSession
            {
                SessionId = sessionKey,
                UserId = randomUser.Id,
                Name = randomUser.Name,
                Email = randomUser.Email,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(Const.ExpiryMinutes)
            };

            await sessionsCollection.ReplaceOneAsync(
                filter: d => d.SessionId == sessionKey,
                replacement: doc,
                options: new ReplaceOptions { IsUpsert = true }
            );

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



public static class MongoDbProfile
{
    public static void MapMongoDbProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/mongodb/profile", async (HttpContext httpContext, IMongoDatabase database) =>
        {
            var authHeader = httpContext.Request.Headers["Authorization"].ToString();
            var token = authHeader.Substring("Bearer ".Length).Trim();
            var collection = database.GetCollection<MongoDbSession>("Sessions");

            var doc = await collection.Find(d => d.SessionId == token).FirstOrDefaultAsync();
            if (doc == null)
                return Results.Json(new { Message = "找不到對應的 MongoDB Session，或該 Session 已經過期！" }, statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(new
            {
                Message = "恭喜！您已成功通過 MongoDB 驗證，成功存取受保護的 Profile 路由。",
                User = new
                {
                    Id = doc.UserId,
                    Name = doc.Name,
                    Email = doc.Email
                },
            });
        });
    }
}
