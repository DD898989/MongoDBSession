const express = require('express');
const crypto = require('crypto');
const MongoDbSession = require('../models/MongoDbSession');
const MemberService = require('../services/MemberService');
const Const = require('../constants');

const router = express.Router();

router.post('/login', async (req, res) => {
    const randomUser = await MemberService.getRandomMemberAsync();

    const sessionKey = crypto.randomUUID();
    const sessionDoc = {
        SessionId: sessionKey,
        UserId: randomUser._id,
        Name: randomUser.Name,
        Email: randomUser.Email,
        CreatedAt: new Date(),
        ExpiresAt: new Date(Date.now() + Const.ExpiryMinutes * 60000)
    };

    await MongoDbSession.findOneAndReplace(
        { SessionId: sessionKey },
        sessionDoc,
        { upsert: true }
    );

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
    const session = await MongoDbSession.findOne({ SessionId: token });

    if (!session || new Date() > session.ExpiresAt) {
        return res.status(401).json({ Message: "找不到對應的 MongoDB Session，或該 Session 已經過期！" });
    }

    res.json({
        Message: "恭喜！您已成功通過 MongoDB 驗證，成功存取受保護的 Profile 路由。",
        User: {
            Id: session.UserId,
            Name: session.Name,
            Email: session.Email
        }
    });
});

module.exports = router;
