// load a cjs/node-style module
// this is a very simplified version of the deno_std/node `createRequire` implementation.
function require(moduleName) {
    const wrapped = UnityOpsApi.LoadAndEvaluateCode(moduleName);

    if (!wrapped) return { };

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
	
    const logger = {
        error: (m) => console.error(m),
        warning: (m) => console.warning(m),
        log: (m) => console.log(m),
    }
    
    Validates.registerBundle(module.exports, logger)
    Validates.registerLogs(module.exports, logger)
    //TODO implement later
    // Validates.registerIntegrationTests(module.exports, logger)
    
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

// NOTE: MetadyneLabs.dcl.eth introduced a dependency on Cannon.js, this library
// Attempts to define the "performance" object and fails. This is due to "use strict" being enabled. [https://www.w3schools.com/js/js_strict.asp]
// Note that in theory, this is invalid code - 
// strict mode is enabled by default in modules (see code exported from module: https://github.com/schteppe/cannon.js/blob/569730f94a1d9da47967a24fad0323ef7d5b4119/src/world/World.js#L491C22-L491C22)
// We are unsure if this is part of a broader problem, if for some reason errors are thrown that appear
// To be related to the usage of "use strict", such as variable is not defined, we should investigate further. 
// This does not occur on unity-renderer.
const performance = {
	now: function() { return Date.now(); }
}

// timeout handler
globalThis.setImmediate = (fn) => Promise.resolve().then(fn)

globalThis.require = require;
globalThis.console = console;
globalThis.WebSocket = require('~system/WebSocketApi').WebSocket;
globalThis.fetch = async function executeFetch(url, init) {
    if (init != undefined && init.body != undefined) {
        init.body = JSON.parse(init.body)
    }
    
    let message = {
        url: url,
        init: init
    }
    
    return Promise.resolve(require('~system/FetchApi').fetch(message))
}


// disable WebAssembly
globalThis.WebAssembly.Instance = function () {
    throw new Error('Wasm is not allowed in scene runtimes')
}
globalThis.WebAssembly.Module = function () {
    throw new Error('Wasm is not allowed in scene runtimes')
}

console.log("UnityOpsApi initialized")