// engine module
const showDeprecated = () => console.log('JSMODULE: EnvionmentApi is DEPRECATED.');

module.exports.areUnsafeRequestAllowed = async function(messages) {
	showDeprecated();
    return { status: false }
}

module.exports.getBootstrapData = async function() {
	showDeprecated();
    return {
        baseUrl: "",
        id: "",
        useFPSThrottling: false
    }
}

module.exports.getCurrentRealm = async function() {
	showDeprecated();
    const realm = await UnityRuntime.GetRealm();
    const runtimeResponse = realm.realmInfo;
    return { currentRealm : {
            displayName : runtimeResponse.realmName,
            serverName : runtimeResponse.realmName,
            protocol: 'v3',
            domain: runtimeResponse.baseUrl,
            layer: '',
            room: ''
        }}
}

module.exports.getDecentralandTime = async function() {
	showDeprecated();
    return await UnityRuntime.GetWorldTime()
}

module.exports.getExplorerConfiguration = async function() {
	showDeprecated();
    return {
        clientUri: "",
        configurations: {}
    };
}

module.exports.getPlatform = async function() {
	showDeprecated();
    return { platform: 'desktop' };
}

module.exports.isPreviewMode = async function() {
	showDeprecated();
    return { isPreview: false };
}