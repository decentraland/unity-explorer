mergeInto(LibraryManager.library, {
    JSContext_Create: function(contextId) {
        const contextIdStr = UTF8ToString(contextId);
        if (!window.__dclJSContexts) {
            window.__dclJSContexts = {};
        }
        if (!window.__dclJSContexts[contextIdStr]) {
            window.__dclJSContexts[contextIdStr] = {
                global: {},
                compiledScripts: {},
                hostObjects: {},
                objectInstances: {},
                objectIdCounter: 0
            };
        }
        return 1;
    },
    
    JSContext_Execute: function(contextId, code) {
        const contextIdStr = UTF8ToString(contextId);
        const codeStr = UTF8ToString(code);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        try {
            const func = new Function('globalThis', 'require', codeStr);
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
            const func = new Function('globalThis', 'return ' + exprStr);
            const result = func(context.global);
            const resultStr = JSON.stringify(result);
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
            const resultStr = JSON.stringify(result);
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
    
    JSContext_AddHostObject: function(contextId, name, objectId) {
        const contextIdStr = UTF8ToString(contextId);
        const nameStr = UTF8ToString(name);
        const objectIdStr = UTF8ToString(objectId);
        const context = window.__dclJSContexts[contextIdStr];
        if (!context) return 0;
        
        context.hostObjects[objectIdStr] = nameStr;
        context.global[nameStr] = {
            __objectId: objectIdStr,
            __invoke: function(methodName, argsJson) {
                return Module.ccall('JSHostObject_Invoke', 'string', ['string', 'string', 'string', 'string'], [contextIdStr, objectIdStr, methodName, argsJson]);
            }
        };
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
            const func = new Function('globalThis', 'return ' + exprStr);
            const obj = func(context.global);
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
