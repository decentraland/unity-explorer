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
        moduleName.substring(0, 1)   // __dirname
    );

    return module.exports;
}

const console = {
    log: function (...args) { UnityOpsApi.Log("SceneLog: " + args.join(' ')) },
    info: function (...args) { UnityOpsApi.Log("SceneInfo: " + args.join(' ')) },
    debug: function (...args) { UnityOpsApi.Log("SceneDebug: " + args.join(' ')) },
    trace: function (...args) { UnityOpsApi.Log("SceneTrace: " + args.join(' ')) },
    warning: function (...args) { UnityOpsApi.Warning("SceneWarning: " + args.join(' ')) },
    error: function (...args) { UnityOpsApi.Error("SceneError: " + args.join(' ')) },
}

// timeout handler
globalThis.setImmediate = (fn) => Promise.resolve().then(fn)

globalThis.require = require;
globalThis.console = console;

// disable WebAssembly
globalThis.WebAssembly.Instance = function () {
    throw new Error('Wasm is not allowed in scene runtimes')
}
globalThis.WebAssembly.Module = function () {
    throw new Error('Wasm is not allowed in scene runtimes')
}

console.log("UnityOpsApi initialized")