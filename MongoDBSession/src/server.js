require('express-async-errors');
const express = require('express');
const mongoose = require('mongoose');
const { createClient } = require('redis');
const DatabaseSeeder = require('./services/DatabaseSeeder');
const jwtEndpoints = require('./endpoints/jwt');
const mongodbEndpoints = require('./endpoints/mongodb');
const redisEndpoints = require('./endpoints/redis');

const app = express();
app.use(express.json());

const MONGODB_URI = process.env.MONGODB_URI || 'mongodb://mongodb:27017/SessionDb';
const REDIS_URL = process.env.REDIS_URL || 'redis://redis:6379';

async function startServer() {
    await mongoose.connect(MONGODB_URI);
    console.log('Connected to MongoDB');

    await DatabaseSeeder.seedMembersAsync();

    const redisClient = createClient({ url: REDIS_URL });
    redisClient.on('error', err => console.error('Redis Client Error', err));
    await redisClient.connect();
    console.log('Connected to Redis');

    app.get('/', (req, res) => {
        res.send('API is online and healthy!');
    });

    app.use('/api/jwt', jwtEndpoints);
    app.use('/api/mongodb', mongodbEndpoints);
    app.use('/api/redis', redisEndpoints(redisClient));

    // Global Error Handler
    app.use((err, req, res, next) => {
        console.error(err);
        res.status(err.status || 500).json({ error: err.message || 'Internal Server Error' });
    });

    const PORT = process.env.PORT || 8080;
    app.listen(PORT, () => {
        console.log(`Server is running on port ${PORT}`);
    });
}

startServer().catch(err => {
    console.error('Failed to start server:', err);
    process.exit(1);
});
