module.exports.getSceneInfo = async function(message) {
    const { cid, contents, metadata, baseUrl } = await UnitySceneApi.GetSceneInfo()    

    return { 
        cid, 
        contents, 
        metadata,
        baseUrl 
    };
}