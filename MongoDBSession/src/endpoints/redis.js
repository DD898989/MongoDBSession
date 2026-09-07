const express = require('express');
const crypto = require('crypto');
const MemberService = require('../services/MemberService');
const Const = require('../constants');

module.exports = function(redisClient) {
    const router = express.Router();

    router.post('/login', async (req, res) => {
        const randomUser = await MemberService.getRandomMemberAsync();

        const sessionKey = crypto.randomUUID();
        const session = {
            SessionId: sessionKey,
            UserId: randomUser._id,
            Name: randomUser.Name,
            Email: randomUser.Email,
            CreatedAt: new Date().toISOString(),
            ExpiresAt: new Date(Date.now() + Const.ExpiryMinutes * 60000).toISOString()
        };

        await redisClient.set(sessionKey, JSON.stringify(session), {
            EX: Const.ExpiryMinutes * 60
        });

        res.json({
            Token: sessionKey,
            ExpiresInMinutes: Const.ExpiryMinutes,
            User: {
                Id: randomUser._id,
                Name: randomUser.Name,
                Email: randomUser.Email
            }
        });
    });

    router.get('/profile', async (req, res) => {
        const authHeader = req.headers.authorization;

        const token = authHeader.split(' ')[1];
        const value = await redisClient.get(token);
        if (!value) {
            return res.status(401).json({ Message: "找不到對應的 Redis Session，或該 Session 已經過期！" });
        }

        const session = JSON.parse(value);

        res.json({
            Message: "恭喜！您已成功通過 Redis 驗證，成功存取受保護的 Profile 路由。",
            User: {
                Id: session.UserId,
                Name: session.Name,
                Email: session.Email
            }
        });
    });

    return router;
};
