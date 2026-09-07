const Member = require('../models/Member');

class MemberService {
    static async getRandomMemberAsync() {
        const randomMembers = await Member.aggregate([{ $sample: { size: 1 } }]);
        return randomMembers[0];
    }
}

module.exports = MemberService;
