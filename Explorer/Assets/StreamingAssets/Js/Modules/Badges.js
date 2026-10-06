// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/badges.proto

module.exports.checkAwards = async function (message) {
    UnityBadgesApi.CheckAwards();
    return {};
}
