// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/scene.proto

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