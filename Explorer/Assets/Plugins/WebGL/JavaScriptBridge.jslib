mergeInto(LibraryManager.library, {
    // Global storage for the host object callback function pointers
    $hostObjectCallback: null,
    $hostObjectCallbackWithReturn: null,
    
    // Register the callback function that will be used to invoke host object methods (void return)
    JSContext_RegisterHostObjectCallback: function(callback) {
        hostObjectCallback = callback;
    },
    JSContext_RegisterHostObjectCallback__deps: ['$hostObjectCallback'],
    
    // Register the callback function that will be used to invoke host object methods with return values
    JSContext_RegisterHostObjectCallbackWithReturn: function(callback) {
        hostObjectCallbackWithReturn = callback;
    },
    JSContext_RegisterHostObjectCallbackWithReturn__deps: ['$hostObjectCallbackWithReturn'],
    
    JSContext_Create: function(contextId) {
        const contextIdStr = UTF8ToString(contextId);
        if (!window.__dclJSContexts) {
            window.__dclJSContexts = {};
        }
        if (!window.__dclJSContexts[contextIdStr]) {
            window.__dclJSContexts[contextIdStr] = {
                global: {
                    // Standard JavaScript built-ins that scene code may access
                    Array: Array,
                    Object: Object,
                    String: String,
                    Number: Number,
                    Boolean: Boolean,
                    Date: Date,
                    Math: Math,
                    JSON: JSON,
                    Promise: Promise,
                    Error: Error,
                    TypeError: TypeError,
                    ReferenceError: ReferenceError,
                    SyntaxError: SyntaxError,
                    RangeError: RangeError,
                    Map: Map,
                    Set: Set,
                    WeakMap: WeakMap,
                    WeakSet: WeakSet,
                    Symbol: Symbol,
                    Proxy: Proxy,
                    Reflect: Reflect,
                    RegExp: RegExp,
                    Function: Function,
                    // Typed arrays
                    ArrayBuffer: ArrayBuffer,
                    DataView: DataView,
                    Int8Array: Int8Array,
                    Uint8Array: Uint8Array,
                    Uint8ClampedArray: Uint8ClampedArray,
                    Int16Array: Int16Array,
                    Uint16Array: Uint16Array,
                    Int32Array: Int32Array,
                    Uint32Array: Uint32Array,
                    Float32Array: Float32Array,
                    Float64Array: Float64Array,
                    BigInt64Array: typeof BigInt64Array !== 'undefined' ? BigInt64Array : undefined,
                    BigUint64Array: typeof BigUint64Array !== 'undefined' ? BigUint64Array : undefined,
                    // Global functions
                    parseInt: parseInt,
                    parseFloat: parseFloat,
                    isNaN: isNaN,
                    isFinite: isFinite,
                    encodeURI: encodeURI,
                    decodeURI: decodeURI,
                    encodeURIComponent: encodeURIComponent,
                    decodeURIComponent: decodeURIComponent,
                    setTimeout: setTimeout,
                    clearTimeout: clearTimeout,
                    setInterval: setInterval,
                    clearInterval: clearInterval,
                    // setImmediate polyfill for Node.js compatibility
                    setImmediate: typeof setImmediate !== 'undefined' ? setImmediate : function(callback) {
                        var args = Array.prototype.slice.call(arguments, 1);
                        return setTimeout(function() { callback.apply(null, args); }, 0);
                    },
                    clearImmediate: typeof clearImmediate !== 'undefined' ? clearImmediate : clearTimeout,
                    // Special values
                    undefined: undefined,
                    NaN: NaN,
                    Infinity: Infinity,
                    // Provide stub WebAssembly object so Init.js can disable it without crashing
                    // Init.js sets WebAssembly.Instance and WebAssembly.Module to throw errors
                    WebAssembly: {
                        Instance: function() { throw new Error('Wasm is not allowed in scene runtimes'); },
                        Module: function() { throw new Error('Wasm is not allowed in scene runtimes'); }
                    },
                    // Node.js compatibility - process object
                    process: (function() {
                        var listeners = {};
                        var proc = {
                            env: {
                                NODE_ENV: 'production'
                            },
                            argv: [],
                            version: 'v18.0.0',
                            versions: {
                                node: '18.0.0',
                                v8: '10.0.0'
                            },
                            platform: 'browser',
                            browser: true,
                            title: 'browser',
                            pid: 1,
                            cwd: function() { return '/'; },
                            chdir: function() {},
                            umask: function() { return 0; },
                            hrtime: function(previousTimestamp) {
                                var clocktime = performance.now() * 1e-3;
                                var seconds = Math.floor(clocktime);
                                var nanoseconds = Math.floor((clocktime % 1) * 1e9);
                                if (previousTimestamp) {
                                    seconds = seconds - previousTimestamp[0];
                                    nanoseconds = nanoseconds - previousTimestamp[1];
                                    if (nanoseconds < 0) {
                                        seconds--;
                                        nanoseconds += 1e9;
                                    }
                                }
                                return [seconds, nanoseconds];
                            },
                            nextTick: function(callback) {
                                if (typeof queueMicrotask === 'function') {
                                    queueMicrotask(callback);
                                } else {
                                    Promise.resolve().then(callback);
                                }
                            },
                            on: function(event, listener) {
                                if (!listeners[event]) listeners[event] = [];
                                listeners[event].push(listener);
                                return proc;
                            },
                            once: function(event, listener) {
                                var wrapped = function() {
                                    proc.removeListener(event, wrapped);
                                    listener.apply(this, arguments);
                                };
                                return proc.on(event, wrapped);
                            },
                            off: function(event, listener) {
                                return proc.removeListener(event, listener);
                            },
                            removeListener: function(event, listener) {
                                if (listeners[event]) {
                                    var idx = listeners[event].indexOf(listener);
                                    if (idx !== -1) listeners[event].splice(idx, 1);
                                }
                                return proc;
                            },
                            removeAllListeners: function(event) {
                                if (event) {
                                    listeners[event] = [];
                                } else {
                                    listeners = {};
                                }
                                return proc;
                            },
                            emit: function(event) {
                                if (listeners[event]) {
                                    var args = Array.prototype.slice.call(arguments, 1);
                                    listeners[event].forEach(function(listener) {
                                        try { listener.apply(null, args); } catch(e) { console.error(e); }
                                    });
                                }
                                return proc;
                            },
                            listeners: function(event) {
                                return listeners[event] ? listeners[event].slice() : [];
                            },
                            binding: function() {
                                throw new Error('process.binding is not supported in browser');
                            },
                            stdout: {
                                write: function(str) { console.log(str); },
                                isTTY: false
                            },
                            stderr: {
                                write: function(str) { console.error(str); },
                                isTTY: false
                            }
                        };
                        // hrtime.bigint for newer Node.js APIs
                        proc.hrtime.bigint = function() {
                            return BigInt(Math.floor(performance.now() * 1e6));
                        };
                        return proc;
                    })(),
                    // Node.js compatibility - Buffer class (using Uint8Array base)
                    Buffer: (function() {
                        function Buffer(arg, encodingOrOffset, length) {
                            if (typeof arg === 'number') {
                                return new Uint8Array(arg);
                            }
                            if (typeof arg === 'string') {
                                return Buffer.from(arg, encodingOrOffset);
                            }
                            if (ArrayBuffer.isView(arg) || arg instanceof ArrayBuffer) {
                                return new Uint8Array(arg, encodingOrOffset, length);
                            }
                            if (Array.isArray(arg)) {
                                return new Uint8Array(arg);
                            }
                            return new Uint8Array(0);
                        }
                        
                        Buffer.from = function(value, encodingOrOffset, length) {
                            if (typeof value === 'string') {
                                var encoding = encodingOrOffset || 'utf8';
                                if (encoding === 'utf8' || encoding === 'utf-8') {
                                    var encoder = new TextEncoder();
                                    return encoder.encode(value);
                                } else if (encoding === 'base64') {
                                    var binary = atob(value);
                                    var bytes = new Uint8Array(binary.length);
                                    for (var i = 0; i < binary.length; i++) {
                                        bytes[i] = binary.charCodeAt(i);
                                    }
                                    return bytes;
                                } else if (encoding === 'hex') {
                                    var bytes = new Uint8Array(value.length / 2);
                                    for (var i = 0; i < value.length; i += 2) {
                                        bytes[i / 2] = parseInt(value.substr(i, 2), 16);
                                    }
                                    return bytes;
                                } else if (encoding === 'binary' || encoding === 'latin1') {
                                    var bytes = new Uint8Array(value.length);
                                    for (var i = 0; i < value.length; i++) {
                                        bytes[i] = value.charCodeAt(i) & 0xff;
                                    }
                                    return bytes;
                                }
                                return new TextEncoder().encode(value);
                            }
                            if (ArrayBuffer.isView(value)) {
                                return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
                            }
                            if (value instanceof ArrayBuffer) {
                                return new Uint8Array(value, encodingOrOffset, length);
                            }
                            if (Array.isArray(value)) {
                                return new Uint8Array(value);
                            }
                            return new Uint8Array(0);
                        };
                        
                        Buffer.alloc = function(size, fill, encoding) {
                            var buf = new Uint8Array(size);
                            if (fill !== undefined) {
                                if (typeof fill === 'number') {
                                    buf.fill(fill);
                                } else if (typeof fill === 'string') {
                                    var fillBuf = Buffer.from(fill, encoding);
                                    for (var i = 0; i < size; i++) {
                                        buf[i] = fillBuf[i % fillBuf.length];
                                    }
                                }
                            }
                            return buf;
                        };
                        
                        Buffer.allocUnsafe = function(size) {
                            return new Uint8Array(size);
                        };
                        
                        Buffer.allocUnsafeSlow = Buffer.allocUnsafe;
                        
                        Buffer.isBuffer = function(obj) {
                            return obj instanceof Uint8Array;
                        };
                        
                        Buffer.isEncoding = function(encoding) {
                            return ['utf8', 'utf-8', 'hex', 'base64', 'binary', 'latin1', 'ascii'].indexOf(encoding.toLowerCase()) !== -1;
                        };
                        
                        Buffer.byteLength = function(string, encoding) {
                            if (typeof string !== 'string') {
                                return string.length || string.byteLength || 0;
                            }
                            encoding = encoding || 'utf8';
                            if (encoding === 'utf8' || encoding === 'utf-8') {
                                return new TextEncoder().encode(string).length;
                            }
                            if (encoding === 'base64') {
                                return Math.ceil(string.length * 3 / 4);
                            }
                            if (encoding === 'hex') {
                                return string.length / 2;
                            }
                            return string.length;
                        };
                        
                        Buffer.concat = function(list, totalLength) {
                            if (list.length === 0) return new Uint8Array(0);
                            if (list.length === 1) return list[0];
                            
                            if (totalLength === undefined) {
                                totalLength = 0;
                                for (var i = 0; i < list.length; i++) {
                                    totalLength += list[i].length;
                                }
                            }
                            
                            var result = new Uint8Array(totalLength);
                            var offset = 0;
                            for (var i = 0; i < list.length; i++) {
                                result.set(list[i], offset);
                                offset += list[i].length;
                            }
                            return result;
                        };
                        
                        Buffer.compare = function(a, b) {
                            var len = Math.min(a.length, b.length);
                            for (var i = 0; i < len; i++) {
                                if (a[i] < b[i]) return -1;
                                if (a[i] > b[i]) return 1;
                            }
                            if (a.length < b.length) return -1;
                            if (a.length > b.length) return 1;
                            return 0;
                        };
                        
                        // Add prototype methods to Uint8Array for Buffer compatibility
                        if (!Uint8Array.prototype.toString_buffer) {
                            Uint8Array.prototype.toString_buffer = Uint8Array.prototype.toString;
                            Uint8Array.prototype.toString = function(encoding, start, end) {
                                if (!encoding || encoding === 'utf8' || encoding === 'utf-8') {
                                    var slice = this;
                                    if (start !== undefined || end !== undefined) {
                                        slice = this.slice(start || 0, end);
                                    }
                                    return new TextDecoder().decode(slice);
                                }
                                if (encoding === 'hex') {
                                    var hex = '';
                                    var s = start || 0;
                                    var e = end !== undefined ? end : this.length;
                                    for (var i = s; i < e; i++) {
                                        hex += this[i].toString(16).padStart(2, '0');
                                    }
                                    return hex;
                                }
                                if (encoding === 'base64') {
                                    var slice = this;
                                    if (start !== undefined || end !== undefined) {
                                        slice = this.slice(start || 0, end);
                                    }
                                    var binary = '';
                                    for (var i = 0; i < slice.length; i++) {
                                        binary += String.fromCharCode(slice[i]);
                                    }
                                    return btoa(binary);
                                }
                                return this.toString_buffer();
                            };
                        }
                        
                        return Buffer;
                    })(),
                    // Node.js compatibility - global reference
                    global: null  // Will be set to globalObj after creation
                },
                compiledScripts: {},
                hostObjects: {},
                objectInstances: {},
                modules: {},
                objectIdCounter: 0
            };
            // Set global to reference itself (Node.js compatibility)
            window.__dclJSContexts[contextIdStr].global.global = window.__dclJSContexts[contextIdStr].global;
            window.__dclJSContexts[contextIdStr].global.globalThis = window.__dclJSContexts[contextIdStr].global;
        }
        return 1;
    },
    
    JSContext_RegisterModule: function(contextId, moduleName, scriptId) {
        const contextIdStr = UTF8ToString(contextId);
        const moduleNameStr = UTF8ToString(moduleName);
        const scriptIdStr = UTF8ToString(scriptId);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        context.modules[moduleNameStr] = scriptIdStr;
        return 1;
    },
    
    JSContext_Execute: function(contextId, code) {
        const contextIdStr = UTF8ToString(contextId);
        const codeStr = UTF8ToString(code);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const globalObj = context.global;
            
            // Helper to generate global assignments from current context.global state
            // Skip browser built-ins that are already available globally to avoid redeclaration errors
            var browserBuiltins = [
                'console', 'window', 'document', 'location', 'navigator', 'history',
                'localStorage', 'sessionStorage', 'fetch', 'XMLHttpRequest', 'WebSocket',
                'performance', 'requestAnimationFrame', 'cancelAnimationFrame',
                'alert', 'confirm', 'prompt', 'atob', 'btoa', 'URL', 'URLSearchParams',
                'TextEncoder', 'TextDecoder', 'Blob', 'File', 'FileReader', 'FormData',
                'Image', 'Audio', 'Event', 'CustomEvent', 'MessageChannel', 'MessagePort',
                'Worker', 'SharedWorker', 'ServiceWorker', 'BroadcastChannel',
                'crypto', 'indexedDB', 'caches', 'AbortController', 'AbortSignal',
                'queueMicrotask', 'reportError', 'structuredClone'
            ];
            var getGlobalAssignments = function() {
                var keys = Object.keys(globalObj);
                return keys.map(function(key) {
                    // Skip browser built-ins to avoid conflicts
                    if (browserBuiltins.indexOf(key) !== -1) {
                        return '';
                    }
                    if (/^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(key)) {
                        return 'var ' + key + ' = globalThis[\'' + key.replace(/'/g, "\\'") + '\'];';
                    }
                    return '';
                }).filter(function(a) { return a.length > 0; }).join('\n');
            };
            
            // Create require function FIRST and add it to global
            // so that globalAssignments will include it
            var requireFunc = function(moduleName) {
                // console.log('[require] Loading module:', moduleName);
                
                var scriptId = context.modules[moduleName];
                if (!scriptId) {
                    console.warn('[require] Module not found:', moduleName, 'Available:', Object.keys(context.modules));
                    return {};
                }
                
                var compiledData = context.compiledScripts[scriptId];
                if (!compiledData || !compiledData.source) {
                    console.warn('[require] Compiled script not found for:', moduleName);
                    return {};
                }
                
                // Create module and exports objects that the script can use
                var moduleObj = { exports: {} };
                var exportsObj = moduleObj.exports;
                
                // Execute the module code with module/exports in scope
                var source = compiledData.source;
                                
                // Check if the source is wrapped in a CommonJS wrapper that needs to be called
                // Pattern: (function (exports, require, module, __filename, __dirname) { ... })
                // This wrapper is NOT self-invoking, so we need to call it
                var wrappedPattern = /^\s*\(function\s*\(\s*exports\s*,\s*require\s*,\s*module\s*,\s*__filename\s*,\s*__dirname\s*\)\s*\{/;
                
                if (wrappedPattern.test(source)) {
                    // console.log('[require] Detected CommonJS wrapper, will invoke it');
                    
                    // Evaluate the source to get the wrapper function, then call it
                    var moduleGlobalAssignments = getGlobalAssignments();
                    var evalCode = moduleGlobalAssignments + '\nreturn ' + source + ';';
                    
                    var getWrapperFunc;
                    try {
                        getWrapperFunc = new Function('globalThis', evalCode);
                    } catch (syntaxError) {
                        console.error('[require] Syntax error compiling wrapper getter:', moduleName, syntaxError);
                        return {};
                    }
                    
                    var wrapperFunc;
                    try {
                        wrapperFunc = getWrapperFunc(globalObj);
                        // console.log('[require] Got wrapper function, type:', typeof wrapperFunc);
                    } catch (evalError) {
                        console.error('[require] Error evaluating wrapper:', moduleName, evalError);
                        return {};
                    }
                    
                    // Now call the wrapper with the CommonJS arguments
                    // console.log('[require] Calling wrapper for', moduleName, '(this may take a while for large files)...');
                    var startTime = performance.now();
                    try {
                        wrapperFunc.call(globalObj, exportsObj, requireFunc, moduleObj, '/' + moduleName, '/');
                        var elapsed = performance.now() - startTime;
                        // console.log('[require] Wrapper executed successfully in', elapsed.toFixed(0), 'ms');
                    } catch (moduleError) {
                        console.error('[require] Error executing wrapper:', moduleName, moduleError);
                        console.error('[require] Stack:', moduleError.stack);
                        return {};
                    }
                } else {
                    // console.log('[require] No CommonJS wrapper detected, executing directly');
                    var moduleGlobalAssignments = getGlobalAssignments();
                    var moduleCode = moduleGlobalAssignments + '\n' + source;
                    
                    var moduleFunc;
                    try {
                        moduleFunc = new Function('globalThis', 'module', 'exports', moduleCode);
                    } catch (syntaxError) {
                        console.error('[require] Syntax error compiling module:', moduleName, syntaxError);
                        return {};
                    }
                    
                    try {
                        moduleFunc(globalObj, moduleObj, exportsObj);
                    } catch (moduleError) {
                        console.error('[require] Error executing module:', moduleName, moduleError);
                return {};
                    }
                }
                
                // Debug: log what we got
                // console.log('[require] module.exports keys:', Object.keys(moduleObj.exports || {}));
                // var onStartType = moduleObj.exports && moduleObj.exports.onStart ? typeof moduleObj.exports.onStart : 'undefined';
                // console.log('[require] Module loaded:', moduleName, 'exports.onStart:', onStartType);
                return moduleObj.exports;
            };
            
            // Add require to global so it's available via globalAssignments
            globalObj.require = requireFunc;
            
            // Now generate globalAssignments (which will include require)
            var globalAssignments = getGlobalAssignments();
            
            // Wrap the code to assign global properties and then execute
            var wrappedCode = globalAssignments + '\n' + codeStr;
            var func = new Function('globalThis', wrappedCode);
            
            func(globalObj);
            return 1;
        } catch (e) {
            console.error('JSContext_Execute error:', e);
            return 0;
        }
    },
    
    JSContext_Compile: function(contextId, code, scriptId) {
        const contextIdStr = UTF8ToString(contextId);
        const codeStr = UTF8ToString(code);
        const scriptIdStr = UTF8ToString(scriptId);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            // Store both the source code and a basic compiled function
            // The source code is needed for proper evaluation with globals exposed
            // The code is typically a function expression like: (function(exports, require, module, ...) { ... })
            context.compiledScripts[scriptIdStr] = {
                source: codeStr,
                // Also create a basic compiled function for fallback
                func: new Function('globalThis', 'require', 'return ' + codeStr)
            };
            return 1;
        } catch (e) {
            console.error('JSContext_Compile error:', e);
            return 0;
        }
    },
    
    JSContext_Evaluate: function(contextId, expression, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const exprStr = UTF8ToString(expression);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            // Create variable declarations for all object instances so they can be referenced
            const objectInstanceKeys = Object.keys(context.objectInstances);
            const objectInstanceDeclarations = objectInstanceKeys.map(key => {
                if (/^__obj_\d+$/.test(key)) {
                    return `var ${key} = __objectInstances['${key}'];`;
                }
                return '';
            }).filter(d => d.length > 0).join('\n');
            
            const wrappedExpr = objectInstanceDeclarations + '\nreturn ' + exprStr;
            const func = new Function('globalThis', '__objectInstances', wrappedExpr);
            const result = func(context.global, context.objectInstances);
            
            // Handle non-serializable results (functions, objects with circular refs, etc.)
            let resultStr;
            if (typeof result === 'function' || (typeof result === 'object' && result !== null)) {
                // Store the object/function and return a reference
                const objectId = '__obj_' + (++context.objectIdCounter);
                context.objectInstances[objectId] = result;
                // Return a special JSON that indicates this is an object reference
                resultStr = JSON.stringify({ __objectRef: objectId });
            } else {
                resultStr = JSON.stringify(result);
                // JSON.stringify returns undefined for undefined, handle that
                if (resultStr === undefined) {
                    resultStr = 'null';
                }
            }
            
            const len = lengthBytesUTF8(resultStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(resultStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_Evaluate error:', e);
            return 0;
        }
    },
    
    JSContext_EvaluateScript: function(contextId, scriptId, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const scriptIdStr = UTF8ToString(scriptId);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const compiledData = context.compiledScripts[scriptIdStr];
            if (!compiledData) return 0;
            
            // Expose globals as local variables
            const globalObj = context.global;
            const globalKeys = Object.keys(globalObj);
            const globalAssignments = globalKeys.map(key => {
                if (/^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(key)) {
                    return `var ${key} = __globalThis['${key.replace(/'/g, "\\'")}'];`;
                }
                return '';
            }).filter(a => a.length > 0).join('\n');
            
            const evalCode = globalAssignments + '\nreturn ' + compiledData.source;
            const evalFunc = new Function('__globalThis', '__require', evalCode);
            
            const result = evalFunc(globalObj, function(moduleName) {
                const moduleData = context.compiledScripts[moduleName];
                if (moduleData) {
                    const innerEvalCode = globalAssignments + '\nreturn ' + moduleData.source;
                    const innerEvalFunc = new Function('__globalThis', '__require', innerEvalCode);
                    return innerEvalFunc(globalObj);
                }
                return {};
            });
            
            // Handle non-serializable results (functions, objects with circular refs, etc.)
            let resultStr;
            if (typeof result === 'function' || (typeof result === 'object' && result !== null)) {
                // Store the object/function and return a reference
                const objectId = '__obj_' + (++context.objectIdCounter);
                context.objectInstances[objectId] = result;
                resultStr = JSON.stringify({ __objectRef: objectId });
            } else {
                resultStr = JSON.stringify(result);
                if (resultStr === undefined) {
                    resultStr = 'null';
                }
            }
            
            const len = lengthBytesUTF8(resultStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(resultStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_EvaluateScript error:', e);
            return 0;
        }
    },
    
    JSContext_AddHostObject__deps: ['$hostObjectCallback', '$hostObjectCallbackWithReturn'],
    JSContext_AddHostObject: function(contextId, name, objectId) {
        const contextIdStr = UTF8ToString(contextId);
        const nameStr = UTF8ToString(name);
        const objectIdStr = UTF8ToString(objectId);
        // console.log('[JSContext_AddHostObject] contextId:', contextIdStr, 'name:', nameStr);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) {
            console.error('[JSContext_AddHostObject] Context not found!');
            return 0;
        }
        // console.log('[JSContext_AddHostObject] context.global before add, has name?:', nameStr in context.global);
        
        context.hostObjects[objectIdStr] = nameStr;
        
        // Helper function to call the registered C# callback for host object method invocation (void return)
        const invokeHostObjectCallback = function(methodName, args) {
            if (!hostObjectCallback) {
                console.warn('[JSContext] No host object callback registered');
                return;
            }
            
            // Allocate strings on the heap for the callback
            const contextIdLen = lengthBytesUTF8(contextIdStr) + 1;
            const objectIdLen = lengthBytesUTF8(objectIdStr) + 1;
            const methodNameLen = lengthBytesUTF8(methodName) + 1;
            const argsJson = JSON.stringify(args || []);
            const argsJsonLen = lengthBytesUTF8(argsJson) + 1;
            
            const contextIdPtr = _malloc(contextIdLen);
            const objectIdPtr = _malloc(objectIdLen);
            const methodNamePtr = _malloc(methodNameLen);
            const argsJsonPtr = _malloc(argsJsonLen);
            
            try {
                stringToUTF8(contextIdStr, contextIdPtr, contextIdLen);
                stringToUTF8(objectIdStr, objectIdPtr, objectIdLen);
                stringToUTF8(methodName, methodNamePtr, methodNameLen);
                stringToUTF8(argsJson, argsJsonPtr, argsJsonLen);
                
                // Call the registered C# callback
                {{{ makeDynCall('viiii', 'hostObjectCallback') }}}(contextIdPtr, objectIdPtr, methodNamePtr, argsJsonPtr);
            } finally {
                _free(contextIdPtr);
                _free(objectIdPtr);
                _free(methodNamePtr);
                _free(argsJsonPtr);
            }
        };
        
        // Helper function to serialize arguments with special handling for typed arrays
        const serializeArgs = function(args) {
            if (!args || args.length === 0) return '[]';
            
            const serializedArgs = args.map(arg => {
                // Handle Uint8Array - convert to base64 with type marker
                if (arg instanceof Uint8Array) {
                    let binary = '';
                    for (let i = 0; i < arg.length; i++) {
                        binary += String.fromCharCode(arg[i]);
                    }
                    return { __type: 'ByteArray', data: btoa(binary), length: arg.length };
                }
                // Handle other typed arrays
                if (ArrayBuffer.isView(arg)) {
                    const bytes = new Uint8Array(arg.buffer, arg.byteOffset, arg.byteLength);
                    let binary = '';
                    for (let i = 0; i < bytes.length; i++) {
                        binary += String.fromCharCode(bytes[i]);
                    }
                    return { __type: 'ByteArray', data: btoa(binary), length: bytes.length };
                }
                return arg;
            });
            
            return JSON.stringify(serializedArgs);
        };
        
        // Helper function to call C# and get a return value
        const invokeHostObjectCallbackWithReturn = function(methodName, args) {
            if (!hostObjectCallbackWithReturn) {
                console.warn('[JSContext] No host object callback with return registered');
                return null;
            }
            
            // Allocate strings on the heap for the callback
            const contextIdLen = lengthBytesUTF8(contextIdStr) + 1;
            const objectIdLen = lengthBytesUTF8(objectIdStr) + 1;
            const methodNameLen = lengthBytesUTF8(methodName) + 1;
            const argsJson = serializeArgs(args);
            const argsJsonLen = lengthBytesUTF8(argsJson) + 1;
            
            // Allocate result buffer (start with 64KB, can grow if needed)
            const resultBufferSize = 65536;
            
            const contextIdPtr = _malloc(contextIdLen);
            const objectIdPtr = _malloc(objectIdLen);
            const methodNamePtr = _malloc(methodNameLen);
            const argsJsonPtr = _malloc(argsJsonLen);
            const resultBufferPtr = _malloc(resultBufferSize);
            
            try {
                stringToUTF8(contextIdStr, contextIdPtr, contextIdLen);
                stringToUTF8(objectIdStr, objectIdPtr, objectIdLen);
                stringToUTF8(methodName, methodNamePtr, methodNameLen);
                stringToUTF8(argsJson, argsJsonPtr, argsJsonLen);
                
                // Call the registered C# callback with return
                const resultLen = {{{ makeDynCall('iiiiii', 'hostObjectCallbackWithReturn') }}}(
                    contextIdPtr, objectIdPtr, methodNamePtr, argsJsonPtr, resultBufferPtr, resultBufferSize);
                
                if (resultLen < 0) {
                    console.warn('[JSContext] Result buffer too small, needed:', -resultLen);
                    return null;
                }
                
                if (resultLen === 0) {
                    return null;
                }
                
                // Read the result string from the buffer
                const resultStr = UTF8ToString(resultBufferPtr, resultLen);
                
                // Parse and deserialize the result
                return deserializeResult(resultStr, context);
            } finally {
                _free(contextIdPtr);
                _free(objectIdPtr);
                _free(methodNamePtr);
                _free(argsJsonPtr);
                _free(resultBufferPtr);
            }
        };
        
        // Helper function to deserialize C# result with special type handling
        // Note: contextIdStr is captured from outer closure in JSContext_AddHostObject
        const deserializeResult = function(resultStr, context) {
            // console.log('[deserializeResult] contextId:', contextIdStr, 'Input resultStr:', typeof resultStr, resultStr ? resultStr.substring(0, 200) : resultStr);
            // console.log('[deserializeResult] Context objectInstances keys:', Object.keys(context.objectInstances));
            
            if (!resultStr || resultStr === 'null') {
                // console.log('[deserializeResult] Returning null (empty or null string)');
                return null;
            }
            
            try {
                const parsed = JSON.parse(resultStr);
                // console.log('[deserializeResult] Parsed type:', typeof parsed, Array.isArray(parsed) ? 'array' : '');
                
                // Handle special types (objects with markers)
                if (parsed && typeof parsed === 'object') {
                    // ByteArray type - convert Base64 to Uint8Array
                    if (parsed.__type === 'ByteArray') {
                        // console.log('[deserializeResult] Returning ByteArray');
                        if (parsed.isEmpty) {
                            return new Uint8Array(0);
                        }
                        // Decode Base64 to Uint8Array
                        const binaryStr = atob(parsed.data);
                        const bytes = new Uint8Array(binaryStr.length);
                        for (let i = 0; i < binaryStr.length; i++) {
                            bytes[i] = binaryStr.charCodeAt(i);
                        }
                        return bytes;
                    }
                    
                    // Object reference - look up in context
                    if (parsed.__objectRef) {
                        const obj = context.objectInstances[parsed.__objectRef];
                        // console.log('[deserializeResult] Returning objectRef:', parsed.__objectRef, 'found:', !!obj);
                        return obj || parsed;
                    }
                    
                    // Error response
                    if (parsed.error) {
                        console.warn('[JSContext] C# method returned error:', parsed.error);
                        return null;
                    }
                    
                    // Plain object without special markers - return unparsed string
                    // so the caller (SDK) can parse it if needed
                    // console.log('[deserializeResult] Plain object, returning unparsed string');
                    return resultStr;
                }
                
                // Primitives (string, number, boolean) - return parsed value
                // console.log('[deserializeResult] Returning primitive:', typeof parsed);
                return parsed;
            } catch (e) {
                // If JSON parse fails, return the raw string
                // console.log('[deserializeResult] JSON parse failed, returning raw string:', e.message);
                return resultStr;
            }
        };
        
        // Create a proxy that handles host object methods
        // Different objects get different stub implementations
        const hostObjectName = nameStr;
        const hostObjectProxy = new Proxy({}, {
            get: function(target, prop, receiver) {
                const methodName = String(prop);
                
                // Return a function for any property access
                return function(...args) {
                    try {
                        // === UnityOpsApi methods ===
                        if (hostObjectName === 'UnityOpsApi') {
                            // Handle LoadAndEvaluateCode by looking up pre-registered modules
                            if (methodName === 'LoadAndEvaluateCode') {
                                const moduleName = args[0];
                                const scriptId = context.modules[moduleName];
                                if (!scriptId) {
                                    console.warn('Module not found:', moduleName);
                                    return null;
                                }
                                const compiledData = context.compiledScripts[scriptId];
                                if (!compiledData) {
                                    console.warn('Compiled script not found for module:', moduleName, 'scriptId:', scriptId);
                                    return null;
                                }
                                
                                // Evaluate the module - globals are accessed via globalThis
                                // We don't create local variable assignments to avoid caching API stubs
                                const globalObj = context.global;
                                
                                // Create evaluation function - code should access globals via globalThis
                                const evalCode = 'return ' + compiledData.source;
                                const evalFunc = new Function('globalThis', '__require', evalCode);
                                
                                const internalRequire = function(innerModuleName) {
                                    const innerScriptId = context.modules[innerModuleName];
                                    const innerData = innerScriptId ? context.compiledScripts[innerScriptId] : null;
                                    if (innerData) {
                                        const innerEvalCode = 'return ' + innerData.source;
                                        const innerEvalFunc = new Function('globalThis', '__require', innerEvalCode);
                                        return innerEvalFunc(globalObj, internalRequire);
                                    }
                                    return {};
                                };
                                
                                return evalFunc(globalObj, internalRequire);
                            }
                            if (methodName === 'Log') { console.log('[Scene]', args[0]); return null; }
                            if (methodName === 'Warning') { console.warn('[Scene]', args[0]); return null; }
                            if (methodName === 'Error') { console.error('[Scene]', args[0]); return null; }
                        }
                        
                        // === Generic handler for all other registered host objects ===
                        // This calls the actual C# method via the callback mechanism
                        // Skip for __resetableSource which has special void-return handling below
                        if (hostObjectName !== '__resetableSource') {
                            // Call the C# method and get the return value
                            // console.log('[HostObject] Calling:', hostObjectName + '.' + methodName);
                            const result = invokeHostObjectCallbackWithReturn(methodName, args);
                            // console.log('[HostObject] Result:', typeof result, result);
                            return result;
                        }
                        
                        // === __resetableSource (internal) ===
                        // This must call back to C# to signal completion/rejection
                        if (hostObjectName === '__resetableSource') {
                            if (methodName === 'Completed') {
                                // Call the C# Completed() method via callback
                                invokeHostObjectCallback('Completed', []);
                                return;
                            }
                            if (methodName === 'Reject') {
                                console.error('[Scene Error]', args[0]);
                                // Call the C# Reject() method via callback
                                invokeHostObjectCallback('Reject', [args[0]]);
                                return;
                            }
                            if (methodName === 'Reset') {
                                // Reset is called from C# side, no need to call back
                                return;
                            }
                        }
                        
                        // === Default: log and return null ===
                        console.warn('[STUB] Unknown method called:', hostObjectName + '.' + methodName, 'args:', args);
                        return null;
                    } catch (e) {
                        console.error('Error in host object method call:', hostObjectName + '.' + methodName, e);
                        throw e;
                    }
                };
            },
            has: function(target, prop) {
                // Always return true to make the object appear to have all properties
                return true;
            }
        });
        
        context.global[nameStr] = hostObjectProxy;
        // console.log('[JSContext_AddHostObject] Added to context.global, verify:', nameStr in context.global, typeof context.global[nameStr]);
        return 1;
    },
    
    JSContext_GetGlobalProperty: function(contextId, name, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const nameStr = UTF8ToString(name);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const value = context.global[nameStr];
            const valueStr = JSON.stringify(value);
            const len = lengthBytesUTF8(valueStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(valueStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            return 0;
        }
    },
    
    JSContext_InvokeFunction: function(contextId, functionName, argsJson, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const funcNameStr = UTF8ToString(functionName);
        const argsJsonStr = UTF8ToString(argsJson);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const func = context.global[funcNameStr];
            if (typeof func !== 'function') return 0;
            const args = JSON.parse(argsJsonStr);
            const result = func.apply(context.global, args);
            const resultStr = JSON.stringify(result);
            const len = lengthBytesUTF8(resultStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(resultStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_InvokeFunction error:', e);
            return 0;
        }
    },
    
    JSContext_Dispose: function(contextId) {
        const contextIdStr = UTF8ToString(contextId);
        if (window.__dclJSContexts && window.__dclJSContexts[contextIdStr]) {
            delete window.__dclJSContexts[contextIdStr];
        }
        return 1;
    },
    
    JSContext_GetStackTrace: function(contextId, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        try {
            const stack = new Error().stack || '';
            const len = lengthBytesUTF8(stack) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(stack, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            return 0;
        }
    },
    
    JSContext_StoreObject: function(contextId, expression, objectIdPtr, objectIdSize) {
        const contextIdStr = UTF8ToString(contextId);
        const exprStr = UTF8ToString(expression);
        // console.log('[JSContext_StoreObject] contextId:', contextIdStr);
        // console.log('[JSContext_StoreObject] expression (first 500 chars):', exprStr.substring(0, 500));
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) {
            console.error('[JSContext_StoreObject] Context not found for:', contextIdStr);
            return 0;
        }
        
        try {
            // Create variable declarations for all object instances so they can be referenced in the expression
            // This allows expressions like "new __obj_1(...)" to work
            const objectInstanceKeys = Object.keys(context.objectInstances);
            const objectInstanceDeclarations = objectInstanceKeys.map(key => {
                if (/^__obj_\d+$/.test(key)) {
                    return `var ${key} = __objectInstances['${key}'];`;
                }
                return '';
            }).filter(d => d.length > 0).join('\n');
            
            const wrappedExpr = objectInstanceDeclarations + '\nreturn ' + exprStr;
            // console.log('[JSContext_StoreObject] wrappedExpr (first 600 chars):', wrappedExpr.substring(0, 600));
            
            // Check for promise resolvers specifically
            // const allKeys = Object.keys(context.global);
            // const resolverKeys = allKeys.filter(k => k.startsWith('__promiseResolver'));
            // console.log('[JSContext_StoreObject] context.global total keys:', allKeys.length);
            // console.log('[JSContext_StoreObject] Resolver keys found:', resolverKeys);
            
            // Also check if any Unity APIs are registered
            // const unityKeys = allKeys.filter(k => k.startsWith('Unity') || k.startsWith('__'));
            // console.log('[JSContext_StoreObject] Unity/internal keys:', unityKeys);
            
            const func = new Function('globalThis', '__objectInstances', 'console', wrappedExpr);
            // console.log('[JSContext_StoreObject] Function created successfully');
            const obj = func(context.global, context.objectInstances, console);
            // console.log('[JSContext_StoreObject] Function executed, result:', obj, 'type:', typeof obj);
            const objectId = '__obj_' + (++context.objectIdCounter);
            context.objectInstances[objectId] = obj;
            // console.log('[JSContext_StoreObject] Stored object:', objectId, 'type:', typeof obj, 'isPromise:', obj instanceof Promise);
            const len = lengthBytesUTF8(objectId) + 1;
            if (objectIdSize < len) {
                return -len;
            }
            stringToUTF8(objectId, objectIdPtr, objectIdSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_StoreObject error:', e);
            console.error('JSContext_StoreObject stack:', e.stack);
            return 0;
        }
    },
    
    JSContext_GetObjectProperty: function(contextId, objectId, name, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const objectIdStr = UTF8ToString(objectId);
        const nameStr = UTF8ToString(name);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const obj = context.objectInstances[objectIdStr];
            if (!obj) return 0;
            const value = obj[nameStr];
            const valueStr = JSON.stringify(value);
            const len = lengthBytesUTF8(valueStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(valueStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            return 0;
        }
    },
    
    JSContext_SetObjectProperty: function(contextId, objectId, name, valueJson) {
        const contextIdStr = UTF8ToString(contextId);
        const objectIdStr = UTF8ToString(objectId);
        const nameStr = UTF8ToString(name);
        const valueJsonStr = UTF8ToString(valueJson);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const obj = context.objectInstances[objectIdStr];
            if (!obj) return 0;
            const value = JSON.parse(valueJsonStr);
            obj[nameStr] = value;
            return 1;
        } catch (e) {
            console.error('JSContext_SetObjectProperty error:', e);
            return 0;
        }
    },
    
    JSContext_InvokeObjectMethod: function(contextId, objectId, methodName, argsJson, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const objectIdStr = UTF8ToString(objectId);
        const methodNameStr = UTF8ToString(methodName);
        const argsJsonStr = UTF8ToString(argsJson);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const obj = context.objectInstances[objectIdStr];
            if (!obj) return 0;
            const method = obj[methodNameStr];
            if (typeof method !== 'function') return 0;
            const args = JSON.parse(argsJsonStr);
            const result = method.apply(obj, args);
            const resultStr = JSON.stringify(result);
            const len = lengthBytesUTF8(resultStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(resultStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_InvokeObjectMethod error:', e);
            return 0;
        }
    },
    
    JSContext_InvokeObjectAsFunction: function(contextId, objectId, argsJson, resultPtr, resultSize) {
        const contextIdStr = UTF8ToString(contextId);
        const objectIdStr = UTF8ToString(objectId);
        const argsJsonStr = UTF8ToString(argsJson);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const obj = context.objectInstances[objectIdStr];
            if (!obj) return 0;
            if (typeof obj !== 'function') return 0;
            const args = JSON.parse(argsJsonStr);
            const result = obj.apply(null, args);
            const resultStr = JSON.stringify(result);
            const len = lengthBytesUTF8(resultStr) + 1;
            if (resultSize < len) {
                return -len;
            }
            stringToUTF8(resultStr, resultPtr, resultSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_InvokeObjectAsFunction error:', e);
            return 0;
        }
    }
});
