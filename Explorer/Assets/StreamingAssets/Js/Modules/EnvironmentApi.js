// engine module
const showDeprecated = () => console.log('JSMODULE: EnvionmentApi is DEPRECATED.');

module.exports.areUnsafeRequestAllowed = async function(messages) {
	showDeprecated();
    return { status: false }
}

module.exports.getBootstrapData = async function() {
	showDeprecated();
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
    const { time } = await UnityRuntime.GetWorldTime()
    return { time };
}

module.exports.getExplorerConfiguration = async function() {
	showDeprecated();
    return { };
}

module.exports.getPlatform = async function() {
	showDeprecated();
    return { platform: 'desktop' };
}

module.exports.isPreviewMode = async function() {
	showDeprecated();
    return { isPreview: false };
}