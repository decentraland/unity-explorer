module.exports.getUserData = async function(message) {
    const result = await UnityUserIdentityApi.GetOwnUserData();
    console.log(result);
    return result;
}
