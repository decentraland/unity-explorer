module.exports.getUserPublicKey = async function(message) {
    return UnityUserIdentityApi.UserPublicKey()
}

module.exports.getUserData = async function(message) {
    const json = await UnityUserIdentityApi.GetOwnUserData();
    const result = JSON.parse(json);
    return result;
}
