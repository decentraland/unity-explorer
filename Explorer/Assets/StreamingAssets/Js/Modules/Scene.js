module.exports.getSceneInfo = async function(message) {
    const { cid, contents, metadata, baseUrl } = UnitySceneApi.GetSceneInfo()    

    return { 
        cid, 
        contents, 
        metadata,
        baseUrl 
    };
}