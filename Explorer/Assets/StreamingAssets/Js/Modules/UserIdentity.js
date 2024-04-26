module.exports.getUserPublicKey = async function(message) {
    return UnityUserIdentityApi.UserPublicKey()
}

module.exports.getUserData = async function(message) {
    const result = await UnityUserIdentityApi.GetOwnUserData();
    if (result.data == undefined) {
        return result
    }
    
    const data = result.data;
    const avatar = data.avatar;
    const wearables = Array(avatar.wearables.Count);
    
    for (let i = 0; i < avatar.wearables.Count; i++) {
        wearables[i] = avatar.wearables[i];
    }
    
    return {
        data: {
            ...data,
            avatar: {
                ...avatar,
                wearables: wearables
            }
        }
    };
}
