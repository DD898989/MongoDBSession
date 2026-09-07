const mongoose = require('mongoose');

const memberSchema = new mongoose.Schema({
    _id: String,
    Name: String,
    Email: String,
    Status: String,
    JoinedAt: Date
}, { collection: 'members', versionKey: false });

module.exports = mongoose.model('Member', memberSchema);
