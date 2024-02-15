module.exports.getSceneInfo = async function() {
    console.log('JSMODULE: getSceneInfo')
	return UnitySceneApi.GetSceneInfo();
}