// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/user_identity.proto

module.exports.getUserPublicKey = async function(message) {
    return UnityUserIdentityApi.UserPublicKey()
}

module.exports.getUserData = async function(message) {
    const json = await UnityUserIdentityApi.GetOwnUserData();
    const result = JSON.parse(json);
    return result;
}
