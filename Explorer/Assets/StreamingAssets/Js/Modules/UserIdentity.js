module.exports.getUserData = async function(message) {
    const result = await UnityUserIdentityApi.GetOwnUserData();
    
    if (!result.containsData) {
        result.data = undefined;
        console.log('!result.containsData');
    } else {
        console.log(result.data.userId);    
    }
    
    return result;
}
