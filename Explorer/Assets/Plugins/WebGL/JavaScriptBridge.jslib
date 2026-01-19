mergeInto(LibraryManager.library, {
    // Global storage for the host object callback function pointer
    $hostObjectCallback: null,
    
    // Register the callback function that will be used to invoke host object methods
    JSContext_RegisterHostObjectCallback: function(callback) {
        hostObjectCallback = callback;
    },
    JSContext_RegisterHostObjectCallback__deps: ['$hostObjectCallback'],
    
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
                    // Special values
                    undefined: undefined,
                    NaN: NaN,
                    Infinity: Infinity,
                    // Provide stub WebAssembly object so Init.js can disable it without crashing
                    // Init.js sets WebAssembly.Instance and WebAssembly.Module to throw errors
                    WebAssembly: {
                        Instance: function() { throw new Error('Wasm is not allowed in scene runtimes'); },
                        Module: function() { throw new Error('Wasm is not allowed in scene runtimes'); }
                    }
                },
                compiledScripts: {},
                hostObjects: {},
                objectInstances: {},
                modules: {},
                objectIdCounter: 0
            };
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
            // Create a function that exposes all properties from context.global as variables
            // This allows code to access UnityOpsApi, console, etc. directly
            const globalObj = context.global;
            const globalKeys = Object.keys(globalObj);
            const globalAssignments = globalKeys.map(key => {
                // Only create variable assignments for valid JavaScript identifiers
                // This allows direct access like "UnityOpsApi" instead of "globalThis.UnityOpsApi"
                if (/^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(key)) {
                    return `var ${key} = globalThis['${key.replace(/'/g, "\\'")}'];`;
                }
                return ''; // Skip invalid identifiers
            }).filter(assignment => assignment.length > 0).join('\n');
            
            // Wrap the code to assign global properties and then execute
            const wrappedCode = globalAssignments + '\n' + codeStr;
            const func = new Function('globalThis', 'require', wrappedCode);
            func(context.global, function(moduleName) {
                const compiledData = context.compiledScripts[moduleName];
                if (compiledData && compiledData.source) {
                    // Evaluate with globals exposed
                    const innerCode = globalAssignments + '\nreturn ' + compiledData.source;
                    const innerFunc = new Function('globalThis', '__require', innerCode);
                    return innerFunc(context.global, function() { return {}; });
                }
                return {};
            });
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
    
    JSContext_AddHostObject__deps: ['$hostObjectCallback'],
    JSContext_AddHostObject: function(contextId, name, objectId) {
        const contextIdStr = UTF8ToString(contextId);
        const nameStr = UTF8ToString(name);
        const objectIdStr = UTF8ToString(objectId);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        context.hostObjects[objectIdStr] = nameStr;
        
        // Helper function to call the registered C# callback for host object method invocation
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
                                
                                // Evaluate the module with globals exposed as local variables
                                // This allows modules to access UnityEngineApi, etc. directly
                                const globalObj = context.global;
                                const globalKeys = Object.keys(globalObj);
                                const globalAssignments = globalKeys.map(key => {
                                    if (/^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(key)) {
                                        return `var ${key} = __globalThis['${key.replace(/'/g, "\\'")}'];`;
                                    }
                                    return '';
                                }).filter(a => a.length > 0).join('\n');
                                
                                // Create evaluation function with globals exposed
                                const evalCode = globalAssignments + '\nreturn ' + compiledData.source;
                                const evalFunc = new Function('__globalThis', '__require', evalCode);
                                
                                const internalRequire = function(innerModuleName) {
                                    const innerScriptId = context.modules[innerModuleName];
                                    const innerData = innerScriptId ? context.compiledScripts[innerScriptId] : null;
                                    if (innerData) {
                                        // Recursively evaluate with globals
                                        const innerEvalCode = globalAssignments + '\nreturn ' + innerData.source;
                                        const innerEvalFunc = new Function('__globalThis', '__require', innerEvalCode);
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
                        
                        // === UnityEngineApi methods (STUB) ===
                        if (hostObjectName === 'UnityEngineApi') {
                            // CrdtGetState returns empty byte array (no initial state)
                            if (methodName === 'CrdtGetState') {
                                return { data: new Uint8Array(0), IsEmpty: true };
                            }
                            // CrdtSendToRenderer accepts data and returns empty response
                            if (methodName === 'CrdtSendToRenderer') {
                                // Log that we received CRDT data (for debugging)
                                console.log('[STUB] CrdtSendToRenderer received data');
                                return { data: new Uint8Array(0), IsEmpty: true };
                            }
                            // SendBatch returns null
                            if (methodName === 'SendBatch') {
                                return null;
                            }
                        }
                        
                        // === UnitySceneApi methods (STUB) ===
                        if (hostObjectName === 'UnitySceneApi') {
                            // Most scene API methods can return empty/default values
                            console.log('[STUB] UnitySceneApi.' + methodName + ' called');
                            return null;
                        }
                        
                        // === CommsApi methods (STUB) ===
                        if (hostObjectName === 'CommsApi') {
                            console.log('[STUB] CommsApi.' + methodName + ' called');
                            return null;
                        }
                        
                        // === UnityRestrictedActionsApi methods (STUB) ===
                        if (hostObjectName === 'UnityRestrictedActionsApi') {
                            console.log('[STUB] UnityRestrictedActionsApi.' + methodName + ' called');
                            return Promise.resolve(null);
                        }
                        
                        // === UnityEthereumApi methods (STUB) ===
                        if (hostObjectName === 'UnityEthereumApi') {
                            console.log('[STUB] UnityEthereumApi.' + methodName + ' called');
                            return Promise.resolve(null);
                        }
                        
                        // === UnityUserIdentityApi methods (STUB) ===
                        if (hostObjectName === 'UnityUserIdentityApi') {
                            console.log('[STUB] UnityUserIdentityApi.' + methodName + ' called');
                            return Promise.resolve({ userId: 'stub-user-id', isGuest: true });
                        }
                        
                        // === UnityWebSocketApi methods (STUB) ===
                        if (hostObjectName === 'UnityWebSocketApi') {
                            console.log('[STUB] UnityWebSocketApi.' + methodName + ' called');
                            return null;
                        }
                        
                        // === UnityCommunicationsControllerApi methods (STUB) ===
                        if (hostObjectName === 'UnityCommunicationsControllerApi') {
                            console.log('[STUB] UnityCommunicationsControllerApi.' + methodName + ' called');
                            return null;
                        }
                        
                        // === UnitySimpleFetchApi methods (STUB) ===
                        if (hostObjectName === 'UnitySimpleFetchApi') {
                            console.log('[STUB] UnitySimpleFetchApi.' + methodName + ' called');
                            return Promise.resolve({ ok: true, status: 200, body: '{}' });
                        }
                        
                        // === UnitySDKMessageBusCommsControllerApi methods (STUB) ===
                        if (hostObjectName === 'UnitySDKMessageBusCommsControllerApi') {
                            console.log('[STUB] UnitySDKMessageBusCommsControllerApi.' + methodName + ' called');
                            return null;
                        }
                        
                        // === UnityPortableExperiencesApi methods (STUB) ===
                        if (hostObjectName === 'UnityPortableExperiencesApi') {
                            console.log('[STUB] UnityPortableExperiencesApi.' + methodName + ' called');
                            return Promise.resolve(null);
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
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
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
            const func = new Function('globalThis', '__objectInstances', wrappedExpr);
            const obj = func(context.global, context.objectInstances);
            const objectId = '__obj_' + (++context.objectIdCounter);
            context.objectInstances[objectId] = obj;
            const len = lengthBytesUTF8(objectId) + 1;
            if (objectIdSize < len) {
                return -len;
            }
            stringToUTF8(objectId, objectIdPtr, objectIdSize);
            return len - 1;
        } catch (e) {
            console.error('JSContext_StoreObject error:', e);
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
