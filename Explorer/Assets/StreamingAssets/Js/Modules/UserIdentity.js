module.exports.getUserData = async function(message) {
    const result = await UnityUserIdentityApi.GetOwnUserData();
    const data = result.data;
    
    if (!data) {
        return {};
    }
    
    const avatar = data.avatar;
    const wearables = [];
    
    for (let i = 0; i < avatar.wearables.Count; i++) {
        wearables.push(avatar.wearables[i]);
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
