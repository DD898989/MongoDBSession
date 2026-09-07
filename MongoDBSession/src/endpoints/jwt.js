const express = require('express');
const jwt = require('jsonwebtoken');
const MemberService = require('../services/MemberService');
const Const = require('../constants');

const router = express.Router();

router.post('/login', async (req, res) => {
    const randomUser = await MemberService.getRandomMemberAsync();

    const payload = {
        nameid: randomUser._id,
        unique_name: randomUser.Name,
        email: randomUser.Email
    };

    const token = jwt.sign(payload, Const.JWT.SecretKey, {
        issuer: Const.JWT.Issuer,
        audience: Const.JWT.Audience,
        expiresIn: `${Const.ExpiryMinutes}m`
    });

    res.json({
        Token: token,
        ExpiresInMinutes: Const.ExpiryMinutes,
        User: {
            Id: randomUser._id,
            Name: randomUser.Name,
            Email: randomUser.Email
        }
    });
});

router.get('/profile', (req, res) => {
    const authHeader = req.headers.authorization;

    const token = authHeader.split(' ')[1];
    const decoded = jwt.verify(token, Const.JWT.SecretKey, {
        issuer: Const.JWT.Issuer,
        audience: Const.JWT.Audience
    });

    res.json({
        Message: "恭喜！您已成功通過 JWT 驗證，成功存取受保護的 Profile 路由。",
        User: {
            Id: decoded.nameid,
            Name: decoded.unique_name,
            Email: decoded.email
        }
    });
});

module.exports = router;
