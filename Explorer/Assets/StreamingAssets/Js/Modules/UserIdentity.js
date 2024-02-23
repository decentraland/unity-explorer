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
            publicKey: data.publicKey,
            displayName: data.displayName,
            hasConnectedWeb3: data.hasConnectedWeb3,
            userId: data.userId,
            version: data.version,
            avatar: {
                bodyShape: avatar.bodyShape,
                eyeColor: avatar.eyeColor,
                hairColor: avatar.hairColor,
                skinColor: avatar.skinColor,
                wearables: wearables,
                snapshots: {
                    body: avatar.snapshots.body,
                    face256: avatar.snapshots.face256
                },
            }
        }
    };
}
