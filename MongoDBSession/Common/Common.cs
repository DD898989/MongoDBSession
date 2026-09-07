using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoDBSession.Models;

public class Member
{
    [BsonId]
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Status { get; set; }
    public DateTime JoinedAt { get; set; }
}


public class Const
{
    public const int ExpiryMinutes = 1;
}


public static class MemberService
{
    public static async Task<Member> GetRandomMemberAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<Member>("members");
        return await collection.Aggregate()
            .Sample(1)
            .FirstAsync();
    }
}


public static class DatabaseSeeder
{
    public static async Task SeedMembersAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<BsonDocument>("members");

        long count = await collection.EstimatedDocumentCountAsync();
        if (count >= 1000000)
        {
            Console.WriteLine($"[Seeder] MongoDB 已經有 {count} 筆會員資料，跳過初始化。");
            return;
        }

        Console.WriteLine($"[Seeder] 開始向 MongoDB 寫入 1,000,000 筆隨機會員資料... 目前已有: {count} 筆");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        int totalToInsert = 1000000 - (int)count;
        int batchSize = 100000;
        int inserted = 0;

        while (inserted < totalToInsert)
        {
            var batch = new List<BsonDocument>();
            int currentBatchSize = Math.Min(batchSize, totalToInsert - inserted);
            for (int i = 0; i < currentBatchSize; i++)
            {
                int id = inserted + i + 1;
                batch.Add(new BsonDocument
                {
                    { "_id", $"MEMBER_{id:D7}" },
                    { "Name", $"MemberName_{id}" },
                    { "Email", $"member_{id}@example.com" },
                    { "Status", "Active" },
                    { "JoinedAt", DateTime.UtcNow.AddMinutes(-id) }
                });
            }

            await collection.InsertManyAsync(batch, new InsertManyOptions { IsOrdered = false });
            inserted += currentBatchSize;
            Console.WriteLine($"[Seeder] 已經成功寫入 {inserted}/{totalToInsert} 筆會員資料...");
        }

        watch.Stop();
        Console.WriteLine($"[Seeder] 初始化完成，共寫入 {totalToInsert} 筆資料，耗時: {watch.Elapsed.TotalSeconds:F2} 秒。");
    }
}
