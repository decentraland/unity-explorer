mergeInto(LibraryManager.library, {
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
                if (context.compiledScripts[moduleName]) {
                    return context.compiledScripts[moduleName]();
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
            const func = new Function('globalThis', 'require', codeStr);
            context.compiledScripts[scriptIdStr] = func;
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
            if (!context.compiledScripts[scriptIdStr]) return 0;
            const func = context.compiledScripts[scriptIdStr];
            const result = func(context.global, function(moduleName) {
                if (context.compiledScripts[moduleName]) {
                    return context.compiledScripts[moduleName]();
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
    
    // Internal helper function to evaluate a compiled script and return the result directly
    // This is used by the UnityOpsApi proxy for LoadAndEvaluateCode
    $evaluateCompiledScriptInternal: function(context, scriptId) {
        if (!context.compiledScripts[scriptId]) return null;
        const func = context.compiledScripts[scriptId];
        const result = func(context.global, function(moduleName) {
            // Look up the module in the registered modules
            const moduleScriptId = context.modules[moduleName];
            if (moduleScriptId && context.compiledScripts[moduleScriptId]) {
                return context.compiledScripts[moduleScriptId](context.global);
            }
            return {};
        });
        return result;
    },
    
    JSContext_AddHostObject: function(contextId, name, objectId) {
        const contextIdStr = UTF8ToString(contextId);
        const nameStr = UTF8ToString(name);
        const objectIdStr = UTF8ToString(objectId);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        context.hostObjects[objectIdStr] = nameStr;
        
        // Create a proxy that handles UnityOpsApi methods directly in JavaScript
        // This avoids the need for JS->C# callbacks which don't work with Module.ccall
        const hostObjectProxy = new Proxy({}, {
            get: function(target, prop, receiver) {
                const methodName = String(prop);
                
                // Return a function for any property access
                return function(...args) {
                    try {
                        // Handle LoadAndEvaluateCode by looking up pre-registered modules
                        if (methodName === 'LoadAndEvaluateCode') {
                            const moduleName = args[0];
                            const scriptId = context.modules[moduleName];
                            if (!scriptId) {
                                console.warn('Module not found:', moduleName);
                                return null;
                            }
                            // Evaluate the pre-compiled script and return the function
                            if (!context.compiledScripts[scriptId]) {
                                console.warn('Compiled script not found for module:', moduleName, 'scriptId:', scriptId);
                                return null;
                            }
                            return context.compiledScripts[scriptId];
                        }
                        
                        // Handle logging methods by routing to JavaScript console
                        if (methodName === 'Log') {
                            console.log(args[0]);
                            return null;
                        }
                        if (methodName === 'Warning') {
                            console.warn(args[0]);
                            return null;
                        }
                        if (methodName === 'Error') {
                            console.error(args[0]);
                            return null;
                        }
                        
                        // For any other method, log a warning
                        console.warn('Unknown UnityOpsApi method called:', methodName, 'args:', args);
                        return null;
                    } catch (e) {
                        console.error('Error in host object method call:', methodName, e);
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
