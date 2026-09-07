const mongoose = require('mongoose');

const mongoDbSessionSchema = new mongoose.Schema({
    SessionId: { type: String, required: true },
    UserId: String,
    Name: String,
    Email: String,
    CreatedAt: Date,
    ExpiresAt: { type: Date, required: true }
}, { collection: 'Sessions', versionKey: false });

mongoDbSessionSchema.index({ ExpiresAt: 1 }, { expireAfterSeconds: 0 });

module.exports = mongoose.model('MongoDbSession', mongoDbSessionSchema);
