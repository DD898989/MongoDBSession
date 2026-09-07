const mongoose = require('mongoose');

class DatabaseSeeder {
    static async seedMembersAsync() {
        const collection = mongoose.connection.collection('members');
        
        const count = await collection.estimatedDocumentCount();
        if (count >= 1000000) {
            console.log(`[Seeder] MongoDB 已經有 ${count} 筆會員資料，跳過初始建立。`);
            return;
        }

        console.log(`[Seeder] 準備向 MongoDB 寫入 1,000,000 筆模擬會員資料... (目前已有: ${count} 筆)`);
        const startTime = Date.now();

        const totalToInsert = 1000000 - count;
        const batchSize = 100000;
        let inserted = 0;

        while (inserted < totalToInsert) {
            const batch = [];
            const currentBatchSize = Math.min(batchSize, totalToInsert - inserted);
            for (let i = 0; i < currentBatchSize; i++) {
                const id = inserted + i + 1 + count;
                const idString = id.toString().padStart(7, '0');
                batch.push({
                    _id: `MEMBER_${idString}`,
                    Name: `MemberName_${id}`,
                    Email: `member_${id}@example.com`,
                    Status: "Active",
                    JoinedAt: new Date(Date.now() - id * 60000)
                });
            }

            await collection.insertMany(batch, { ordered: false });
            inserted += currentBatchSize;
            console.log(`[Seeder] 已經成功寫入 ${inserted}/${totalToInsert} 筆會員資料...`);
        }

        const elapsedSeconds = ((Date.now() - startTime) / 1000).toFixed(2);
        console.log(`[Seeder] 初始建立完畢！共新增 ${totalToInsert} 筆資料，耗時: ${elapsedSeconds} 秒。`);
    }
}

module.exports = DatabaseSeeder;
