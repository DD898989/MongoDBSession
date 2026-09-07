using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MongoDBSession.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MongoDBSession.Endpoints;

public static class JWT
{
    public const string SecretKey = "ThisIsAVerySecretKeyForJWTSessionTest12345!";
    public const string Issuer = "MongoDBSessionApi";
    public const string Audience = "MongoDBSessionApi";
}

public static class JWTLogin
{
    public static void MapJWTLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jwt/login", async (IMongoDatabase database) =>
        {
            var randomUser = await MemberService.GetRandomMemberAsync(database);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JWT.SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, randomUser.Id),
                new Claim(ClaimTypes.Name, randomUser.Name),
                new Claim(ClaimTypes.Email, randomUser.Email),
            };

            var token = new JwtSecurityToken(
                issuer: JWT.Issuer,
                audience: JWT.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Const.ExpiryMinutes),
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new
            {
                Token = tokenString,
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

public static class JWTProfile
{
    public static void MapJWTProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/jwt/profile", (ClaimsPrincipal user) =>
        {
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = user.Identity?.Name;
            var email = user.FindFirst(ClaimTypes.Email)?.Value;

            return Results.Ok(new
            {
                Message = "恭喜！您已成功通過 JWT 驗證，成功存取受保護的 Profile 路由。",
                User = new
                {
                    Id = id,
                    Name = name,
                    Email = email
                },
            });
        });
    }
}