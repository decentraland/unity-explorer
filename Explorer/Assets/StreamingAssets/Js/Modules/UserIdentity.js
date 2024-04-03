module.exports.getUserPublicKey = async function(message) {
    return UnityUserIdentityApi.UserPublicKey()
}

module.exports.getUserData = async function(message) {
    return  UnityUserIdentityApi.GetOwnUserData()
}
