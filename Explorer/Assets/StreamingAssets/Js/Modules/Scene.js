module.exports.getSceneInfo = async function(message) {
    const { cid, contents, metadata, baseUrl } = UnitySceneApi.GetSceneInfo()
    const parsedContents = JSON.parse(contents)
    return { 
        cid,
        contents: parsedContents, 
        metadata,
        baseUrl 
    };
}