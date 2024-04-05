module.exports.spawn = async function(message) {
    console.log('JSMODULE: spawn')
    return {
        name: "obsolete",
        parentCid: 'obsolete',
        pid: 'obsolete'
    };
}

module.exports.kill = async function(message) {
    console.log('JSMODULE: kill')
    return {
        status: false
    };
}

module.exports.exit = async function(message) {
    console.log('JSMODULE: exit')
    return {
        status: false
    };
}

module.exports.getPortableExperiencesLoaded = async function(message) {
    console.log('JSMODULE: getPortableExperiencesLoaded')
    return {
        loaded: []
    };
}