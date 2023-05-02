// load a cjs/node-style module
// this is a very simplified version of the deno_std/node `createRequire` implementation.
function require(moduleName) {
    const wrapped = UnityOpsApi.LoadAndEvaluateCode(moduleName);

    // create minimal context for the execution
    var module = {
        exports: {}
    };
    // call the script
    // note: `require` function base path would need to be updated for proper support
    wrapped.call(
        module.exports,             // this
        module.exports,             // exports
        require,                    // require
        module,                     // module
        moduleName.substring(1),    // __filename
        moduleName.substring(0,1)   // __dirname
    );

    return module.exports;
}

const console = {
    log: function(msg) { UnityOpsApi.Log("SceneLog: " + msg) },
    info: function(msg) { UnityOpsApi.Log("SceneInfo: " + msg) },
    debug: function(msg) { UnityOpsApi.Log("SceneDebug: " + msg) },
    trace: function(msg) { UnityOpsApi.Log("SceneTrace: " + msg) },
    warning: function(msg) { UnityOpsApi.Warning("SceneWarning: " + msg) },
    error: function(msg) { UnityOpsApi.Error("SceneError: " + msg) },
}

console.log("UnityOpsApi initialized")