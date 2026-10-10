// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/portable_experiences.proto

module.exports.spawn = async function(message) {
    console.log('JSMODULE: spawn');
    const response = await UnityPortableExperiencesApi.Spawn(message.pid, message.ens);
    return response;
}

module.exports.kill = async function(message) {
    console.log('JSMODULE: kill');
    const isSuccess = await UnityPortableExperiencesApi.Kill(message.pid);
    return isSuccess;
}

module.exports.exit = async function() {
    console.log('JSMODULE: exit');
    const isSuccess = await UnityPortableExperiencesApi.Exit();
    return isSuccess;
}

module.exports.getPortableExperiencesLoaded = async function(message) {
    console.log('JSMODULE: getPortableExperiencesLoaded');
    return {
        loaded: JSON.parse(UnityPortableExperiencesApi.GetLoadedPortableExperiences())
    };
}