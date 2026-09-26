"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __commonJS = (cb, mod2) => function __require() {
    return mod2 || (0, cb[__getOwnPropNames(cb)[0]])((mod2 = { exports: {} }).exports, mod2), mod2.exports;
};
var __export = (target, all) => {
    for (var name in all)
        __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
        for (let key of __getOwnPropNames(from))
            if (!__hasOwnProp.call(to, key) && key !== except)
                __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
};
var __toESM = (mod2, isNodeMode, target) => (target = mod2 != null ? __create(__getProtoOf(mod2)) : {}, __copyProps(
    // If the importer is in node compatibility mode or this is not an ESM
    // file that has been converted to a CommonJS file using a Babel-
    // compatible transform (i.e. "__esModule" has not been set), then set
    // "default" to the CommonJS "module.exports" for node compatibility.
    isNodeMode || !mod2 || !mod2.__esModule ? __defProp(target, "default", { value: mod2, enumerable: true }) : target,
    mod2
));
var __toCommonJS = (mod2) => __copyProps(__defProp({}, "__esModule", { value: true }), mod2);

// node_modules/@protobufjs/aspromise/index.js
var require_aspromise = __commonJS({
    "node_modules/@protobufjs/aspromise/index.js"(exports2, module2) {
        "use strict";
        module2.exports = asPromise;
        function asPromise(fn, ctx) {
            var params = new Array(arguments.length - 1), offset = 0, index = 2, pending = true;
            while (index < arguments.length)
                params[offset++] = arguments[index++];
            return new Promise(function executor(resolve2, reject) {
                params[offset] = function callback(err) {
                    if (pending) {
                        pending = false;
                        if (err)
                            reject(err);
                        else {
                            var params2 = new Array(arguments.length - 1), offset2 = 0;
                            while (offset2 < params2.length)
                                params2[offset2++] = arguments[offset2];
                            resolve2.apply(null, params2);
                        }
                    }
                };
                try {
                    fn.apply(ctx || null, params);
                } catch (err) {
                    if (pending) {
                        pending = false;
                        reject(err);
                    }
                }
            });
        }
    }
});

// node_modules/@protobufjs/base64/index.js
var require_base64 = __commonJS({
    "node_modules/@protobufjs/base64/index.js"(exports2) {
        "use strict";
        var base64 = exports2;
        base64.length = function length2(string) {
            var p = string.length;
            if (!p)
                return 0;
            var n = 0;
            while (--p % 4 > 1 && string.charAt(p) === "=")
                ++n;
            return Math.ceil(string.length * 3) / 4 - n;
        };
        var b64 = new Array(64);
        var s64 = new Array(123);
        for (i = 0; i < 64; )
            s64[b64[i] = i < 26 ? i + 65 : i < 52 ? i + 71 : i < 62 ? i - 4 : i - 59 | 43] = i++;
        var i;
        base64.encode = function encode(buffer, start, end) {
            var parts = null, chunk = [];
            var i2 = 0, j = 0, t;
            while (start < end) {
                var b = buffer[start++];
                switch (j) {
                    case 0:
                        chunk[i2++] = b64[b >> 2];
                        t = (b & 3) << 4;
                        j = 1;
                        break;
                    case 1:
                        chunk[i2++] = b64[t | b >> 4];
                        t = (b & 15) << 2;
                        j = 2;
                        break;
                    case 2:
                        chunk[i2++] = b64[t | b >> 6];
                        chunk[i2++] = b64[b & 63];
                        j = 0;
                        break;
                }
                if (i2 > 8191) {
                    (parts || (parts = [])).push(String.fromCharCode.apply(String, chunk));
                    i2 = 0;
                }
            }
            if (j) {
                chunk[i2++] = b64[t];
                chunk[i2++] = 61;
                if (j === 1)
                    chunk[i2++] = 61;
            }
            if (parts) {
                if (i2)
                    parts.push(String.fromCharCode.apply(String, chunk.slice(0, i2)));
                return parts.join("");
            }
            return String.fromCharCode.apply(String, chunk.slice(0, i2));
        };
        var invalidEncoding = "invalid encoding";
        base64.decode = function decode(string, buffer, offset) {
            var start = offset;
            var j = 0, t;
            for (var i2 = 0; i2 < string.length; ) {
                var c = string.charCodeAt(i2++);
                if (c === 61 && j > 1)
                    break;
                if ((c = s64[c]) === void 0)
                    throw Error(invalidEncoding);
                switch (j) {
                    case 0:
                        t = c;
                        j = 1;
                        break;
                    case 1:
                        buffer[offset++] = t << 2 | (c & 48) >> 4;
                        t = c;
                        j = 2;
                        break;
                    case 2:
                        buffer[offset++] = (t & 15) << 4 | (c & 60) >> 2;
                        t = c;
                        j = 3;
                        break;
                    case 3:
                        buffer[offset++] = (t & 3) << 6 | c;
                        j = 0;
                        break;
                }
            }
            if (j === 1)
                throw Error(invalidEncoding);
            return offset - start;
        };
        base64.test = function test(string) {
            return /^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(string);
        };
    }
});

// node_modules/@protobufjs/eventemitter/index.js
var require_eventemitter = __commonJS({
    "node_modules/@protobufjs/eventemitter/index.js"(exports2, module2) {
        "use strict";
        module2.exports = EventEmitter;
        function EventEmitter() {
            this._listeners = {};
        }
        EventEmitter.prototype.on = function on(evt, fn, ctx) {
            (this._listeners[evt] || (this._listeners[evt] = [])).push({
                fn,
                ctx: ctx || this
            });
            return this;
        };
        EventEmitter.prototype.off = function off(evt, fn) {
            if (evt === void 0)
                this._listeners = {};
            else {
                if (fn === void 0)
                    this._listeners[evt] = [];
                else {
                    var listeners = this._listeners[evt];
                    for (var i = 0; i < listeners.length; )
                        if (listeners[i].fn === fn)
                            listeners.splice(i, 1);
                        else
                            ++i;
                }
            }
            return this;
        };
        EventEmitter.prototype.emit = function emit(evt) {
            var listeners = this._listeners[evt];
            if (listeners) {
                var args = [], i = 1;
                for (; i < arguments.length; )
                    args.push(arguments[i++]);
                for (i = 0; i < listeners.length; )
                    listeners[i].fn.apply(listeners[i++].ctx, args);
            }
            return this;
        };
    }
});

// node_modules/@protobufjs/float/index.js
var require_float = __commonJS({
    "node_modules/@protobufjs/float/index.js"(exports2, module2) {
        "use strict";
        module2.exports = factory(factory);
        function factory(exports3) {
            if (typeof Float32Array !== "undefined")
                (function() {
                    var f32 = new Float32Array([-0]), f8b = new Uint8Array(f32.buffer), le = f8b[3] === 128;
                    function writeFloat_f32_cpy(val, buf, pos) {
                        f32[0] = val;
                        buf[pos] = f8b[0];
                        buf[pos + 1] = f8b[1];
                        buf[pos + 2] = f8b[2];
                        buf[pos + 3] = f8b[3];
                    }
                    function writeFloat_f32_rev(val, buf, pos) {
                        f32[0] = val;
                        buf[pos] = f8b[3];
                        buf[pos + 1] = f8b[2];
                        buf[pos + 2] = f8b[1];
                        buf[pos + 3] = f8b[0];
                    }
                    exports3.writeFloatLE = le ? writeFloat_f32_cpy : writeFloat_f32_rev;
                    exports3.writeFloatBE = le ? writeFloat_f32_rev : writeFloat_f32_cpy;
                    function readFloat_f32_cpy(buf, pos) {
                        f8b[0] = buf[pos];
                        f8b[1] = buf[pos + 1];
                        f8b[2] = buf[pos + 2];
                        f8b[3] = buf[pos + 3];
                        return f32[0];
                    }
                    function readFloat_f32_rev(buf, pos) {
                        f8b[3] = buf[pos];
                        f8b[2] = buf[pos + 1];
                        f8b[1] = buf[pos + 2];
                        f8b[0] = buf[pos + 3];
                        return f32[0];
                    }
                    exports3.readFloatLE = le ? readFloat_f32_cpy : readFloat_f32_rev;
                    exports3.readFloatBE = le ? readFloat_f32_rev : readFloat_f32_cpy;
                })();
            else
                (function() {
                    function writeFloat_ieee754(writeUint, val, buf, pos) {
                        var sign = val < 0 ? 1 : 0;
                        if (sign)
                            val = -val;
                        if (val === 0)
                            writeUint(1 / val > 0 ? (
                                /* positive */
                                0
                            ) : (
                                /* negative 0 */
                                2147483648
                            ), buf, pos);
                        else if (isNaN(val))
                            writeUint(2143289344, buf, pos);
                        else if (val > 34028234663852886e22)
                            writeUint((sign << 31 | 2139095040) >>> 0, buf, pos);
                        else if (val < 11754943508222875e-54)
                            writeUint((sign << 31 | Math.round(val / 1401298464324817e-60)) >>> 0, buf, pos);
                        else {
                            var exponent = Math.floor(Math.log(val) / Math.LN2), mantissa = Math.round(val * Math.pow(2, -exponent) * 8388608) & 8388607;
                            writeUint((sign << 31 | exponent + 127 << 23 | mantissa) >>> 0, buf, pos);
                        }
                    }
                    exports3.writeFloatLE = writeFloat_ieee754.bind(null, writeUintLE);
                    exports3.writeFloatBE = writeFloat_ieee754.bind(null, writeUintBE);
                    function readFloat_ieee754(readUint, buf, pos) {
                        var uint = readUint(buf, pos), sign = (uint >> 31) * 2 + 1, exponent = uint >>> 23 & 255, mantissa = uint & 8388607;
                        return exponent === 255 ? mantissa ? NaN : sign * Infinity : exponent === 0 ? sign * 1401298464324817e-60 * mantissa : sign * Math.pow(2, exponent - 150) * (mantissa + 8388608);
                    }
                    exports3.readFloatLE = readFloat_ieee754.bind(null, readUintLE);
                    exports3.readFloatBE = readFloat_ieee754.bind(null, readUintBE);
                })();
            if (typeof Float64Array !== "undefined")
                (function() {
                    var f64 = new Float64Array([-0]), f8b = new Uint8Array(f64.buffer), le = f8b[7] === 128;
                    function writeDouble_f64_cpy(val, buf, pos) {
                        f64[0] = val;
                        buf[pos] = f8b[0];
                        buf[pos + 1] = f8b[1];
                        buf[pos + 2] = f8b[2];
                        buf[pos + 3] = f8b[3];
                        buf[pos + 4] = f8b[4];
                        buf[pos + 5] = f8b[5];
                        buf[pos + 6] = f8b[6];
                        buf[pos + 7] = f8b[7];
                    }
                    function writeDouble_f64_rev(val, buf, pos) {
                        f64[0] = val;
                        buf[pos] = f8b[7];
                        buf[pos + 1] = f8b[6];
                        buf[pos + 2] = f8b[5];
                        buf[pos + 3] = f8b[4];
                        buf[pos + 4] = f8b[3];
                        buf[pos + 5] = f8b[2];
                        buf[pos + 6] = f8b[1];
                        buf[pos + 7] = f8b[0];
                    }
                    exports3.writeDoubleLE = le ? writeDouble_f64_cpy : writeDouble_f64_rev;
                    exports3.writeDoubleBE = le ? writeDouble_f64_rev : writeDouble_f64_cpy;
                    function readDouble_f64_cpy(buf, pos) {
                        f8b[0] = buf[pos];
                        f8b[1] = buf[pos + 1];
                        f8b[2] = buf[pos + 2];
                        f8b[3] = buf[pos + 3];
                        f8b[4] = buf[pos + 4];
                        f8b[5] = buf[pos + 5];
                        f8b[6] = buf[pos + 6];
                        f8b[7] = buf[pos + 7];
                        return f64[0];
                    }
                    function readDouble_f64_rev(buf, pos) {
                        f8b[7] = buf[pos];
                        f8b[6] = buf[pos + 1];
                        f8b[5] = buf[pos + 2];
                        f8b[4] = buf[pos + 3];
                        f8b[3] = buf[pos + 4];
                        f8b[2] = buf[pos + 5];
                        f8b[1] = buf[pos + 6];
                        f8b[0] = buf[pos + 7];
                        return f64[0];
                    }
                    exports3.readDoubleLE = le ? readDouble_f64_cpy : readDouble_f64_rev;
                    exports3.readDoubleBE = le ? readDouble_f64_rev : readDouble_f64_cpy;
                })();
            else
                (function() {
                    function writeDouble_ieee754(writeUint, off0, off1, val, buf, pos) {
                        var sign = val < 0 ? 1 : 0;
                        if (sign)
                            val = -val;
                        if (val === 0) {
                            writeUint(0, buf, pos + off0);
                            writeUint(1 / val > 0 ? (
                                /* positive */
                                0
                            ) : (
                                /* negative 0 */
                                2147483648
                            ), buf, pos + off1);
                        } else if (isNaN(val)) {
                            writeUint(0, buf, pos + off0);
                            writeUint(2146959360, buf, pos + off1);
                        } else if (val > 17976931348623157e292) {
                            writeUint(0, buf, pos + off0);
                            writeUint((sign << 31 | 2146435072) >>> 0, buf, pos + off1);
                        } else {
                            var mantissa;
                            if (val < 22250738585072014e-324) {
                                mantissa = val / 5e-324;
                                writeUint(mantissa >>> 0, buf, pos + off0);
                                writeUint((sign << 31 | mantissa / 4294967296) >>> 0, buf, pos + off1);
                            } else {
                                var exponent = Math.floor(Math.log(val) / Math.LN2);
                                if (exponent === 1024)
                                    exponent = 1023;
                                mantissa = val * Math.pow(2, -exponent);
                                writeUint(mantissa * 4503599627370496 >>> 0, buf, pos + off0);
                                writeUint((sign << 31 | exponent + 1023 << 20 | mantissa * 1048576 & 1048575) >>> 0, buf, pos + off1);
                            }
                        }
                    }
                    exports3.writeDoubleLE = writeDouble_ieee754.bind(null, writeUintLE, 0, 4);
                    exports3.writeDoubleBE = writeDouble_ieee754.bind(null, writeUintBE, 4, 0);
                    function readDouble_ieee754(readUint, off0, off1, buf, pos) {
                        var lo = readUint(buf, pos + off0), hi = readUint(buf, pos + off1);
                        var sign = (hi >> 31) * 2 + 1, exponent = hi >>> 20 & 2047, mantissa = 4294967296 * (hi & 1048575) + lo;
                        return exponent === 2047 ? mantissa ? NaN : sign * Infinity : exponent === 0 ? sign * 5e-324 * mantissa : sign * Math.pow(2, exponent - 1075) * (mantissa + 4503599627370496);
                    }
                    exports3.readDoubleLE = readDouble_ieee754.bind(null, readUintLE, 0, 4);
                    exports3.readDoubleBE = readDouble_ieee754.bind(null, readUintBE, 4, 0);
                })();
            return exports3;
        }
        function writeUintLE(val, buf, pos) {
            buf[pos] = val & 255;
            buf[pos + 1] = val >>> 8 & 255;
            buf[pos + 2] = val >>> 16 & 255;
            buf[pos + 3] = val >>> 24;
        }
        function writeUintBE(val, buf, pos) {
            buf[pos] = val >>> 24;
            buf[pos + 1] = val >>> 16 & 255;
            buf[pos + 2] = val >>> 8 & 255;
            buf[pos + 3] = val & 255;
        }
        function readUintLE(buf, pos) {
            return (buf[pos] | buf[pos + 1] << 8 | buf[pos + 2] << 16 | buf[pos + 3] << 24) >>> 0;
        }
        function readUintBE(buf, pos) {
            return (buf[pos] << 24 | buf[pos + 1] << 16 | buf[pos + 2] << 8 | buf[pos + 3]) >>> 0;
        }
    }
});

// node_modules/@protobufjs/inquire/index.js
var require_inquire = __commonJS({
    "node_modules/@protobufjs/inquire/index.js"(exports, module) {
        "use strict";
        module.exports = inquire;
        function inquire(moduleName) {
            try {
                var mod = eval("quire".replace(/^/, "re"))(moduleName);
                if (mod && (mod.length || Object.keys(mod).length))
                    return mod;
            } catch (e) {
            }
            return null;
        }
    }
});

// node_modules/@protobufjs/utf8/index.js
var require_utf8 = __commonJS({
    "node_modules/@protobufjs/utf8/index.js"(exports2) {
        "use strict";
        var utf83 = exports2;
        utf83.length = function utf8_length(string) {
            var len = 0, c = 0;
            for (var i = 0; i < string.length; ++i) {
                c = string.charCodeAt(i);
                if (c < 128)
                    len += 1;
                else if (c < 2048)
                    len += 2;
                else if ((c & 64512) === 55296 && (string.charCodeAt(i + 1) & 64512) === 56320) {
                    ++i;
                    len += 4;
                } else
                    len += 3;
            }
            return len;
        };
        utf83.read = function utf8_read(buffer, start, end) {
            var len = end - start;
            if (len < 1)
                return "";
            var parts = null, chunk = [], i = 0, t;
            while (start < end) {
                t = buffer[start++];
                if (t < 128)
                    chunk[i++] = t;
                else if (t > 191 && t < 224)
                    chunk[i++] = (t & 31) << 6 | buffer[start++] & 63;
                else if (t > 239 && t < 365) {
                    t = ((t & 7) << 18 | (buffer[start++] & 63) << 12 | (buffer[start++] & 63) << 6 | buffer[start++] & 63) - 65536;
                    chunk[i++] = 55296 + (t >> 10);
                    chunk[i++] = 56320 + (t & 1023);
                } else
                    chunk[i++] = (t & 15) << 12 | (buffer[start++] & 63) << 6 | buffer[start++] & 63;
                if (i > 8191) {
                    (parts || (parts = [])).push(String.fromCharCode.apply(String, chunk));
                    i = 0;
                }
            }
            if (parts) {
                if (i)
                    parts.push(String.fromCharCode.apply(String, chunk.slice(0, i)));
                return parts.join("");
            }
            return String.fromCharCode.apply(String, chunk.slice(0, i));
        };
        utf83.write = function utf8_write(string, buffer, offset) {
            var start = offset, c1, c2;
            for (var i = 0; i < string.length; ++i) {
                c1 = string.charCodeAt(i);
                if (c1 < 128) {
                    buffer[offset++] = c1;
                } else if (c1 < 2048) {
                    buffer[offset++] = c1 >> 6 | 192;
                    buffer[offset++] = c1 & 63 | 128;
                } else if ((c1 & 64512) === 55296 && ((c2 = string.charCodeAt(i + 1)) & 64512) === 56320) {
                    c1 = 65536 + ((c1 & 1023) << 10) + (c2 & 1023);
                    ++i;
                    buffer[offset++] = c1 >> 18 | 240;
                    buffer[offset++] = c1 >> 12 & 63 | 128;
                    buffer[offset++] = c1 >> 6 & 63 | 128;
                    buffer[offset++] = c1 & 63 | 128;
                } else {
                    buffer[offset++] = c1 >> 12 | 224;
                    buffer[offset++] = c1 >> 6 & 63 | 128;
                    buffer[offset++] = c1 & 63 | 128;
                }
            }
            return offset - start;
        };
    }
});

// node_modules/@protobufjs/pool/index.js
var require_pool = __commonJS({
    "node_modules/@protobufjs/pool/index.js"(exports2, module2) {
        "use strict";
        module2.exports = pool;
        function pool(alloc, slice, size) {
            var SIZE = size || 8192;
            var MAX = SIZE >>> 1;
            var slab = null;
            var offset = SIZE;
            return function pool_alloc(size2) {
                if (size2 < 1 || size2 > MAX)
                    return alloc(size2);
                if (offset + size2 > SIZE) {
                    slab = alloc(SIZE);
                    offset = 0;
                }
                var buf = slice.call(slab, offset, offset += size2);
                if (offset & 7)
                    offset = (offset | 7) + 1;
                return buf;
            };
        }
    }
});

// node_modules/protobufjs/src/util/longbits.js
var require_longbits = __commonJS({
    "node_modules/protobufjs/src/util/longbits.js"(exports2, module2) {
        "use strict";
        module2.exports = LongBits;
        var util = require_minimal();
        function LongBits(lo, hi) {
            this.lo = lo >>> 0;
            this.hi = hi >>> 0;
        }
        var zero = LongBits.zero = new LongBits(0, 0);
        zero.toNumber = function() {
            return 0;
        };
        zero.zzEncode = zero.zzDecode = function() {
            return this;
        };
        zero.length = function() {
            return 1;
        };
        var zeroHash = LongBits.zeroHash = "\0\0\0\0\0\0\0\0";
        LongBits.fromNumber = function fromNumber(value) {
            if (value === 0)
                return zero;
            var sign = value < 0;
            if (sign)
                value = -value;
            var lo = value >>> 0, hi = (value - lo) / 4294967296 >>> 0;
            if (sign) {
                hi = ~hi >>> 0;
                lo = ~lo >>> 0;
                if (++lo > 4294967295) {
                    lo = 0;
                    if (++hi > 4294967295)
                        hi = 0;
                }
            }
            return new LongBits(lo, hi);
        };
        LongBits.from = function from(value) {
            if (typeof value === "number")
                return LongBits.fromNumber(value);
            if (util.isString(value)) {
                if (util.Long)
                    value = util.Long.fromString(value);
                else
                    return LongBits.fromNumber(parseInt(value, 10));
            }
            return value.low || value.high ? new LongBits(value.low >>> 0, value.high >>> 0) : zero;
        };
        LongBits.prototype.toNumber = function toNumber(unsigned) {
            if (!unsigned && this.hi >>> 31) {
                var lo = ~this.lo + 1 >>> 0, hi = ~this.hi >>> 0;
                if (!lo)
                    hi = hi + 1 >>> 0;
                return -(lo + hi * 4294967296);
            }
            return this.lo + this.hi * 4294967296;
        };
        LongBits.prototype.toLong = function toLong(unsigned) {
            return util.Long ? new util.Long(this.lo | 0, this.hi | 0, Boolean(unsigned)) : { low: this.lo | 0, high: this.hi | 0, unsigned: Boolean(unsigned) };
        };
        var charCodeAt = String.prototype.charCodeAt;
        LongBits.fromHash = function fromHash(hash) {
            if (hash === zeroHash)
                return zero;
            return new LongBits(
                (charCodeAt.call(hash, 0) | charCodeAt.call(hash, 1) << 8 | charCodeAt.call(hash, 2) << 16 | charCodeAt.call(hash, 3) << 24) >>> 0,
                (charCodeAt.call(hash, 4) | charCodeAt.call(hash, 5) << 8 | charCodeAt.call(hash, 6) << 16 | charCodeAt.call(hash, 7) << 24) >>> 0
            );
        };
        LongBits.prototype.toHash = function toHash() {
            return String.fromCharCode(
                this.lo & 255,
                this.lo >>> 8 & 255,
                this.lo >>> 16 & 255,
                this.lo >>> 24,
                this.hi & 255,
                this.hi >>> 8 & 255,
                this.hi >>> 16 & 255,
                this.hi >>> 24
            );
        };
        LongBits.prototype.zzEncode = function zzEncode() {
            var mask = this.hi >> 31;
            this.hi = ((this.hi << 1 | this.lo >>> 31) ^ mask) >>> 0;
            this.lo = (this.lo << 1 ^ mask) >>> 0;
            return this;
        };
        LongBits.prototype.zzDecode = function zzDecode() {
            var mask = -(this.lo & 1);
            this.lo = ((this.lo >>> 1 | this.hi << 31) ^ mask) >>> 0;
            this.hi = (this.hi >>> 1 ^ mask) >>> 0;
            return this;
        };
        LongBits.prototype.length = function length2() {
            var part0 = this.lo, part1 = (this.lo >>> 28 | this.hi << 4) >>> 0, part2 = this.hi >>> 24;
            return part2 === 0 ? part1 === 0 ? part0 < 16384 ? part0 < 128 ? 1 : 2 : part0 < 2097152 ? 3 : 4 : part1 < 16384 ? part1 < 128 ? 5 : 6 : part1 < 2097152 ? 7 : 8 : part2 < 128 ? 9 : 10;
        };
    }
});

// node_modules/protobufjs/src/util/minimal.js
var require_minimal = __commonJS({
    "node_modules/protobufjs/src/util/minimal.js"(exports2) {
        "use strict";
        var util = exports2;
        util.asPromise = require_aspromise();
        util.base64 = require_base64();
        util.EventEmitter = require_eventemitter();
        util.float = require_float();
        util.inquire = require_inquire();
        util.utf8 = require_utf8();
        util.pool = require_pool();
        util.LongBits = require_longbits();
        util.isNode = Boolean(typeof global !== "undefined" && global && global.process && global.process.versions && global.process.versions.node);
        util.global = util.isNode && global || false || typeof self !== "undefined" && self || exports2;
        util.emptyArray = Object.freeze ? Object.freeze([]) : (
            /* istanbul ignore next */
            []
        );
        util.emptyObject = Object.freeze ? Object.freeze({}) : (
            /* istanbul ignore next */
            {}
        );
        util.isInteger = Number.isInteger || /* istanbul ignore next */
            function isInteger(value) {
                return typeof value === "number" && isFinite(value) && Math.floor(value) === value;
            };
        util.isString = function isString(value) {
            return typeof value === "string" || value instanceof String;
        };
        util.isObject = function isObject3(value) {
            return value && typeof value === "object";
        };
        util.isset = /**
         * Checks if a property on a message is considered to be present.
         * @param {Object} obj Plain object or message instance
         * @param {string} prop Property name
         * @returns {boolean} `true` if considered to be present, otherwise `false`
         */
            util.isSet = function isSet3(obj, prop) {
                var value = obj[prop];
                if (value != null && obj.hasOwnProperty(prop))
                    return typeof value !== "object" || (Array.isArray(value) ? value.length : Object.keys(value).length) > 0;
                return false;
            };
        util.Buffer = function() {
            try {
                var Buffer2 = util.inquire("buffer").Buffer;
                return Buffer2.prototype.utf8Write ? Buffer2 : (
                    /* istanbul ignore next */
                    null
                );
            } catch (e) {
                return null;
            }
        }();
        util._Buffer_from = null;
        util._Buffer_allocUnsafe = null;
        util.newBuffer = function newBuffer(sizeOrArray) {
            return typeof sizeOrArray === "number" ? util.Buffer ? util._Buffer_allocUnsafe(sizeOrArray) : new util.Array(sizeOrArray) : util.Buffer ? util._Buffer_from(sizeOrArray) : typeof Uint8Array === "undefined" ? sizeOrArray : new Uint8Array(sizeOrArray);
        };
        util.Array = typeof Uint8Array !== "undefined" ? Uint8Array : Array;
        util.Long = /* istanbul ignore next */
            util.global.dcodeIO && /* istanbul ignore next */
            util.global.dcodeIO.Long || /* istanbul ignore next */
            util.global.Long || util.inquire("long");
        util.key2Re = /^true|false|0|1$/;
        util.key32Re = /^-?(?:0|[1-9][0-9]*)$/;
        util.key64Re = /^(?:[\\x00-\\xff]{8}|-?(?:0|[1-9][0-9]*))$/;
        util.longToHash = function longToHash(value) {
            return value ? util.LongBits.from(value).toHash() : util.LongBits.zeroHash;
        };
        util.longFromHash = function longFromHash(hash, unsigned) {
            var bits = util.LongBits.fromHash(hash);
            if (util.Long)
                return util.Long.fromBits(bits.lo, bits.hi, unsigned);
            return bits.toNumber(Boolean(unsigned));
        };
        function merge(dst, src, ifNotSet) {
            for (var keys = Object.keys(src), i = 0; i < keys.length; ++i)
                if (dst[keys[i]] === void 0 || !ifNotSet)
                    dst[keys[i]] = src[keys[i]];
            return dst;
        }
        util.merge = merge;
        util.lcFirst = function lcFirst(str) {
            return str.charAt(0).toLowerCase() + str.substring(1);
        };
        function newError(name) {
            function CustomError(message, properties) {
                if (!(this instanceof CustomError))
                    return new CustomError(message, properties);
                Object.defineProperty(this, "message", { get: function() {
                        return message;
                    } });
                if (Error.captureStackTrace)
                    Error.captureStackTrace(this, CustomError);
                else
                    Object.defineProperty(this, "stack", { value: new Error().stack || "" });
                if (properties)
                    merge(this, properties);
            }
            (CustomError.prototype = Object.create(Error.prototype)).constructor = CustomError;
            Object.defineProperty(CustomError.prototype, "name", { get: function() {
                    return name;
                } });
            CustomError.prototype.toString = function toString() {
                return this.name + ": " + this.message;
            };
            return CustomError;
        }
        util.newError = newError;
        util.ProtocolError = newError("ProtocolError");
        util.oneOfGetter = function getOneOf(fieldNames) {
            var fieldMap = {};
            for (var i = 0; i < fieldNames.length; ++i)
                fieldMap[fieldNames[i]] = 1;
            return function() {
                for (var keys = Object.keys(this), i2 = keys.length - 1; i2 > -1; --i2)
                    if (fieldMap[keys[i2]] === 1 && this[keys[i2]] !== void 0 && this[keys[i2]] !== null)
                        return keys[i2];
            };
        };
        util.oneOfSetter = function setOneOf(fieldNames) {
            return function(name) {
                for (var i = 0; i < fieldNames.length; ++i)
                    if (fieldNames[i] !== name)
                        delete this[fieldNames[i]];
            };
        };
        util.toJSONOptions = {
            longs: String,
            enums: String,
            bytes: String,
            json: true
        };
        util._configure = function() {
            var Buffer2 = util.Buffer;
            if (!Buffer2) {
                util._Buffer_from = util._Buffer_allocUnsafe = null;
                return;
            }
            util._Buffer_from = Buffer2.from !== Uint8Array.from && Buffer2.from || /* istanbul ignore next */
                function Buffer_from(value, encoding) {
                    return new Buffer2(value, encoding);
                };
            util._Buffer_allocUnsafe = Buffer2.allocUnsafe || /* istanbul ignore next */
                function Buffer_allocUnsafe(size) {
                    return new Buffer2(size);
                };
        };
    }
});

// node_modules/protobufjs/src/writer.js
var require_writer = __commonJS({
    "node_modules/protobufjs/src/writer.js"(exports2, module2) {
        "use strict";
        module2.exports = Writer;
        var util = require_minimal();
        var BufferWriter;
        var LongBits = util.LongBits;
        var base64 = util.base64;
        var utf83 = util.utf8;
        function Op(fn, len, val) {
            this.fn = fn;
            this.len = len;
            this.next = void 0;
            this.val = val;
        }
        function noop() {
        }
        function State(writer) {
            this.head = writer.head;
            this.tail = writer.tail;
            this.len = writer.len;
            this.next = writer.states;
        }
        function Writer() {
            this.len = 0;
            this.head = new Op(noop, 0, 0);
            this.tail = this.head;
            this.states = null;
        }
        var create = function create2() {
            return util.Buffer ? function create_buffer_setup() {
                return (Writer.create = function create_buffer() {
                    return new BufferWriter();
                })();
            } : function create_array() {
                return new Writer();
            };
        };
        Writer.create = create();
        Writer.alloc = function alloc(size) {
            return new util.Array(size);
        };
        if (util.Array !== Array)
            Writer.alloc = util.pool(Writer.alloc, util.Array.prototype.subarray);
        Writer.prototype._push = function push(fn, len, val) {
            this.tail = this.tail.next = new Op(fn, len, val);
            this.len += len;
            return this;
        };
        function writeByte(val, buf, pos) {
            buf[pos] = val & 255;
        }
        function writeVarint32(val, buf, pos) {
            while (val > 127) {
                buf[pos++] = val & 127 | 128;
                val >>>= 7;
            }
            buf[pos] = val;
        }
        function VarintOp(len, val) {
            this.len = len;
            this.next = void 0;
            this.val = val;
        }
        VarintOp.prototype = Object.create(Op.prototype);
        VarintOp.prototype.fn = writeVarint32;
        Writer.prototype.uint32 = function write_uint32(value) {
            this.len += (this.tail = this.tail.next = new VarintOp(
                (value = value >>> 0) < 128 ? 1 : value < 16384 ? 2 : value < 2097152 ? 3 : value < 268435456 ? 4 : 5,
                value
            )).len;
            return this;
        };
        Writer.prototype.int32 = function write_int32(value) {
            return value < 0 ? this._push(writeVarint64, 10, LongBits.fromNumber(value)) : this.uint32(value);
        };
        Writer.prototype.sint32 = function write_sint32(value) {
            return this.uint32((value << 1 ^ value >> 31) >>> 0);
        };
        function writeVarint64(val, buf, pos) {
            while (val.hi) {
                buf[pos++] = val.lo & 127 | 128;
                val.lo = (val.lo >>> 7 | val.hi << 25) >>> 0;
                val.hi >>>= 7;
            }
            while (val.lo > 127) {
                buf[pos++] = val.lo & 127 | 128;
                val.lo = val.lo >>> 7;
            }
            buf[pos++] = val.lo;
        }
        Writer.prototype.uint64 = function write_uint64(value) {
            var bits = LongBits.from(value);
            return this._push(writeVarint64, bits.length(), bits);
        };
        Writer.prototype.int64 = Writer.prototype.uint64;
        Writer.prototype.sint64 = function write_sint64(value) {
            var bits = LongBits.from(value).zzEncode();
            return this._push(writeVarint64, bits.length(), bits);
        };
        Writer.prototype.bool = function write_bool(value) {
            return this._push(writeByte, 1, value ? 1 : 0);
        };
        function writeFixed32(val, buf, pos) {
            buf[pos] = val & 255;
            buf[pos + 1] = val >>> 8 & 255;
            buf[pos + 2] = val >>> 16 & 255;
            buf[pos + 3] = val >>> 24;
        }
        Writer.prototype.fixed32 = function write_fixed32(value) {
            return this._push(writeFixed32, 4, value >>> 0);
        };
        Writer.prototype.sfixed32 = Writer.prototype.fixed32;
        Writer.prototype.fixed64 = function write_fixed64(value) {
            var bits = LongBits.from(value);
            return this._push(writeFixed32, 4, bits.lo)._push(writeFixed32, 4, bits.hi);
        };
        Writer.prototype.sfixed64 = Writer.prototype.fixed64;
        Writer.prototype.float = function write_float(value) {
            return this._push(util.float.writeFloatLE, 4, value);
        };
        Writer.prototype.double = function write_double(value) {
            return this._push(util.float.writeDoubleLE, 8, value);
        };
        var writeBytes = util.Array.prototype.set ? function writeBytes_set(val, buf, pos) {
            buf.set(val, pos);
        } : function writeBytes_for(val, buf, pos) {
            for (var i = 0; i < val.length; ++i)
                buf[pos + i] = val[i];
        };
        Writer.prototype.bytes = function write_bytes(value) {
            var len = value.length >>> 0;
            if (!len)
                return this._push(writeByte, 1, 0);
            if (util.isString(value)) {
                var buf = Writer.alloc(len = base64.length(value));
                base64.decode(value, buf, 0);
                value = buf;
            }
            return this.uint32(len)._push(writeBytes, len, value);
        };
        Writer.prototype.string = function write_string(value) {
            var len = utf83.length(value);
            return len ? this.uint32(len)._push(utf83.write, len, value) : this._push(writeByte, 1, 0);
        };
        Writer.prototype.fork = function fork() {
            this.states = new State(this);
            this.head = this.tail = new Op(noop, 0, 0);
            this.len = 0;
            return this;
        };
        Writer.prototype.reset = function reset() {
            if (this.states) {
                this.head = this.states.head;
                this.tail = this.states.tail;
                this.len = this.states.len;
                this.states = this.states.next;
            } else {
                this.head = this.tail = new Op(noop, 0, 0);
                this.len = 0;
            }
            return this;
        };
        Writer.prototype.ldelim = function ldelim() {
            var head = this.head, tail = this.tail, len = this.len;
            this.reset().uint32(len);
            if (len) {
                this.tail.next = head.next;
                this.tail = tail;
                this.len += len;
            }
            return this;
        };
        Writer.prototype.finish = function finish() {
            var head = this.head.next, buf = this.constructor.alloc(this.len), pos = 0;
            while (head) {
                head.fn(head.val, buf, pos);
                pos += head.len;
                head = head.next;
            }
            return buf;
        };
        Writer._configure = function(BufferWriter_) {
            BufferWriter = BufferWriter_;
            Writer.create = create();
            BufferWriter._configure();
        };
    }
});

// node_modules/protobufjs/src/writer_buffer.js
var require_writer_buffer = __commonJS({
    "node_modules/protobufjs/src/writer_buffer.js"(exports2, module2) {
        "use strict";
        module2.exports = BufferWriter;
        var Writer = require_writer();
        (BufferWriter.prototype = Object.create(Writer.prototype)).constructor = BufferWriter;
        var util = require_minimal();
        function BufferWriter() {
            Writer.call(this);
        }
        BufferWriter._configure = function() {
            BufferWriter.alloc = util._Buffer_allocUnsafe;
            BufferWriter.writeBytesBuffer = util.Buffer && util.Buffer.prototype instanceof Uint8Array && util.Buffer.prototype.set.name === "set" ? function writeBytesBuffer_set(val, buf, pos) {
                buf.set(val, pos);
            } : function writeBytesBuffer_copy(val, buf, pos) {
                if (val.copy)
                    val.copy(buf, pos, 0, val.length);
                else
                    for (var i = 0; i < val.length; )
                        buf[pos++] = val[i++];
            };
        };
        BufferWriter.prototype.bytes = function write_bytes_buffer(value) {
            if (util.isString(value))
                value = util._Buffer_from(value, "base64");
            var len = value.length >>> 0;
            this.uint32(len);
            if (len)
                this._push(BufferWriter.writeBytesBuffer, len, value);
            return this;
        };
        function writeStringBuffer(val, buf, pos) {
            if (val.length < 40)
                util.utf8.write(val, buf, pos);
            else if (buf.utf8Write)
                buf.utf8Write(val, pos);
            else
                buf.write(val, pos);
        }
        BufferWriter.prototype.string = function write_string_buffer(value) {
            var len = util.Buffer.byteLength(value);
            this.uint32(len);
            if (len)
                this._push(writeStringBuffer, len, value);
            return this;
        };
        BufferWriter._configure();
    }
});

// node_modules/protobufjs/src/reader.js
var require_reader = __commonJS({
    "node_modules/protobufjs/src/reader.js"(exports2, module2) {
        "use strict";
        module2.exports = Reader;
        var util = require_minimal();
        var BufferReader;
        var LongBits = util.LongBits;
        var utf83 = util.utf8;
        function indexOutOfRange(reader, writeLength) {
            return RangeError("index out of range: " + reader.pos + " + " + (writeLength || 1) + " > " + reader.len);
        }
        function Reader(buffer) {
            this.buf = buffer;
            this.pos = 0;
            this.len = buffer.length;
        }
        var create_array = typeof Uint8Array !== "undefined" ? function create_typed_array(buffer) {
            if (buffer instanceof Uint8Array || Array.isArray(buffer))
                return new Reader(buffer);
            throw Error("illegal buffer");
        } : function create_array2(buffer) {
            if (Array.isArray(buffer))
                return new Reader(buffer);
            throw Error("illegal buffer");
        };
        var create = function create2() {
            return util.Buffer ? function create_buffer_setup(buffer) {
                return (Reader.create = function create_buffer(buffer2) {
                    return util.Buffer.isBuffer(buffer2) ? new BufferReader(buffer2) : create_array(buffer2);
                })(buffer);
            } : create_array;
        };
        Reader.create = create();
        Reader.prototype._slice = util.Array.prototype.subarray || /* istanbul ignore next */
            util.Array.prototype.slice;
        Reader.prototype.uint32 = function read_uint32_setup() {
            var value = 4294967295;
            return function read_uint32() {
                value = (this.buf[this.pos] & 127) >>> 0;
                if (this.buf[this.pos++] < 128)
                    return value;
                value = (value | (this.buf[this.pos] & 127) << 7) >>> 0;
                if (this.buf[this.pos++] < 128)
                    return value;
                value = (value | (this.buf[this.pos] & 127) << 14) >>> 0;
                if (this.buf[this.pos++] < 128)
                    return value;
                value = (value | (this.buf[this.pos] & 127) << 21) >>> 0;
                if (this.buf[this.pos++] < 128)
                    return value;
                value = (value | (this.buf[this.pos] & 15) << 28) >>> 0;
                if (this.buf[this.pos++] < 128)
                    return value;
                if ((this.pos += 5) > this.len) {
                    this.pos = this.len;
                    throw indexOutOfRange(this, 10);
                }
                return value;
            };
        }();
        Reader.prototype.int32 = function read_int32() {
            return this.uint32() | 0;
        };
        Reader.prototype.sint32 = function read_sint32() {
            var value = this.uint32();
            return value >>> 1 ^ -(value & 1) | 0;
        };
        function readLongVarint() {
            var bits = new LongBits(0, 0);
            var i = 0;
            if (this.len - this.pos > 4) {
                for (; i < 4; ++i) {
                    bits.lo = (bits.lo | (this.buf[this.pos] & 127) << i * 7) >>> 0;
                    if (this.buf[this.pos++] < 128)
                        return bits;
                }
                bits.lo = (bits.lo | (this.buf[this.pos] & 127) << 28) >>> 0;
                bits.hi = (bits.hi | (this.buf[this.pos] & 127) >> 4) >>> 0;
                if (this.buf[this.pos++] < 128)
                    return bits;
                i = 0;
            } else {
                for (; i < 3; ++i) {
                    if (this.pos >= this.len)
                        throw indexOutOfRange(this);
                    bits.lo = (bits.lo | (this.buf[this.pos] & 127) << i * 7) >>> 0;
                    if (this.buf[this.pos++] < 128)
                        return bits;
                }
                bits.lo = (bits.lo | (this.buf[this.pos++] & 127) << i * 7) >>> 0;
                return bits;
            }
            if (this.len - this.pos > 4) {
                for (; i < 5; ++i) {
                    bits.hi = (bits.hi | (this.buf[this.pos] & 127) << i * 7 + 3) >>> 0;
                    if (this.buf[this.pos++] < 128)
                        return bits;
                }
            } else {
                for (; i < 5; ++i) {
                    if (this.pos >= this.len)
                        throw indexOutOfRange(this);
                    bits.hi = (bits.hi | (this.buf[this.pos] & 127) << i * 7 + 3) >>> 0;
                    if (this.buf[this.pos++] < 128)
                        return bits;
                }
            }
            throw Error("invalid varint encoding");
        }
        Reader.prototype.bool = function read_bool() {
            return this.uint32() !== 0;
        };
        function readFixed32_end(buf, end) {
            return (buf[end - 4] | buf[end - 3] << 8 | buf[end - 2] << 16 | buf[end - 1] << 24) >>> 0;
        }
        Reader.prototype.fixed32 = function read_fixed32() {
            if (this.pos + 4 > this.len)
                throw indexOutOfRange(this, 4);
            return readFixed32_end(this.buf, this.pos += 4);
        };
        Reader.prototype.sfixed32 = function read_sfixed32() {
            if (this.pos + 4 > this.len)
                throw indexOutOfRange(this, 4);
            return readFixed32_end(this.buf, this.pos += 4) | 0;
        };
        function readFixed64() {
            if (this.pos + 8 > this.len)
                throw indexOutOfRange(this, 8);
            return new LongBits(readFixed32_end(this.buf, this.pos += 4), readFixed32_end(this.buf, this.pos += 4));
        }
        Reader.prototype.float = function read_float() {
            if (this.pos + 4 > this.len)
                throw indexOutOfRange(this, 4);
            var value = util.float.readFloatLE(this.buf, this.pos);
            this.pos += 4;
            return value;
        };
        Reader.prototype.double = function read_double() {
            if (this.pos + 8 > this.len)
                throw indexOutOfRange(this, 4);
            var value = util.float.readDoubleLE(this.buf, this.pos);
            this.pos += 8;
            return value;
        };
        Reader.prototype.bytes = function read_bytes() {
            var length2 = this.uint32(), start = this.pos, end = this.pos + length2;
            if (end > this.len)
                throw indexOutOfRange(this, length2);
            this.pos += length2;
            if (Array.isArray(this.buf))
                return this.buf.slice(start, end);
            return start === end ? new this.buf.constructor(0) : this._slice.call(this.buf, start, end);
        };
        Reader.prototype.string = function read_string() {
            var bytes = this.bytes();
            return utf83.read(bytes, 0, bytes.length);
        };
        Reader.prototype.skip = function skip(length2) {
            if (typeof length2 === "number") {
                if (this.pos + length2 > this.len)
                    throw indexOutOfRange(this, length2);
                this.pos += length2;
            } else {
                do {
                    if (this.pos >= this.len)
                        throw indexOutOfRange(this);
                } while (this.buf[this.pos++] & 128);
            }
            return this;
        };
        Reader.prototype.skipType = function(wireType) {
            switch (wireType) {
                case 0:
                    this.skip();
                    break;
                case 1:
                    this.skip(8);
                    break;
                case 2:
                    this.skip(this.uint32());
                    break;
                case 3:
                    while ((wireType = this.uint32() & 7) !== 4) {
                        this.skipType(wireType);
                    }
                    break;
                case 5:
                    this.skip(4);
                    break;
                default:
                    throw Error("invalid wire type " + wireType + " at offset " + this.pos);
            }
            return this;
        };
        Reader._configure = function(BufferReader_) {
            BufferReader = BufferReader_;
            Reader.create = create();
            BufferReader._configure();
            var fn = util.Long ? "toLong" : (
                /* istanbul ignore next */
                "toNumber"
            );
            util.merge(Reader.prototype, {
                int64: function read_int64() {
                    return readLongVarint.call(this)[fn](false);
                },
                uint64: function read_uint64() {
                    return readLongVarint.call(this)[fn](true);
                },
                sint64: function read_sint64() {
                    return readLongVarint.call(this).zzDecode()[fn](false);
                },
                fixed64: function read_fixed64() {
                    return readFixed64.call(this)[fn](true);
                },
                sfixed64: function read_sfixed64() {
                    return readFixed64.call(this)[fn](false);
                }
            });
        };
    }
});

// node_modules/protobufjs/src/reader_buffer.js
var require_reader_buffer = __commonJS({
    "node_modules/protobufjs/src/reader_buffer.js"(exports2, module2) {
        "use strict";
        module2.exports = BufferReader;
        var Reader = require_reader();
        (BufferReader.prototype = Object.create(Reader.prototype)).constructor = BufferReader;
        var util = require_minimal();
        function BufferReader(buffer) {
            Reader.call(this, buffer);
        }
        BufferReader._configure = function() {
            if (util.Buffer)
                BufferReader.prototype._slice = util.Buffer.prototype.slice;
        };
        BufferReader.prototype.string = function read_string_buffer() {
            var len = this.uint32();
            return this.buf.utf8Slice ? this.buf.utf8Slice(this.pos, this.pos = Math.min(this.pos + len, this.len)) : this.buf.toString("utf-8", this.pos, this.pos = Math.min(this.pos + len, this.len));
        };
        BufferReader._configure();
    }
});

// node_modules/protobufjs/src/rpc/service.js
var require_service = __commonJS({
    "node_modules/protobufjs/src/rpc/service.js"(exports2, module2) {
        "use strict";
        module2.exports = Service;
        var util = require_minimal();
        (Service.prototype = Object.create(util.EventEmitter.prototype)).constructor = Service;
        function Service(rpcImpl, requestDelimited, responseDelimited) {
            if (typeof rpcImpl !== "function")
                throw TypeError("rpcImpl must be a function");
            util.EventEmitter.call(this);
            this.rpcImpl = rpcImpl;
            this.requestDelimited = Boolean(requestDelimited);
            this.responseDelimited = Boolean(responseDelimited);
        }
        Service.prototype.rpcCall = function rpcCall(method, requestCtor, responseCtor, request, callback) {
            if (!request)
                throw TypeError("request must be specified");
            var self2 = this;
            if (!callback)
                return util.asPromise(rpcCall, self2, method, requestCtor, responseCtor, request);
            if (!self2.rpcImpl) {
                setTimeout(function() {
                    callback(Error("already ended"));
                }, 0);
                return void 0;
            }
            try {
                return self2.rpcImpl(
                    method,
                    requestCtor[self2.requestDelimited ? "encodeDelimited" : "encode"](request).finish(),
                    function rpcCallback(err, response) {
                        if (err) {
                            self2.emit("error", err, method);
                            return callback(err);
                        }
                        if (response === null) {
                            self2.end(
                                /* endedByRPC */
                                true
                            );
                            return void 0;
                        }
                        if (!(response instanceof responseCtor)) {
                            try {
                                response = responseCtor[self2.responseDelimited ? "decodeDelimited" : "decode"](response);
                            } catch (err2) {
                                self2.emit("error", err2, method);
                                return callback(err2);
                            }
                        }
                        self2.emit("data", response, method);
                        return callback(null, response);
                    }
                );
            } catch (err) {
                self2.emit("error", err, method);
                setTimeout(function() {
                    callback(err);
                }, 0);
                return void 0;
            }
        };
        Service.prototype.end = function end(endedByRPC) {
            if (this.rpcImpl) {
                if (!endedByRPC)
                    this.rpcImpl(null, null, null);
                this.rpcImpl = null;
                this.emit("end").off();
            }
            return this;
        };
    }
});

// node_modules/protobufjs/src/rpc.js
var require_rpc = __commonJS({
    "node_modules/protobufjs/src/rpc.js"(exports2) {
        "use strict";
        var rpc = exports2;
        rpc.Service = require_service();
    }
});

// node_modules/protobufjs/src/roots.js
var require_roots = __commonJS({
    "node_modules/protobufjs/src/roots.js"(exports2, module2) {
        "use strict";
        module2.exports = {};
    }
});

// node_modules/protobufjs/src/index-minimal.js
var require_index_minimal = __commonJS({
    "node_modules/protobufjs/src/index-minimal.js"(exports2) {
        "use strict";
        var protobuf = exports2;
        protobuf.build = "minimal";
        protobuf.Writer = require_writer();
        protobuf.BufferWriter = require_writer_buffer();
        protobuf.Reader = require_reader();
        protobuf.BufferReader = require_reader_buffer();
        protobuf.util = require_minimal();
        protobuf.rpc = require_rpc();
        protobuf.roots = require_roots();
        protobuf.configure = configure;
        function configure() {
            protobuf.util._configure();
            protobuf.Writer._configure(protobuf.BufferWriter);
            protobuf.Reader._configure(protobuf.BufferReader);
        }
        configure();
    }
});

// node_modules/protobufjs/minimal.js
var require_minimal2 = __commonJS({
    "node_modules/protobufjs/minimal.js"(exports2, module2) {
        "use strict";
        module2.exports = require_index_minimal();
    }
});

// node_modules/long/src/long.js
var require_long = __commonJS({
    "node_modules/long/src/long.js"(exports2, module2) {
        module2.exports = Long2;
        var wasm = null;
        try {
            wasm = new WebAssembly.Instance(new WebAssembly.Module(new Uint8Array([
                0,
                97,
                115,
                109,
                1,
                0,
                0,
                0,
                1,
                13,
                2,
                96,
                0,
                1,
                127,
                96,
                4,
                127,
                127,
                127,
                127,
                1,
                127,
                3,
                7,
                6,
                0,
                1,
                1,
                1,
                1,
                1,
                6,
                6,
                1,
                127,
                1,
                65,
                0,
                11,
                7,
                50,
                6,
                3,
                109,
                117,
                108,
                0,
                1,
                5,
                100,
                105,
                118,
                95,
                115,
                0,
                2,
                5,
                100,
                105,
                118,
                95,
                117,
                0,
                3,
                5,
                114,
                101,
                109,
                95,
                115,
                0,
                4,
                5,
                114,
                101,
                109,
                95,
                117,
                0,
                5,
                8,
                103,
                101,
                116,
                95,
                104,
                105,
                103,
                104,
                0,
                0,
                10,
                191,
                1,
                6,
                4,
                0,
                35,
                0,
                11,
                36,
                1,
                1,
                126,
                32,
                0,
                173,
                32,
                1,
                173,
                66,
                32,
                134,
                132,
                32,
                2,
                173,
                32,
                3,
                173,
                66,
                32,
                134,
                132,
                126,
                34,
                4,
                66,
                32,
                135,
                167,
                36,
                0,
                32,
                4,
                167,
                11,
                36,
                1,
                1,
                126,
                32,
                0,
                173,
                32,
                1,
                173,
                66,
                32,
                134,
                132,
                32,
                2,
                173,
                32,
                3,
                173,
                66,
                32,
                134,
                132,
                127,
                34,
                4,
                66,
                32,
                135,
                167,
                36,
                0,
                32,
                4,
                167,
                11,
                36,
                1,
                1,
                126,
                32,
                0,
                173,
                32,
                1,
                173,
                66,
                32,
                134,
                132,
                32,
                2,
                173,
                32,
                3,
                173,
                66,
                32,
                134,
                132,
                128,
                34,
                4,
                66,
                32,
                135,
                167,
                36,
                0,
                32,
                4,
                167,
                11,
                36,
                1,
                1,
                126,
                32,
                0,
                173,
                32,
                1,
                173,
                66,
                32,
                134,
                132,
                32,
                2,
                173,
                32,
                3,
                173,
                66,
                32,
                134,
                132,
                129,
                34,
                4,
                66,
                32,
                135,
                167,
                36,
                0,
                32,
                4,
                167,
                11,
                36,
                1,
                1,
                126,
                32,
                0,
                173,
                32,
                1,
                173,
                66,
                32,
                134,
                132,
                32,
                2,
                173,
                32,
                3,
                173,
                66,
                32,
                134,
                132,
                130,
                34,
                4,
                66,
                32,
                135,
                167,
                36,
                0,
                32,
                4,
                167,
                11
            ])), {}).exports;
        } catch (e) {
        }
        function Long2(low, high, unsigned) {
            this.low = low | 0;
            this.high = high | 0;
            this.unsigned = !!unsigned;
        }
        Long2.prototype.__isLong__;
        Object.defineProperty(Long2.prototype, "__isLong__", { value: true });
        function isLong(obj) {
            return (obj && obj["__isLong__"]) === true;
        }
        Long2.isLong = isLong;
        var INT_CACHE = {};
        var UINT_CACHE = {};
        function fromInt(value, unsigned) {
            var obj, cachedObj, cache;
            if (unsigned) {
                value >>>= 0;
                if (cache = 0 <= value && value < 256) {
                    cachedObj = UINT_CACHE[value];
                    if (cachedObj)
                        return cachedObj;
                }
                obj = fromBits(value, (value | 0) < 0 ? -1 : 0, true);
                if (cache)
                    UINT_CACHE[value] = obj;
                return obj;
            } else {
                value |= 0;
                if (cache = -128 <= value && value < 128) {
                    cachedObj = INT_CACHE[value];
                    if (cachedObj)
                        return cachedObj;
                }
                obj = fromBits(value, value < 0 ? -1 : 0, false);
                if (cache)
                    INT_CACHE[value] = obj;
                return obj;
            }
        }
        Long2.fromInt = fromInt;
        function fromNumber(value, unsigned) {
            if (isNaN(value))
                return unsigned ? UZERO : ZERO;
            if (unsigned) {
                if (value < 0)
                    return UZERO;
                if (value >= TWO_PWR_64_DBL)
                    return MAX_UNSIGNED_VALUE;
            } else {
                if (value <= -TWO_PWR_63_DBL)
                    return MIN_VALUE;
                if (value + 1 >= TWO_PWR_63_DBL)
                    return MAX_VALUE;
            }
            if (value < 0)
                return fromNumber(-value, unsigned).neg();
            return fromBits(value % TWO_PWR_32_DBL | 0, value / TWO_PWR_32_DBL | 0, unsigned);
        }
        Long2.fromNumber = fromNumber;
        function fromBits(lowBits, highBits, unsigned) {
            return new Long2(lowBits, highBits, unsigned);
        }
        Long2.fromBits = fromBits;
        var pow_dbl = Math.pow;
        function fromString(str, unsigned, radix) {
            if (str.length === 0)
                throw Error("empty string");
            if (str === "NaN" || str === "Infinity" || str === "+Infinity" || str === "-Infinity")
                return ZERO;
            if (typeof unsigned === "number") {
                radix = unsigned, unsigned = false;
            } else {
                unsigned = !!unsigned;
            }
            radix = radix || 10;
            if (radix < 2 || 36 < radix)
                throw RangeError("radix");
            var p;
            if ((p = str.indexOf("-")) > 0)
                throw Error("interior hyphen");
            else if (p === 0) {
                return fromString(str.substring(1), unsigned, radix).neg();
            }
            var radixToPower = fromNumber(pow_dbl(radix, 8));
            var result = ZERO;
            for (var i = 0; i < str.length; i += 8) {
                var size = Math.min(8, str.length - i), value = parseInt(str.substring(i, i + size), radix);
                if (size < 8) {
                    var power = fromNumber(pow_dbl(radix, size));
                    result = result.mul(power).add(fromNumber(value));
                } else {
                    result = result.mul(radixToPower);
                    result = result.add(fromNumber(value));
                }
            }
            result.unsigned = unsigned;
            return result;
        }
        Long2.fromString = fromString;
        function fromValue(val, unsigned) {
            if (typeof val === "number")
                return fromNumber(val, unsigned);
            if (typeof val === "string")
                return fromString(val, unsigned);
            return fromBits(val.low, val.high, typeof unsigned === "boolean" ? unsigned : val.unsigned);
        }
        Long2.fromValue = fromValue;
        var TWO_PWR_16_DBL = 1 << 16;
        var TWO_PWR_24_DBL = 1 << 24;
        var TWO_PWR_32_DBL = TWO_PWR_16_DBL * TWO_PWR_16_DBL;
        var TWO_PWR_64_DBL = TWO_PWR_32_DBL * TWO_PWR_32_DBL;
        var TWO_PWR_63_DBL = TWO_PWR_64_DBL / 2;
        var TWO_PWR_24 = fromInt(TWO_PWR_24_DBL);
        var ZERO = fromInt(0);
        Long2.ZERO = ZERO;
        var UZERO = fromInt(0, true);
        Long2.UZERO = UZERO;
        var ONE = fromInt(1);
        Long2.ONE = ONE;
        var UONE = fromInt(1, true);
        Long2.UONE = UONE;
        var NEG_ONE = fromInt(-1);
        Long2.NEG_ONE = NEG_ONE;
        var MAX_VALUE = fromBits(4294967295 | 0, 2147483647 | 0, false);
        Long2.MAX_VALUE = MAX_VALUE;
        var MAX_UNSIGNED_VALUE = fromBits(4294967295 | 0, 4294967295 | 0, true);
        Long2.MAX_UNSIGNED_VALUE = MAX_UNSIGNED_VALUE;
        var MIN_VALUE = fromBits(0, 2147483648 | 0, false);
        Long2.MIN_VALUE = MIN_VALUE;
        var LongPrototype = Long2.prototype;
        LongPrototype.toInt = function toInt() {
            return this.unsigned ? this.low >>> 0 : this.low;
        };
        LongPrototype.toNumber = function toNumber() {
            if (this.unsigned)
                return (this.high >>> 0) * TWO_PWR_32_DBL + (this.low >>> 0);
            return this.high * TWO_PWR_32_DBL + (this.low >>> 0);
        };
        LongPrototype.toString = function toString(radix) {
            radix = radix || 10;
            if (radix < 2 || 36 < radix)
                throw RangeError("radix");
            if (this.isZero())
                return "0";
            if (this.isNegative()) {
                if (this.eq(MIN_VALUE)) {
                    var radixLong = fromNumber(radix), div = this.div(radixLong), rem1 = div.mul(radixLong).sub(this);
                    return div.toString(radix) + rem1.toInt().toString(radix);
                } else
                    return "-" + this.neg().toString(radix);
            }
            var radixToPower = fromNumber(pow_dbl(radix, 6), this.unsigned), rem = this;
            var result = "";
            while (true) {
                var remDiv = rem.div(radixToPower), intval = rem.sub(remDiv.mul(radixToPower)).toInt() >>> 0, digits = intval.toString(radix);
                rem = remDiv;
                if (rem.isZero())
                    return digits + result;
                else {
                    while (digits.length < 6)
                        digits = "0" + digits;
                    result = "" + digits + result;
                }
            }
        };
        LongPrototype.getHighBits = function getHighBits() {
            return this.high;
        };
        LongPrototype.getHighBitsUnsigned = function getHighBitsUnsigned() {
            return this.high >>> 0;
        };
        LongPrototype.getLowBits = function getLowBits() {
            return this.low;
        };
        LongPrototype.getLowBitsUnsigned = function getLowBitsUnsigned() {
            return this.low >>> 0;
        };
        LongPrototype.getNumBitsAbs = function getNumBitsAbs() {
            if (this.isNegative())
                return this.eq(MIN_VALUE) ? 64 : this.neg().getNumBitsAbs();
            var val = this.high != 0 ? this.high : this.low;
            for (var bit = 31; bit > 0; bit--)
                if ((val & 1 << bit) != 0)
                    break;
            return this.high != 0 ? bit + 33 : bit + 1;
        };
        LongPrototype.isZero = function isZero() {
            return this.high === 0 && this.low === 0;
        };
        LongPrototype.eqz = LongPrototype.isZero;
        LongPrototype.isNegative = function isNegative() {
            return !this.unsigned && this.high < 0;
        };
        LongPrototype.isPositive = function isPositive() {
            return this.unsigned || this.high >= 0;
        };
        LongPrototype.isOdd = function isOdd() {
            return (this.low & 1) === 1;
        };
        LongPrototype.isEven = function isEven() {
            return (this.low & 1) === 0;
        };
        LongPrototype.equals = function equals(other) {
            if (!isLong(other))
                other = fromValue(other);
            if (this.unsigned !== other.unsigned && this.high >>> 31 === 1 && other.high >>> 31 === 1)
                return false;
            return this.high === other.high && this.low === other.low;
        };
        LongPrototype.eq = LongPrototype.equals;
        LongPrototype.notEquals = function notEquals(other) {
            return !this.eq(
                /* validates */
                other
            );
        };
        LongPrototype.neq = LongPrototype.notEquals;
        LongPrototype.ne = LongPrototype.notEquals;
        LongPrototype.lessThan = function lessThan(other) {
            return this.comp(
                /* validates */
                other
            ) < 0;
        };
        LongPrototype.lt = LongPrototype.lessThan;
        LongPrototype.lessThanOrEqual = function lessThanOrEqual(other) {
            return this.comp(
                /* validates */
                other
            ) <= 0;
        };
        LongPrototype.lte = LongPrototype.lessThanOrEqual;
        LongPrototype.le = LongPrototype.lessThanOrEqual;
        LongPrototype.greaterThan = function greaterThan(other) {
            return this.comp(
                /* validates */
                other
            ) > 0;
        };
        LongPrototype.gt = LongPrototype.greaterThan;
        LongPrototype.greaterThanOrEqual = function greaterThanOrEqual(other) {
            return this.comp(
                /* validates */
                other
            ) >= 0;
        };
        LongPrototype.gte = LongPrototype.greaterThanOrEqual;
        LongPrototype.ge = LongPrototype.greaterThanOrEqual;
        LongPrototype.compare = function compare(other) {
            if (!isLong(other))
                other = fromValue(other);
            if (this.eq(other))
                return 0;
            var thisNeg = this.isNegative(), otherNeg = other.isNegative();
            if (thisNeg && !otherNeg)
                return -1;
            if (!thisNeg && otherNeg)
                return 1;
            if (!this.unsigned)
                return this.sub(other).isNegative() ? -1 : 1;
            return other.high >>> 0 > this.high >>> 0 || other.high === this.high && other.low >>> 0 > this.low >>> 0 ? -1 : 1;
        };
        LongPrototype.comp = LongPrototype.compare;
        LongPrototype.negate = function negate() {
            if (!this.unsigned && this.eq(MIN_VALUE))
                return MIN_VALUE;
            return this.not().add(ONE);
        };
        LongPrototype.neg = LongPrototype.negate;
        LongPrototype.add = function add(addend) {
            if (!isLong(addend))
                addend = fromValue(addend);
            var a48 = this.high >>> 16;
            var a32 = this.high & 65535;
            var a16 = this.low >>> 16;
            var a00 = this.low & 65535;
            var b48 = addend.high >>> 16;
            var b32 = addend.high & 65535;
            var b16 = addend.low >>> 16;
            var b00 = addend.low & 65535;
            var c48 = 0, c32 = 0, c16 = 0, c00 = 0;
            c00 += a00 + b00;
            c16 += c00 >>> 16;
            c00 &= 65535;
            c16 += a16 + b16;
            c32 += c16 >>> 16;
            c16 &= 65535;
            c32 += a32 + b32;
            c48 += c32 >>> 16;
            c32 &= 65535;
            c48 += a48 + b48;
            c48 &= 65535;
            return fromBits(c16 << 16 | c00, c48 << 16 | c32, this.unsigned);
        };
        LongPrototype.subtract = function subtract(subtrahend) {
            if (!isLong(subtrahend))
                subtrahend = fromValue(subtrahend);
            return this.add(subtrahend.neg());
        };
        LongPrototype.sub = LongPrototype.subtract;
        LongPrototype.multiply = function multiply(multiplier) {
            if (this.isZero())
                return ZERO;
            if (!isLong(multiplier))
                multiplier = fromValue(multiplier);
            if (wasm) {
                var low = wasm.mul(
                    this.low,
                    this.high,
                    multiplier.low,
                    multiplier.high
                );
                return fromBits(low, wasm.get_high(), this.unsigned);
            }
            if (multiplier.isZero())
                return ZERO;
            if (this.eq(MIN_VALUE))
                return multiplier.isOdd() ? MIN_VALUE : ZERO;
            if (multiplier.eq(MIN_VALUE))
                return this.isOdd() ? MIN_VALUE : ZERO;
            if (this.isNegative()) {
                if (multiplier.isNegative())
                    return this.neg().mul(multiplier.neg());
                else
                    return this.neg().mul(multiplier).neg();
            } else if (multiplier.isNegative())
                return this.mul(multiplier.neg()).neg();
            if (this.lt(TWO_PWR_24) && multiplier.lt(TWO_PWR_24))
                return fromNumber(this.toNumber() * multiplier.toNumber(), this.unsigned);
            var a48 = this.high >>> 16;
            var a32 = this.high & 65535;
            var a16 = this.low >>> 16;
            var a00 = this.low & 65535;
            var b48 = multiplier.high >>> 16;
            var b32 = multiplier.high & 65535;
            var b16 = multiplier.low >>> 16;
            var b00 = multiplier.low & 65535;
            var c48 = 0, c32 = 0, c16 = 0, c00 = 0;
            c00 += a00 * b00;
            c16 += c00 >>> 16;
            c00 &= 65535;
            c16 += a16 * b00;
            c32 += c16 >>> 16;
            c16 &= 65535;
            c16 += a00 * b16;
            c32 += c16 >>> 16;
            c16 &= 65535;
            c32 += a32 * b00;
            c48 += c32 >>> 16;
            c32 &= 65535;
            c32 += a16 * b16;
            c48 += c32 >>> 16;
            c32 &= 65535;
            c32 += a00 * b32;
            c48 += c32 >>> 16;
            c32 &= 65535;
            c48 += a48 * b00 + a32 * b16 + a16 * b32 + a00 * b48;
            c48 &= 65535;
            return fromBits(c16 << 16 | c00, c48 << 16 | c32, this.unsigned);
        };
        LongPrototype.mul = LongPrototype.multiply;
        LongPrototype.divide = function divide(divisor) {
            if (!isLong(divisor))
                divisor = fromValue(divisor);
            if (divisor.isZero())
                throw Error("division by zero");
            if (wasm) {
                if (!this.unsigned && this.high === -2147483648 && divisor.low === -1 && divisor.high === -1) {
                    return this;
                }
                var low = (this.unsigned ? wasm.div_u : wasm.div_s)(
                    this.low,
                    this.high,
                    divisor.low,
                    divisor.high
                );
                return fromBits(low, wasm.get_high(), this.unsigned);
            }
            if (this.isZero())
                return this.unsigned ? UZERO : ZERO;
            var approx, rem, res;
            if (!this.unsigned) {
                if (this.eq(MIN_VALUE)) {
                    if (divisor.eq(ONE) || divisor.eq(NEG_ONE))
                        return MIN_VALUE;
                    else if (divisor.eq(MIN_VALUE))
                        return ONE;
                    else {
                        var halfThis = this.shr(1);
                        approx = halfThis.div(divisor).shl(1);
                        if (approx.eq(ZERO)) {
                            return divisor.isNegative() ? ONE : NEG_ONE;
                        } else {
                            rem = this.sub(divisor.mul(approx));
                            res = approx.add(rem.div(divisor));
                            return res;
                        }
                    }
                } else if (divisor.eq(MIN_VALUE))
                    return this.unsigned ? UZERO : ZERO;
                if (this.isNegative()) {
                    if (divisor.isNegative())
                        return this.neg().div(divisor.neg());
                    return this.neg().div(divisor).neg();
                } else if (divisor.isNegative())
                    return this.div(divisor.neg()).neg();
                res = ZERO;
            } else {
                if (!divisor.unsigned)
                    divisor = divisor.toUnsigned();
                if (divisor.gt(this))
                    return UZERO;
                if (divisor.gt(this.shru(1)))
                    return UONE;
                res = UZERO;
            }
            rem = this;
            while (rem.gte(divisor)) {
                approx = Math.max(1, Math.floor(rem.toNumber() / divisor.toNumber()));
                var log2 = Math.ceil(Math.log(approx) / Math.LN2), delta = log2 <= 48 ? 1 : pow_dbl(2, log2 - 48), approxRes = fromNumber(approx), approxRem = approxRes.mul(divisor);
                while (approxRem.isNegative() || approxRem.gt(rem)) {
                    approx -= delta;
                    approxRes = fromNumber(approx, this.unsigned);
                    approxRem = approxRes.mul(divisor);
                }
                if (approxRes.isZero())
                    approxRes = ONE;
                res = res.add(approxRes);
                rem = rem.sub(approxRem);
            }
            return res;
        };
        LongPrototype.div = LongPrototype.divide;
        LongPrototype.modulo = function modulo(divisor) {
            if (!isLong(divisor))
                divisor = fromValue(divisor);
            if (wasm) {
                var low = (this.unsigned ? wasm.rem_u : wasm.rem_s)(
                    this.low,
                    this.high,
                    divisor.low,
                    divisor.high
                );
                return fromBits(low, wasm.get_high(), this.unsigned);
            }
            return this.sub(this.div(divisor).mul(divisor));
        };
        LongPrototype.mod = LongPrototype.modulo;
        LongPrototype.rem = LongPrototype.modulo;
        LongPrototype.not = function not() {
            return fromBits(~this.low, ~this.high, this.unsigned);
        };
        LongPrototype.and = function and(other) {
            if (!isLong(other))
                other = fromValue(other);
            return fromBits(this.low & other.low, this.high & other.high, this.unsigned);
        };
        LongPrototype.or = function or(other) {
            if (!isLong(other))
                other = fromValue(other);
            return fromBits(this.low | other.low, this.high | other.high, this.unsigned);
        };
        LongPrototype.xor = function xor(other) {
            if (!isLong(other))
                other = fromValue(other);
            return fromBits(this.low ^ other.low, this.high ^ other.high, this.unsigned);
        };
        LongPrototype.shiftLeft = function shiftLeft(numBits) {
            if (isLong(numBits))
                numBits = numBits.toInt();
            if ((numBits &= 63) === 0)
                return this;
            else if (numBits < 32)
                return fromBits(this.low << numBits, this.high << numBits | this.low >>> 32 - numBits, this.unsigned);
            else
                return fromBits(0, this.low << numBits - 32, this.unsigned);
        };
        LongPrototype.shl = LongPrototype.shiftLeft;
        LongPrototype.shiftRight = function shiftRight(numBits) {
            if (isLong(numBits))
                numBits = numBits.toInt();
            if ((numBits &= 63) === 0)
                return this;
            else if (numBits < 32)
                return fromBits(this.low >>> numBits | this.high << 32 - numBits, this.high >> numBits, this.unsigned);
            else
                return fromBits(this.high >> numBits - 32, this.high >= 0 ? 0 : -1, this.unsigned);
        };
        LongPrototype.shr = LongPrototype.shiftRight;
        LongPrototype.shiftRightUnsigned = function shiftRightUnsigned(numBits) {
            if (isLong(numBits))
                numBits = numBits.toInt();
            numBits &= 63;
            if (numBits === 0)
                return this;
            else {
                var high = this.high;
                if (numBits < 32) {
                    var low = this.low;
                    return fromBits(low >>> numBits | high << 32 - numBits, high >>> numBits, this.unsigned);
                } else if (numBits === 32)
                    return fromBits(high, 0, this.unsigned);
                else
                    return fromBits(high >>> numBits - 32, 0, this.unsigned);
            }
        };
        LongPrototype.shru = LongPrototype.shiftRightUnsigned;
        LongPrototype.shr_u = LongPrototype.shiftRightUnsigned;
        LongPrototype.toSigned = function toSigned() {
            if (!this.unsigned)
                return this;
            return fromBits(this.low, this.high, false);
        };
        LongPrototype.toUnsigned = function toUnsigned() {
            if (this.unsigned)
                return this;
            return fromBits(this.low, this.high, true);
        };
        LongPrototype.toBytes = function toBytes(le) {
            return le ? this.toBytesLE() : this.toBytesBE();
        };
        LongPrototype.toBytesLE = function toBytesLE() {
            var hi = this.high, lo = this.low;
            return [
                lo & 255,
                lo >>> 8 & 255,
                lo >>> 16 & 255,
                lo >>> 24,
                hi & 255,
                hi >>> 8 & 255,
                hi >>> 16 & 255,
                hi >>> 24
            ];
        };
        LongPrototype.toBytesBE = function toBytesBE() {
            var hi = this.high, lo = this.low;
            return [
                hi >>> 24,
                hi >>> 16 & 255,
                hi >>> 8 & 255,
                hi & 255,
                lo >>> 24,
                lo >>> 16 & 255,
                lo >>> 8 & 255,
                lo & 255
            ];
        };
        Long2.fromBytes = function fromBytes(bytes, unsigned, le) {
            return le ? Long2.fromBytesLE(bytes, unsigned) : Long2.fromBytesBE(bytes, unsigned);
        };
        Long2.fromBytesLE = function fromBytesLE(bytes, unsigned) {
            return new Long2(
                bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24,
                bytes[4] | bytes[5] << 8 | bytes[6] << 16 | bytes[7] << 24,
                unsigned
            );
        };
        Long2.fromBytesBE = function fromBytesBE(bytes, unsigned) {
            return new Long2(
                bytes[4] << 24 | bytes[5] << 16 | bytes[6] << 8 | bytes[7],
                bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3],
                unsigned
            );
        };
    }
});

// src/index.ts
var src_exports = {};
__export(src_exports, {
    onStart: () => onStart,
    onUpdate: () => onUpdate
});
module.exports = __toCommonJS(src_exports);

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/animator.gen.js
var import_minimal = __toESM(require_minimal2());
function createBasePBAnimator() {
    return { states: [] };
}
var PBAnimator = {
    encode(message, writer = import_minimal.default.Writer.create()) {
        for (const v of message.states) {
            PBAnimationState.encode(v, writer.uint32(10).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal.default.Reader ? input : import_minimal.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAnimator();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.states.push(PBAnimationState.decode(reader, reader.uint32()));
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBAnimationState() {
    return {
        name: "",
        clip: "",
        playing: void 0,
        weight: void 0,
        speed: void 0,
        loop: void 0,
        shouldReset: void 0
    };
}
var PBAnimationState = {
    encode(message, writer = import_minimal.default.Writer.create()) {
        if (message.name !== "") {
            writer.uint32(10).string(message.name);
        }
        if (message.clip !== "") {
            writer.uint32(18).string(message.clip);
        }
        if (message.playing !== void 0) {
            writer.uint32(24).bool(message.playing);
        }
        if (message.weight !== void 0) {
            writer.uint32(37).float(message.weight);
        }
        if (message.speed !== void 0) {
            writer.uint32(45).float(message.speed);
        }
        if (message.loop !== void 0) {
            writer.uint32(48).bool(message.loop);
        }
        if (message.shouldReset !== void 0) {
            writer.uint32(56).bool(message.shouldReset);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal.default.Reader ? input : import_minimal.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAnimationState();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.name = reader.string();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.clip = reader.string();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.playing = reader.bool();
                    continue;
                case 4:
                    if (tag != 37) {
                        break;
                    }
                    message.weight = reader.float();
                    continue;
                case 5:
                    if (tag != 45) {
                        break;
                    }
                    message.speed = reader.float();
                    continue;
                case 6:
                    if (tag != 48) {
                        break;
                    }
                    message.loop = reader.bool();
                    continue;
                case 7:
                    if (tag != 56) {
                        break;
                    }
                    message.shouldReset = reader.bool();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/Animator.gen.js
var AnimatorSchema = {
    COMPONENT_ID: 1042,
    serialize(value, builder) {
        const writer = PBAnimator.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBAnimator.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBAnimator.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBAnimator"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/audio_source.gen.js
var import_minimal2 = __toESM(require_minimal2());
function createBasePBAudioSource() {
    return { playing: void 0, volume: void 0, loop: void 0, pitch: void 0, audioClipUrl: "" };
}
var PBAudioSource = {
    encode(message, writer = import_minimal2.default.Writer.create()) {
        if (message.playing !== void 0) {
            writer.uint32(8).bool(message.playing);
        }
        if (message.volume !== void 0) {
            writer.uint32(21).float(message.volume);
        }
        if (message.loop !== void 0) {
            writer.uint32(24).bool(message.loop);
        }
        if (message.pitch !== void 0) {
            writer.uint32(37).float(message.pitch);
        }
        if (message.audioClipUrl !== "") {
            writer.uint32(42).string(message.audioClipUrl);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal2.default.Reader ? input : import_minimal2.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAudioSource();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.playing = reader.bool();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.volume = reader.float();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.loop = reader.bool();
                    continue;
                case 4:
                    if (tag != 37) {
                        break;
                    }
                    message.pitch = reader.float();
                    continue;
                case 5:
                    if (tag != 42) {
                        break;
                    }
                    message.audioClipUrl = reader.string();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/AudioSource.gen.js
var AudioSourceSchema = {
    COMPONENT_ID: 1020,
    serialize(value, builder) {
        const writer = PBAudioSource.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBAudioSource.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBAudioSource.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBAudioSource"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/audio_stream.gen.js
var import_minimal3 = __toESM(require_minimal2());
function createBasePBAudioStream() {
    return { playing: void 0, volume: void 0, url: "" };
}
var PBAudioStream = {
    encode(message, writer = import_minimal3.default.Writer.create()) {
        if (message.playing !== void 0) {
            writer.uint32(8).bool(message.playing);
        }
        if (message.volume !== void 0) {
            writer.uint32(21).float(message.volume);
        }
        if (message.url !== "") {
            writer.uint32(26).string(message.url);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal3.default.Reader ? input : import_minimal3.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAudioStream();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.playing = reader.bool();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.volume = reader.float();
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.url = reader.string();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/AudioStream.gen.js
var AudioStreamSchema = {
    COMPONENT_ID: 1021,
    serialize(value, builder) {
        const writer = PBAudioStream.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBAudioStream.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBAudioStream.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBAudioStream"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/avatar_attach.gen.js
var import_minimal4 = __toESM(require_minimal2());
var AvatarAnchorPointType;
(function(AvatarAnchorPointType2) {
    AvatarAnchorPointType2[AvatarAnchorPointType2["AAPT_POSITION"] = 0] = "AAPT_POSITION";
    AvatarAnchorPointType2[AvatarAnchorPointType2["AAPT_NAME_TAG"] = 1] = "AAPT_NAME_TAG";
    AvatarAnchorPointType2[AvatarAnchorPointType2["AAPT_LEFT_HAND"] = 2] = "AAPT_LEFT_HAND";
    AvatarAnchorPointType2[AvatarAnchorPointType2["AAPT_RIGHT_HAND"] = 3] = "AAPT_RIGHT_HAND";
})(AvatarAnchorPointType || (AvatarAnchorPointType = {}));
function createBasePBAvatarAttach() {
    return { avatarId: void 0, anchorPointId: 0 };
}
var PBAvatarAttach = {
    encode(message, writer = import_minimal4.default.Writer.create()) {
        if (message.avatarId !== void 0) {
            writer.uint32(10).string(message.avatarId);
        }
        if (message.anchorPointId !== 0) {
            writer.uint32(16).int32(message.anchorPointId);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal4.default.Reader ? input : import_minimal4.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAvatarAttach();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.avatarId = reader.string();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.anchorPointId = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/AvatarAttach.gen.js
var AvatarAttachSchema = {
    COMPONENT_ID: 1073,
    serialize(value, builder) {
        const writer = PBAvatarAttach.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBAvatarAttach.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBAvatarAttach.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBAvatarAttach"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/avatar_modifier_area.gen.js
var import_minimal6 = __toESM(require_minimal2());

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/common/vectors.gen.js
var import_minimal5 = __toESM(require_minimal2());
function createBaseVector3() {
    return { x: 0, y: 0, z: 0 };
}
var Vector3 = {
    encode(message, writer = import_minimal5.default.Writer.create()) {
        if (message.x !== 0) {
            writer.uint32(13).float(message.x);
        }
        if (message.y !== 0) {
            writer.uint32(21).float(message.y);
        }
        if (message.z !== 0) {
            writer.uint32(29).float(message.z);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal5.default.Reader ? input : import_minimal5.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseVector3();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 13) {
                        break;
                    }
                    message.x = reader.float();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.y = reader.float();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.z = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/avatar_modifier_area.gen.js
var AvatarModifierType;
(function(AvatarModifierType2) {
    AvatarModifierType2[AvatarModifierType2["AMT_HIDE_AVATARS"] = 0] = "AMT_HIDE_AVATARS";
    AvatarModifierType2[AvatarModifierType2["AMT_DISABLE_PASSPORTS"] = 1] = "AMT_DISABLE_PASSPORTS";
})(AvatarModifierType || (AvatarModifierType = {}));
function createBasePBAvatarModifierArea() {
    return { area: void 0, excludeIds: [], modifiers: [] };
}
var PBAvatarModifierArea = {
    encode(message, writer = import_minimal6.default.Writer.create()) {
        if (message.area !== void 0) {
            Vector3.encode(message.area, writer.uint32(10).fork()).ldelim();
        }
        for (const v of message.excludeIds) {
            writer.uint32(18).string(v);
        }
        writer.uint32(26).fork();
        for (const v of message.modifiers) {
            writer.int32(v);
        }
        writer.ldelim();
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal6.default.Reader ? input : import_minimal6.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAvatarModifierArea();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.area = Vector3.decode(reader, reader.uint32());
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.excludeIds.push(reader.string());
                    continue;
                case 3:
                    if (tag == 24) {
                        message.modifiers.push(reader.int32());
                        continue;
                    }
                    if (tag == 26) {
                        const end2 = reader.uint32() + reader.pos;
                        while (reader.pos < end2) {
                            message.modifiers.push(reader.int32());
                        }
                        continue;
                    }
                    break;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/AvatarModifierArea.gen.js
var AvatarModifierAreaSchema = {
    COMPONENT_ID: 1070,
    serialize(value, builder) {
        const writer = PBAvatarModifierArea.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBAvatarModifierArea.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBAvatarModifierArea.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBAvatarModifierArea"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/avatar_shape.gen.js
var import_long = __toESM(require_long());
var import_minimal8 = __toESM(require_minimal2());

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/common/colors.gen.js
var import_minimal7 = __toESM(require_minimal2());
function createBaseColor3() {
    return { r: 0, g: 0, b: 0 };
}
var Color3 = {
    encode(message, writer = import_minimal7.default.Writer.create()) {
        if (message.r !== 0) {
            writer.uint32(13).float(message.r);
        }
        if (message.g !== 0) {
            writer.uint32(21).float(message.g);
        }
        if (message.b !== 0) {
            writer.uint32(29).float(message.b);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal7.default.Reader ? input : import_minimal7.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseColor3();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 13) {
                        break;
                    }
                    message.r = reader.float();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.g = reader.float();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.b = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBaseColor4() {
    return { r: 0, g: 0, b: 0, a: 0 };
}
var Color4 = {
    encode(message, writer = import_minimal7.default.Writer.create()) {
        if (message.r !== 0) {
            writer.uint32(13).float(message.r);
        }
        if (message.g !== 0) {
            writer.uint32(21).float(message.g);
        }
        if (message.b !== 0) {
            writer.uint32(29).float(message.b);
        }
        if (message.a !== 0) {
            writer.uint32(37).float(message.a);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal7.default.Reader ? input : import_minimal7.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseColor4();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 13) {
                        break;
                    }
                    message.r = reader.float();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.g = reader.float();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.b = reader.float();
                    continue;
                case 4:
                    if (tag != 37) {
                        break;
                    }
                    message.a = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/avatar_shape.gen.js
function createBasePBAvatarShape() {
    return {
        id: "",
        name: void 0,
        bodyShape: void 0,
        skinColor: void 0,
        hairColor: void 0,
        eyeColor: void 0,
        expressionTriggerId: void 0,
        expressionTriggerTimestamp: void 0,
        talking: void 0,
        wearables: [],
        emotes: []
    };
}
var PBAvatarShape = {
    encode(message, writer = import_minimal8.default.Writer.create()) {
        if (message.id !== "") {
            writer.uint32(10).string(message.id);
        }
        if (message.name !== void 0) {
            writer.uint32(18).string(message.name);
        }
        if (message.bodyShape !== void 0) {
            writer.uint32(26).string(message.bodyShape);
        }
        if (message.skinColor !== void 0) {
            Color3.encode(message.skinColor, writer.uint32(34).fork()).ldelim();
        }
        if (message.hairColor !== void 0) {
            Color3.encode(message.hairColor, writer.uint32(42).fork()).ldelim();
        }
        if (message.eyeColor !== void 0) {
            Color3.encode(message.eyeColor, writer.uint32(50).fork()).ldelim();
        }
        if (message.expressionTriggerId !== void 0) {
            writer.uint32(58).string(message.expressionTriggerId);
        }
        if (message.expressionTriggerTimestamp !== void 0) {
            writer.uint32(64).int64(message.expressionTriggerTimestamp);
        }
        if (message.talking !== void 0) {
            writer.uint32(72).bool(message.talking);
        }
        for (const v of message.wearables) {
            writer.uint32(82).string(v);
        }
        for (const v of message.emotes) {
            writer.uint32(90).string(v);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal8.default.Reader ? input : import_minimal8.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBAvatarShape();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.id = reader.string();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.name = reader.string();
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.bodyShape = reader.string();
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.skinColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 5:
                    if (tag != 42) {
                        break;
                    }
                    message.hairColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 6:
                    if (tag != 50) {
                        break;
                    }
                    message.eyeColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 7:
                    if (tag != 58) {
                        break;
                    }
                    message.expressionTriggerId = reader.string();
                    continue;
                case 8:
                    if (tag != 64) {
                        break;
                    }
                    message.expressionTriggerTimestamp = longToNumber(reader.int64());
                    continue;
                case 9:
                    if (tag != 72) {
                        break;
                    }
                    message.talking = reader.bool();
                    continue;
                case 10:
                    if (tag != 82) {
                        break;
                    }
                    message.wearables.push(reader.string());
                    continue;
                case 11:
                    if (tag != 90) {
                        break;
                    }
                    message.emotes.push(reader.string());
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
var tsProtoGlobalThis = (() => {
    if (typeof globalThis !== "undefined") {
        return globalThis;
    }
    if (typeof self !== "undefined") {
        return self;
    }
    if (false) {
        return void 0;
    }
    if (typeof global !== "undefined") {
        return global;
    }
    throw "Unable to locate global object";
})();
function longToNumber(long) {
    if (long.gt(Number.MAX_SAFE_INTEGER)) {
        throw new tsProtoGlobalThis.Error("Value is larger than Number.MAX_SAFE_INTEGER");
    }
    return long.toNumber();
}
if (import_minimal8.default.util.Long !== import_long.default) {
    import_minimal8.default.util.Long = import_long.default;
    import_minimal8.default.configure();
}

// node_modules/@dcl/ecs/dist/components/generated/AvatarShape.gen.js
var AvatarShapeSchema = {
    COMPONENT_ID: 1080,
    serialize(value, builder) {
        const writer = PBAvatarShape.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBAvatarShape.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBAvatarShape.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBAvatarShape"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/billboard.gen.js
var import_minimal9 = __toESM(require_minimal2());
var BillboardMode;
(function(BillboardMode2) {
    BillboardMode2[BillboardMode2["BM_NONE"] = 0] = "BM_NONE";
    BillboardMode2[BillboardMode2["BM_X"] = 1] = "BM_X";
    BillboardMode2[BillboardMode2["BM_Y"] = 2] = "BM_Y";
    BillboardMode2[BillboardMode2["BM_Z"] = 4] = "BM_Z";
    BillboardMode2[BillboardMode2["BM_ALL"] = 7] = "BM_ALL";
})(BillboardMode || (BillboardMode = {}));
function createBasePBBillboard() {
    return { billboardMode: void 0 };
}
var PBBillboard = {
    encode(message, writer = import_minimal9.default.Writer.create()) {
        if (message.billboardMode !== void 0) {
            writer.uint32(8).int32(message.billboardMode);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal9.default.Reader ? input : import_minimal9.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBBillboard();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.billboardMode = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/Billboard.gen.js
var BillboardSchema = {
    COMPONENT_ID: 1090,
    serialize(value, builder) {
        const writer = PBBillboard.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBBillboard.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBBillboard.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBBillboard"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/camera_mode.gen.js
var import_minimal10 = __toESM(require_minimal2());
function createBasePBCameraMode() {
    return { mode: 0 };
}
var PBCameraMode = {
    encode(message, writer = import_minimal10.default.Writer.create()) {
        if (message.mode !== 0) {
            writer.uint32(8).int32(message.mode);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal10.default.Reader ? input : import_minimal10.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBCameraMode();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.mode = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/CameraMode.gen.js
var CameraModeSchema = {
    COMPONENT_ID: 1072,
    serialize(value, builder) {
        const writer = PBCameraMode.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBCameraMode.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBCameraMode.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBCameraMode"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/camera_mode_area.gen.js
var import_minimal11 = __toESM(require_minimal2());
function createBasePBCameraModeArea() {
    return { area: void 0, mode: 0 };
}
var PBCameraModeArea = {
    encode(message, writer = import_minimal11.default.Writer.create()) {
        if (message.area !== void 0) {
            Vector3.encode(message.area, writer.uint32(10).fork()).ldelim();
        }
        if (message.mode !== 0) {
            writer.uint32(16).int32(message.mode);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal11.default.Reader ? input : import_minimal11.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBCameraModeArea();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.area = Vector3.decode(reader, reader.uint32());
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.mode = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/CameraModeArea.gen.js
var CameraModeAreaSchema = {
    COMPONENT_ID: 1071,
    serialize(value, builder) {
        const writer = PBCameraModeArea.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBCameraModeArea.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBCameraModeArea.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBCameraModeArea"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/gltf_container.gen.js
var import_minimal12 = __toESM(require_minimal2());
function createBasePBGltfContainer() {
    return { src: "" };
}
var PBGltfContainer = {
    encode(message, writer = import_minimal12.default.Writer.create()) {
        if (message.src !== "") {
            writer.uint32(10).string(message.src);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal12.default.Reader ? input : import_minimal12.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBGltfContainer();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.src = reader.string();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/GltfContainer.gen.js
var GltfContainerSchema = {
    COMPONENT_ID: 1041,
    serialize(value, builder) {
        const writer = PBGltfContainer.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBGltfContainer.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBGltfContainer.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBGltfContainer"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/material.gen.js
var import_minimal14 = __toESM(require_minimal2());

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/common/texture.gen.js
var import_minimal13 = __toESM(require_minimal2());
var TextureWrapMode;
(function(TextureWrapMode2) {
    TextureWrapMode2[TextureWrapMode2["TWM_REPEAT"] = 0] = "TWM_REPEAT";
    TextureWrapMode2[TextureWrapMode2["TWM_CLAMP"] = 1] = "TWM_CLAMP";
    TextureWrapMode2[TextureWrapMode2["TWM_MIRROR"] = 2] = "TWM_MIRROR";
    TextureWrapMode2[TextureWrapMode2["TWM_MIRROR_ONCE"] = 3] = "TWM_MIRROR_ONCE";
})(TextureWrapMode || (TextureWrapMode = {}));
var TextureFilterMode;
(function(TextureFilterMode2) {
    TextureFilterMode2[TextureFilterMode2["TFM_POINT"] = 0] = "TFM_POINT";
    TextureFilterMode2[TextureFilterMode2["TFM_BILINEAR"] = 1] = "TFM_BILINEAR";
    TextureFilterMode2[TextureFilterMode2["TFM_TRILINEAR"] = 2] = "TFM_TRILINEAR";
})(TextureFilterMode || (TextureFilterMode = {}));
function createBaseTexture() {
    return { src: "", wrapMode: void 0, filterMode: void 0 };
}
var Texture = {
    encode(message, writer = import_minimal13.default.Writer.create()) {
        if (message.src !== "") {
            writer.uint32(10).string(message.src);
        }
        if (message.wrapMode !== void 0) {
            writer.uint32(16).int32(message.wrapMode);
        }
        if (message.filterMode !== void 0) {
            writer.uint32(24).int32(message.filterMode);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal13.default.Reader ? input : import_minimal13.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseTexture();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.src = reader.string();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.wrapMode = reader.int32();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.filterMode = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBaseAvatarTexture() {
    return { userId: "", wrapMode: void 0, filterMode: void 0 };
}
var AvatarTexture = {
    encode(message, writer = import_minimal13.default.Writer.create()) {
        if (message.userId !== "") {
            writer.uint32(10).string(message.userId);
        }
        if (message.wrapMode !== void 0) {
            writer.uint32(16).int32(message.wrapMode);
        }
        if (message.filterMode !== void 0) {
            writer.uint32(24).int32(message.filterMode);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal13.default.Reader ? input : import_minimal13.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseAvatarTexture();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.userId = reader.string();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.wrapMode = reader.int32();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.filterMode = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBaseVideoTexture() {
    return { videoPlayerEntity: 0, wrapMode: void 0, filterMode: void 0 };
}
var VideoTexture = {
    encode(message, writer = import_minimal13.default.Writer.create()) {
        if (message.videoPlayerEntity !== 0) {
            writer.uint32(8).uint32(message.videoPlayerEntity);
        }
        if (message.wrapMode !== void 0) {
            writer.uint32(16).int32(message.wrapMode);
        }
        if (message.filterMode !== void 0) {
            writer.uint32(24).int32(message.filterMode);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal13.default.Reader ? input : import_minimal13.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseVideoTexture();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.videoPlayerEntity = reader.uint32();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.wrapMode = reader.int32();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.filterMode = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBaseTextureUnion() {
    return { tex: void 0 };
}
var TextureUnion = {
    encode(message, writer = import_minimal13.default.Writer.create()) {
        switch (message.tex?.$case) {
            case "texture":
                Texture.encode(message.tex.texture, writer.uint32(10).fork()).ldelim();
                break;
            case "avatarTexture":
                AvatarTexture.encode(message.tex.avatarTexture, writer.uint32(18).fork()).ldelim();
                break;
            case "videoTexture":
                VideoTexture.encode(message.tex.videoTexture, writer.uint32(26).fork()).ldelim();
                break;
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal13.default.Reader ? input : import_minimal13.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseTextureUnion();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.tex = { $case: "texture", texture: Texture.decode(reader, reader.uint32()) };
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.tex = { $case: "avatarTexture", avatarTexture: AvatarTexture.decode(reader, reader.uint32()) };
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.tex = { $case: "videoTexture", videoTexture: VideoTexture.decode(reader, reader.uint32()) };
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/material.gen.js
var MaterialTransparencyMode;
(function(MaterialTransparencyMode2) {
    MaterialTransparencyMode2[MaterialTransparencyMode2["MTM_OPAQUE"] = 0] = "MTM_OPAQUE";
    MaterialTransparencyMode2[MaterialTransparencyMode2["MTM_ALPHA_TEST"] = 1] = "MTM_ALPHA_TEST";
    MaterialTransparencyMode2[MaterialTransparencyMode2["MTM_ALPHA_BLEND"] = 2] = "MTM_ALPHA_BLEND";
    MaterialTransparencyMode2[MaterialTransparencyMode2["MTM_ALPHA_TEST_AND_ALPHA_BLEND"] = 3] = "MTM_ALPHA_TEST_AND_ALPHA_BLEND";
    MaterialTransparencyMode2[MaterialTransparencyMode2["MTM_AUTO"] = 4] = "MTM_AUTO";
})(MaterialTransparencyMode || (MaterialTransparencyMode = {}));
function createBasePBMaterial() {
    return { material: void 0 };
}
var PBMaterial = {
    encode(message, writer = import_minimal14.default.Writer.create()) {
        switch (message.material?.$case) {
            case "unlit":
                PBMaterial_UnlitMaterial.encode(message.material.unlit, writer.uint32(10).fork()).ldelim();
                break;
            case "pbr":
                PBMaterial_PbrMaterial.encode(message.material.pbr, writer.uint32(18).fork()).ldelim();
                break;
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal14.default.Reader ? input : import_minimal14.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMaterial();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.material = { $case: "unlit", unlit: PBMaterial_UnlitMaterial.decode(reader, reader.uint32()) };
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.material = { $case: "pbr", pbr: PBMaterial_PbrMaterial.decode(reader, reader.uint32()) };
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMaterial_UnlitMaterial() {
    return { texture: void 0, alphaTest: void 0, castShadows: void 0, diffuseColor: void 0 };
}
var PBMaterial_UnlitMaterial = {
    encode(message, writer = import_minimal14.default.Writer.create()) {
        if (message.texture !== void 0) {
            TextureUnion.encode(message.texture, writer.uint32(10).fork()).ldelim();
        }
        if (message.alphaTest !== void 0) {
            writer.uint32(21).float(message.alphaTest);
        }
        if (message.castShadows !== void 0) {
            writer.uint32(24).bool(message.castShadows);
        }
        if (message.diffuseColor !== void 0) {
            Color4.encode(message.diffuseColor, writer.uint32(34).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal14.default.Reader ? input : import_minimal14.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMaterial_UnlitMaterial();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.texture = TextureUnion.decode(reader, reader.uint32());
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.alphaTest = reader.float();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.castShadows = reader.bool();
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.diffuseColor = Color4.decode(reader, reader.uint32());
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMaterial_PbrMaterial() {
    return {
        texture: void 0,
        alphaTest: void 0,
        castShadows: void 0,
        alphaTexture: void 0,
        emissiveTexture: void 0,
        bumpTexture: void 0,
        albedoColor: void 0,
        emissiveColor: void 0,
        reflectivityColor: void 0,
        transparencyMode: void 0,
        metallic: void 0,
        roughness: void 0,
        glossiness: void 0,
        specularIntensity: void 0,
        emissiveIntensity: void 0,
        directIntensity: void 0
    };
}
var PBMaterial_PbrMaterial = {
    encode(message, writer = import_minimal14.default.Writer.create()) {
        if (message.texture !== void 0) {
            TextureUnion.encode(message.texture, writer.uint32(10).fork()).ldelim();
        }
        if (message.alphaTest !== void 0) {
            writer.uint32(21).float(message.alphaTest);
        }
        if (message.castShadows !== void 0) {
            writer.uint32(24).bool(message.castShadows);
        }
        if (message.alphaTexture !== void 0) {
            TextureUnion.encode(message.alphaTexture, writer.uint32(34).fork()).ldelim();
        }
        if (message.emissiveTexture !== void 0) {
            TextureUnion.encode(message.emissiveTexture, writer.uint32(42).fork()).ldelim();
        }
        if (message.bumpTexture !== void 0) {
            TextureUnion.encode(message.bumpTexture, writer.uint32(50).fork()).ldelim();
        }
        if (message.albedoColor !== void 0) {
            Color4.encode(message.albedoColor, writer.uint32(58).fork()).ldelim();
        }
        if (message.emissiveColor !== void 0) {
            Color3.encode(message.emissiveColor, writer.uint32(66).fork()).ldelim();
        }
        if (message.reflectivityColor !== void 0) {
            Color3.encode(message.reflectivityColor, writer.uint32(74).fork()).ldelim();
        }
        if (message.transparencyMode !== void 0) {
            writer.uint32(80).int32(message.transparencyMode);
        }
        if (message.metallic !== void 0) {
            writer.uint32(93).float(message.metallic);
        }
        if (message.roughness !== void 0) {
            writer.uint32(101).float(message.roughness);
        }
        if (message.glossiness !== void 0) {
            writer.uint32(109).float(message.glossiness);
        }
        if (message.specularIntensity !== void 0) {
            writer.uint32(117).float(message.specularIntensity);
        }
        if (message.emissiveIntensity !== void 0) {
            writer.uint32(125).float(message.emissiveIntensity);
        }
        if (message.directIntensity !== void 0) {
            writer.uint32(133).float(message.directIntensity);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal14.default.Reader ? input : import_minimal14.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMaterial_PbrMaterial();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.texture = TextureUnion.decode(reader, reader.uint32());
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.alphaTest = reader.float();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.castShadows = reader.bool();
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.alphaTexture = TextureUnion.decode(reader, reader.uint32());
                    continue;
                case 5:
                    if (tag != 42) {
                        break;
                    }
                    message.emissiveTexture = TextureUnion.decode(reader, reader.uint32());
                    continue;
                case 6:
                    if (tag != 50) {
                        break;
                    }
                    message.bumpTexture = TextureUnion.decode(reader, reader.uint32());
                    continue;
                case 7:
                    if (tag != 58) {
                        break;
                    }
                    message.albedoColor = Color4.decode(reader, reader.uint32());
                    continue;
                case 8:
                    if (tag != 66) {
                        break;
                    }
                    message.emissiveColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 9:
                    if (tag != 74) {
                        break;
                    }
                    message.reflectivityColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 10:
                    if (tag != 80) {
                        break;
                    }
                    message.transparencyMode = reader.int32();
                    continue;
                case 11:
                    if (tag != 93) {
                        break;
                    }
                    message.metallic = reader.float();
                    continue;
                case 12:
                    if (tag != 101) {
                        break;
                    }
                    message.roughness = reader.float();
                    continue;
                case 13:
                    if (tag != 109) {
                        break;
                    }
                    message.glossiness = reader.float();
                    continue;
                case 14:
                    if (tag != 117) {
                        break;
                    }
                    message.specularIntensity = reader.float();
                    continue;
                case 15:
                    if (tag != 125) {
                        break;
                    }
                    message.emissiveIntensity = reader.float();
                    continue;
                case 16:
                    if (tag != 133) {
                        break;
                    }
                    message.directIntensity = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/Material.gen.js
var MaterialSchema = {
    COMPONENT_ID: 1017,
    serialize(value, builder) {
        const writer = PBMaterial.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBMaterial.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBMaterial.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBMaterial"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/mesh_collider.gen.js
var import_minimal15 = __toESM(require_minimal2());
var ColliderLayer;
(function(ColliderLayer2) {
    ColliderLayer2[ColliderLayer2["CL_NONE"] = 0] = "CL_NONE";
    ColliderLayer2[ColliderLayer2["CL_POINTER"] = 1] = "CL_POINTER";
    ColliderLayer2[ColliderLayer2["CL_PHYSICS"] = 2] = "CL_PHYSICS";
    ColliderLayer2[ColliderLayer2["CL_RESERVED1"] = 4] = "CL_RESERVED1";
    ColliderLayer2[ColliderLayer2["CL_RESERVED2"] = 8] = "CL_RESERVED2";
    ColliderLayer2[ColliderLayer2["CL_RESERVED3"] = 16] = "CL_RESERVED3";
    ColliderLayer2[ColliderLayer2["CL_RESERVED4"] = 32] = "CL_RESERVED4";
    ColliderLayer2[ColliderLayer2["CL_RESERVED5"] = 64] = "CL_RESERVED5";
    ColliderLayer2[ColliderLayer2["CL_RESERVED6"] = 128] = "CL_RESERVED6";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM1"] = 256] = "CL_CUSTOM1";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM2"] = 512] = "CL_CUSTOM2";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM3"] = 1024] = "CL_CUSTOM3";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM4"] = 2048] = "CL_CUSTOM4";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM5"] = 4096] = "CL_CUSTOM5";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM6"] = 8192] = "CL_CUSTOM6";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM7"] = 16384] = "CL_CUSTOM7";
    ColliderLayer2[ColliderLayer2["CL_CUSTOM8"] = 32768] = "CL_CUSTOM8";
})(ColliderLayer || (ColliderLayer = {}));
function createBasePBMeshCollider() {
    return { collisionMask: void 0, mesh: void 0 };
}
var PBMeshCollider = {
    encode(message, writer = import_minimal15.default.Writer.create()) {
        if (message.collisionMask !== void 0) {
            writer.uint32(8).int32(message.collisionMask);
        }
        switch (message.mesh?.$case) {
            case "box":
                PBMeshCollider_BoxMesh.encode(message.mesh.box, writer.uint32(18).fork()).ldelim();
                break;
            case "sphere":
                PBMeshCollider_SphereMesh.encode(message.mesh.sphere, writer.uint32(26).fork()).ldelim();
                break;
            case "cylinder":
                PBMeshCollider_CylinderMesh.encode(message.mesh.cylinder, writer.uint32(34).fork()).ldelim();
                break;
            case "plane":
                PBMeshCollider_PlaneMesh.encode(message.mesh.plane, writer.uint32(42).fork()).ldelim();
                break;
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal15.default.Reader ? input : import_minimal15.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshCollider();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.collisionMask = reader.int32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.mesh = { $case: "box", box: PBMeshCollider_BoxMesh.decode(reader, reader.uint32()) };
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.mesh = { $case: "sphere", sphere: PBMeshCollider_SphereMesh.decode(reader, reader.uint32()) };
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.mesh = { $case: "cylinder", cylinder: PBMeshCollider_CylinderMesh.decode(reader, reader.uint32()) };
                    continue;
                case 5:
                    if (tag != 42) {
                        break;
                    }
                    message.mesh = { $case: "plane", plane: PBMeshCollider_PlaneMesh.decode(reader, reader.uint32()) };
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshCollider_BoxMesh() {
    return {};
}
var PBMeshCollider_BoxMesh = {
    encode(_, writer = import_minimal15.default.Writer.create()) {
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal15.default.Reader ? input : import_minimal15.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshCollider_BoxMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshCollider_CylinderMesh() {
    return { radiusTop: void 0, radiusBottom: void 0 };
}
var PBMeshCollider_CylinderMesh = {
    encode(message, writer = import_minimal15.default.Writer.create()) {
        if (message.radiusTop !== void 0) {
            writer.uint32(13).float(message.radiusTop);
        }
        if (message.radiusBottom !== void 0) {
            writer.uint32(21).float(message.radiusBottom);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal15.default.Reader ? input : import_minimal15.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshCollider_CylinderMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 13) {
                        break;
                    }
                    message.radiusTop = reader.float();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.radiusBottom = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshCollider_PlaneMesh() {
    return {};
}
var PBMeshCollider_PlaneMesh = {
    encode(_, writer = import_minimal15.default.Writer.create()) {
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal15.default.Reader ? input : import_minimal15.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshCollider_PlaneMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshCollider_SphereMesh() {
    return {};
}
var PBMeshCollider_SphereMesh = {
    encode(_, writer = import_minimal15.default.Writer.create()) {
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal15.default.Reader ? input : import_minimal15.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshCollider_SphereMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/MeshCollider.gen.js
var MeshColliderSchema = {
    COMPONENT_ID: 1019,
    serialize(value, builder) {
        const writer = PBMeshCollider.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBMeshCollider.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBMeshCollider.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBMeshCollider"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/mesh_renderer.gen.js
var import_minimal16 = __toESM(require_minimal2());
function createBasePBMeshRenderer() {
    return { mesh: void 0 };
}
var PBMeshRenderer = {
    encode(message, writer = import_minimal16.default.Writer.create()) {
        switch (message.mesh?.$case) {
            case "box":
                PBMeshRenderer_BoxMesh.encode(message.mesh.box, writer.uint32(10).fork()).ldelim();
                break;
            case "sphere":
                PBMeshRenderer_SphereMesh.encode(message.mesh.sphere, writer.uint32(18).fork()).ldelim();
                break;
            case "cylinder":
                PBMeshRenderer_CylinderMesh.encode(message.mesh.cylinder, writer.uint32(26).fork()).ldelim();
                break;
            case "plane":
                PBMeshRenderer_PlaneMesh.encode(message.mesh.plane, writer.uint32(34).fork()).ldelim();
                break;
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal16.default.Reader ? input : import_minimal16.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshRenderer();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.mesh = { $case: "box", box: PBMeshRenderer_BoxMesh.decode(reader, reader.uint32()) };
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.mesh = { $case: "sphere", sphere: PBMeshRenderer_SphereMesh.decode(reader, reader.uint32()) };
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.mesh = { $case: "cylinder", cylinder: PBMeshRenderer_CylinderMesh.decode(reader, reader.uint32()) };
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.mesh = { $case: "plane", plane: PBMeshRenderer_PlaneMesh.decode(reader, reader.uint32()) };
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshRenderer_BoxMesh() {
    return { uvs: [] };
}
var PBMeshRenderer_BoxMesh = {
    encode(message, writer = import_minimal16.default.Writer.create()) {
        writer.uint32(10).fork();
        for (const v of message.uvs) {
            writer.float(v);
        }
        writer.ldelim();
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal16.default.Reader ? input : import_minimal16.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshRenderer_BoxMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag == 13) {
                        message.uvs.push(reader.float());
                        continue;
                    }
                    if (tag == 10) {
                        const end2 = reader.uint32() + reader.pos;
                        while (reader.pos < end2) {
                            message.uvs.push(reader.float());
                        }
                        continue;
                    }
                    break;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshRenderer_CylinderMesh() {
    return { radiusTop: void 0, radiusBottom: void 0 };
}
var PBMeshRenderer_CylinderMesh = {
    encode(message, writer = import_minimal16.default.Writer.create()) {
        if (message.radiusTop !== void 0) {
            writer.uint32(13).float(message.radiusTop);
        }
        if (message.radiusBottom !== void 0) {
            writer.uint32(21).float(message.radiusBottom);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal16.default.Reader ? input : import_minimal16.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshRenderer_CylinderMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 13) {
                        break;
                    }
                    message.radiusTop = reader.float();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.radiusBottom = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshRenderer_PlaneMesh() {
    return { uvs: [] };
}
var PBMeshRenderer_PlaneMesh = {
    encode(message, writer = import_minimal16.default.Writer.create()) {
        writer.uint32(10).fork();
        for (const v of message.uvs) {
            writer.float(v);
        }
        writer.ldelim();
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal16.default.Reader ? input : import_minimal16.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshRenderer_PlaneMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag == 13) {
                        message.uvs.push(reader.float());
                        continue;
                    }
                    if (tag == 10) {
                        const end2 = reader.uint32() + reader.pos;
                        while (reader.pos < end2) {
                            message.uvs.push(reader.float());
                        }
                        continue;
                    }
                    break;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBMeshRenderer_SphereMesh() {
    return {};
}
var PBMeshRenderer_SphereMesh = {
    encode(_, writer = import_minimal16.default.Writer.create()) {
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal16.default.Reader ? input : import_minimal16.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBMeshRenderer_SphereMesh();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/MeshRenderer.gen.js
var MeshRendererSchema = {
    COMPONENT_ID: 1018,
    serialize(value, builder) {
        const writer = PBMeshRenderer.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBMeshRenderer.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBMeshRenderer.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBMeshRenderer"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/nft_shape.gen.js
var import_minimal17 = __toESM(require_minimal2());
var NftFrameType;
(function(NftFrameType2) {
    NftFrameType2[NftFrameType2["NFT_CLASSIC"] = 0] = "NFT_CLASSIC";
    NftFrameType2[NftFrameType2["NFT_BAROQUE_ORNAMENT"] = 1] = "NFT_BAROQUE_ORNAMENT";
    NftFrameType2[NftFrameType2["NFT_DIAMOND_ORNAMENT"] = 2] = "NFT_DIAMOND_ORNAMENT";
    NftFrameType2[NftFrameType2["NFT_MINIMAL_WIDE"] = 3] = "NFT_MINIMAL_WIDE";
    NftFrameType2[NftFrameType2["NFT_MINIMAL_GREY"] = 4] = "NFT_MINIMAL_GREY";
    NftFrameType2[NftFrameType2["NFT_BLOCKY"] = 5] = "NFT_BLOCKY";
    NftFrameType2[NftFrameType2["NFT_GOLD_EDGES"] = 6] = "NFT_GOLD_EDGES";
    NftFrameType2[NftFrameType2["NFT_GOLD_CARVED"] = 7] = "NFT_GOLD_CARVED";
    NftFrameType2[NftFrameType2["NFT_GOLD_WIDE"] = 8] = "NFT_GOLD_WIDE";
    NftFrameType2[NftFrameType2["NFT_GOLD_ROUNDED"] = 9] = "NFT_GOLD_ROUNDED";
    NftFrameType2[NftFrameType2["NFT_METAL_MEDIUM"] = 10] = "NFT_METAL_MEDIUM";
    NftFrameType2[NftFrameType2["NFT_METAL_WIDE"] = 11] = "NFT_METAL_WIDE";
    NftFrameType2[NftFrameType2["NFT_METAL_SLIM"] = 12] = "NFT_METAL_SLIM";
    NftFrameType2[NftFrameType2["NFT_METAL_ROUNDED"] = 13] = "NFT_METAL_ROUNDED";
    NftFrameType2[NftFrameType2["NFT_PINS"] = 14] = "NFT_PINS";
    NftFrameType2[NftFrameType2["NFT_MINIMAL_BLACK"] = 15] = "NFT_MINIMAL_BLACK";
    NftFrameType2[NftFrameType2["NFT_MINIMAL_WHITE"] = 16] = "NFT_MINIMAL_WHITE";
    NftFrameType2[NftFrameType2["NFT_TAPE"] = 17] = "NFT_TAPE";
    NftFrameType2[NftFrameType2["NFT_WOOD_SLIM"] = 18] = "NFT_WOOD_SLIM";
    NftFrameType2[NftFrameType2["NFT_WOOD_WIDE"] = 19] = "NFT_WOOD_WIDE";
    NftFrameType2[NftFrameType2["NFT_WOOD_TWIGS"] = 20] = "NFT_WOOD_TWIGS";
    NftFrameType2[NftFrameType2["NFT_CANVAS"] = 21] = "NFT_CANVAS";
    NftFrameType2[NftFrameType2["NFT_NONE"] = 22] = "NFT_NONE";
})(NftFrameType || (NftFrameType = {}));
function createBasePBNftShape() {
    return { src: "", style: void 0, color: void 0 };
}
var PBNftShape = {
    encode(message, writer = import_minimal17.default.Writer.create()) {
        if (message.src !== "") {
            writer.uint32(10).string(message.src);
        }
        if (message.style !== void 0) {
            writer.uint32(16).int32(message.style);
        }
        if (message.color !== void 0) {
            Color3.encode(message.color, writer.uint32(26).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal17.default.Reader ? input : import_minimal17.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBNftShape();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.src = reader.string();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.style = reader.int32();
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.color = Color3.decode(reader, reader.uint32());
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/NftShape.gen.js
var NftShapeSchema = {
    COMPONENT_ID: 1040,
    serialize(value, builder) {
        const writer = PBNftShape.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBNftShape.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBNftShape.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBNftShape"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/pointer_events.gen.js
var import_minimal18 = __toESM(require_minimal2());
function createBasePBPointerEvents() {
    return { pointerEvents: [] };
}
var PBPointerEvents = {
    encode(message, writer = import_minimal18.default.Writer.create()) {
        for (const v of message.pointerEvents) {
            PBPointerEvents_Entry.encode(v, writer.uint32(10).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal18.default.Reader ? input : import_minimal18.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBPointerEvents();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.pointerEvents.push(PBPointerEvents_Entry.decode(reader, reader.uint32()));
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBPointerEvents_Info() {
    return { button: void 0, hoverText: void 0, maxDistance: void 0, showFeedback: void 0 };
}
var PBPointerEvents_Info = {
    encode(message, writer = import_minimal18.default.Writer.create()) {
        if (message.button !== void 0) {
            writer.uint32(8).int32(message.button);
        }
        if (message.hoverText !== void 0) {
            writer.uint32(18).string(message.hoverText);
        }
        if (message.maxDistance !== void 0) {
            writer.uint32(29).float(message.maxDistance);
        }
        if (message.showFeedback !== void 0) {
            writer.uint32(32).bool(message.showFeedback);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal18.default.Reader ? input : import_minimal18.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBPointerEvents_Info();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.button = reader.int32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.hoverText = reader.string();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.maxDistance = reader.float();
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.showFeedback = reader.bool();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};
function createBasePBPointerEvents_Entry() {
    return { eventType: 0, eventInfo: void 0 };
}
var PBPointerEvents_Entry = {
    encode(message, writer = import_minimal18.default.Writer.create()) {
        if (message.eventType !== 0) {
            writer.uint32(8).int32(message.eventType);
        }
        if (message.eventInfo !== void 0) {
            PBPointerEvents_Info.encode(message.eventInfo, writer.uint32(18).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal18.default.Reader ? input : import_minimal18.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBPointerEvents_Entry();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.eventType = reader.int32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.eventInfo = PBPointerEvents_Info.decode(reader, reader.uint32());
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/PointerEvents.gen.js
var PointerEventsSchema = {
    COMPONENT_ID: 1062,
    serialize(value, builder) {
        const writer = PBPointerEvents.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBPointerEvents.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBPointerEvents.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBPointerEvents"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/pointer_events_result.gen.js
var import_minimal20 = __toESM(require_minimal2());

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/common/raycast_hit.gen.js
var import_minimal19 = __toESM(require_minimal2());
function createBaseRaycastHit() {
    return {
        position: void 0,
        globalOrigin: void 0,
        direction: void 0,
        normalHit: void 0,
        length: 0,
        meshName: void 0,
        entityId: void 0
    };
}
var RaycastHit = {
    encode(message, writer = import_minimal19.default.Writer.create()) {
        if (message.position !== void 0) {
            Vector3.encode(message.position, writer.uint32(10).fork()).ldelim();
        }
        if (message.globalOrigin !== void 0) {
            Vector3.encode(message.globalOrigin, writer.uint32(18).fork()).ldelim();
        }
        if (message.direction !== void 0) {
            Vector3.encode(message.direction, writer.uint32(26).fork()).ldelim();
        }
        if (message.normalHit !== void 0) {
            Vector3.encode(message.normalHit, writer.uint32(34).fork()).ldelim();
        }
        if (message.length !== 0) {
            writer.uint32(45).float(message.length);
        }
        if (message.meshName !== void 0) {
            writer.uint32(50).string(message.meshName);
        }
        if (message.entityId !== void 0) {
            writer.uint32(56).uint32(message.entityId);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal19.default.Reader ? input : import_minimal19.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseRaycastHit();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.position = Vector3.decode(reader, reader.uint32());
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.globalOrigin = Vector3.decode(reader, reader.uint32());
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.direction = Vector3.decode(reader, reader.uint32());
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.normalHit = Vector3.decode(reader, reader.uint32());
                    continue;
                case 5:
                    if (tag != 45) {
                        break;
                    }
                    message.length = reader.float();
                    continue;
                case 6:
                    if (tag != 50) {
                        break;
                    }
                    message.meshName = reader.string();
                    continue;
                case 7:
                    if (tag != 56) {
                        break;
                    }
                    message.entityId = reader.uint32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/pointer_events_result.gen.js
function createBasePBPointerEventsResult() {
    return { button: 0, hit: void 0, state: 0, timestamp: 0, analog: void 0 };
}
var PBPointerEventsResult = {
    encode(message, writer = import_minimal20.default.Writer.create()) {
        if (message.button !== 0) {
            writer.uint32(8).int32(message.button);
        }
        if (message.hit !== void 0) {
            RaycastHit.encode(message.hit, writer.uint32(18).fork()).ldelim();
        }
        if (message.state !== 0) {
            writer.uint32(32).int32(message.state);
        }
        if (message.timestamp !== 0) {
            writer.uint32(40).uint32(message.timestamp);
        }
        if (message.analog !== void 0) {
            writer.uint32(53).float(message.analog);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal20.default.Reader ? input : import_minimal20.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBPointerEventsResult();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.button = reader.int32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.hit = RaycastHit.decode(reader, reader.uint32());
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.state = reader.int32();
                    continue;
                case 5:
                    if (tag != 40) {
                        break;
                    }
                    message.timestamp = reader.uint32();
                    continue;
                case 6:
                    if (tag != 53) {
                        break;
                    }
                    message.analog = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/PointerEventsResult.gen.js
var PointerEventsResultSchema = {
    COMPONENT_ID: 1063,
    serialize(value, builder) {
        const writer = PBPointerEventsResult.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBPointerEventsResult.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBPointerEventsResult.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBPointerEventsResult"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/pointer_lock.gen.js
var import_minimal21 = __toESM(require_minimal2());
function createBasePBPointerLock() {
    return { isPointerLocked: false };
}
var PBPointerLock = {
    encode(message, writer = import_minimal21.default.Writer.create()) {
        if (message.isPointerLocked === true) {
            writer.uint32(8).bool(message.isPointerLocked);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal21.default.Reader ? input : import_minimal21.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBPointerLock();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.isPointerLocked = reader.bool();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/PointerLock.gen.js
var PointerLockSchema = {
    COMPONENT_ID: 1074,
    serialize(value, builder) {
        const writer = PBPointerLock.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBPointerLock.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBPointerLock.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBPointerLock"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/raycast.gen.js
var import_minimal22 = __toESM(require_minimal2());
var RaycastQueryType;
(function(RaycastQueryType2) {
    RaycastQueryType2[RaycastQueryType2["RQT_HIT_FIRST"] = 0] = "RQT_HIT_FIRST";
    RaycastQueryType2[RaycastQueryType2["RQT_QUERY_ALL"] = 1] = "RQT_QUERY_ALL";
    RaycastQueryType2[RaycastQueryType2["RQT_NONE"] = 2] = "RQT_NONE";
})(RaycastQueryType || (RaycastQueryType = {}));
function createBasePBRaycast() {
    return {
        timestamp: void 0,
        originOffset: void 0,
        direction: void 0,
        maxDistance: 0,
        queryType: 0,
        continuous: void 0,
        collisionMask: void 0
    };
}
var PBRaycast = {
    encode(message, writer = import_minimal22.default.Writer.create()) {
        if (message.timestamp !== void 0) {
            writer.uint32(8).uint32(message.timestamp);
        }
        if (message.originOffset !== void 0) {
            Vector3.encode(message.originOffset, writer.uint32(18).fork()).ldelim();
        }
        switch (message.direction?.$case) {
            case "localDirection":
                Vector3.encode(message.direction.localDirection, writer.uint32(50).fork()).ldelim();
                break;
            case "globalDirection":
                Vector3.encode(message.direction.globalDirection, writer.uint32(26).fork()).ldelim();
                break;
            case "globalTarget":
                Vector3.encode(message.direction.globalTarget, writer.uint32(58).fork()).ldelim();
                break;
            case "targetEntity":
                writer.uint32(80).uint32(message.direction.targetEntity);
                break;
        }
        if (message.maxDistance !== 0) {
            writer.uint32(37).float(message.maxDistance);
        }
        if (message.queryType !== 0) {
            writer.uint32(40).int32(message.queryType);
        }
        if (message.continuous !== void 0) {
            writer.uint32(64).bool(message.continuous);
        }
        if (message.collisionMask !== void 0) {
            writer.uint32(72).uint32(message.collisionMask);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal22.default.Reader ? input : import_minimal22.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBRaycast();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.timestamp = reader.uint32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.originOffset = Vector3.decode(reader, reader.uint32());
                    continue;
                case 6:
                    if (tag != 50) {
                        break;
                    }
                    message.direction = { $case: "localDirection", localDirection: Vector3.decode(reader, reader.uint32()) };
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.direction = { $case: "globalDirection", globalDirection: Vector3.decode(reader, reader.uint32()) };
                    continue;
                case 7:
                    if (tag != 58) {
                        break;
                    }
                    message.direction = { $case: "globalTarget", globalTarget: Vector3.decode(reader, reader.uint32()) };
                    continue;
                case 10:
                    if (tag != 80) {
                        break;
                    }
                    message.direction = { $case: "targetEntity", targetEntity: reader.uint32() };
                    continue;
                case 4:
                    if (tag != 37) {
                        break;
                    }
                    message.maxDistance = reader.float();
                    continue;
                case 5:
                    if (tag != 40) {
                        break;
                    }
                    message.queryType = reader.int32();
                    continue;
                case 8:
                    if (tag != 64) {
                        break;
                    }
                    message.continuous = reader.bool();
                    continue;
                case 9:
                    if (tag != 72) {
                        break;
                    }
                    message.collisionMask = reader.uint32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/Raycast.gen.js
var RaycastSchema = {
    COMPONENT_ID: 1067,
    serialize(value, builder) {
        const writer = PBRaycast.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBRaycast.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBRaycast.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBRaycast"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/raycast_result.gen.js
var import_minimal23 = __toESM(require_minimal2());
function createBasePBRaycastResult() {
    return { timestamp: void 0, globalOrigin: void 0, direction: void 0, hits: [] };
}
var PBRaycastResult = {
    encode(message, writer = import_minimal23.default.Writer.create()) {
        if (message.timestamp !== void 0) {
            writer.uint32(8).uint32(message.timestamp);
        }
        if (message.globalOrigin !== void 0) {
            Vector3.encode(message.globalOrigin, writer.uint32(18).fork()).ldelim();
        }
        if (message.direction !== void 0) {
            Vector3.encode(message.direction, writer.uint32(26).fork()).ldelim();
        }
        for (const v of message.hits) {
            RaycastHit.encode(v, writer.uint32(34).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal23.default.Reader ? input : import_minimal23.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBRaycastResult();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.timestamp = reader.uint32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.globalOrigin = Vector3.decode(reader, reader.uint32());
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.direction = Vector3.decode(reader, reader.uint32());
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.hits.push(RaycastHit.decode(reader, reader.uint32()));
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/RaycastResult.gen.js
var RaycastResultSchema = {
    COMPONENT_ID: 1068,
    serialize(value, builder) {
        const writer = PBRaycastResult.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBRaycastResult.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBRaycastResult.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBRaycastResult"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/text_shape.gen.js
var import_minimal24 = __toESM(require_minimal2());
function createBasePBTextShape() {
    return {
        text: "",
        font: void 0,
        fontSize: void 0,
        fontAutoSize: void 0,
        textAlign: void 0,
        width: void 0,
        height: void 0,
        paddingTop: void 0,
        paddingRight: void 0,
        paddingBottom: void 0,
        paddingLeft: void 0,
        lineSpacing: void 0,
        lineCount: void 0,
        textWrapping: void 0,
        shadowBlur: void 0,
        shadowOffsetX: void 0,
        shadowOffsetY: void 0,
        outlineWidth: void 0,
        shadowColor: void 0,
        outlineColor: void 0,
        textColor: void 0
    };
}
var PBTextShape = {
    encode(message, writer = import_minimal24.default.Writer.create()) {
        if (message.text !== "") {
            writer.uint32(10).string(message.text);
        }
        if (message.font !== void 0) {
            writer.uint32(16).int32(message.font);
        }
        if (message.fontSize !== void 0) {
            writer.uint32(29).float(message.fontSize);
        }
        if (message.fontAutoSize !== void 0) {
            writer.uint32(32).bool(message.fontAutoSize);
        }
        if (message.textAlign !== void 0) {
            writer.uint32(40).int32(message.textAlign);
        }
        if (message.width !== void 0) {
            writer.uint32(53).float(message.width);
        }
        if (message.height !== void 0) {
            writer.uint32(61).float(message.height);
        }
        if (message.paddingTop !== void 0) {
            writer.uint32(69).float(message.paddingTop);
        }
        if (message.paddingRight !== void 0) {
            writer.uint32(77).float(message.paddingRight);
        }
        if (message.paddingBottom !== void 0) {
            writer.uint32(85).float(message.paddingBottom);
        }
        if (message.paddingLeft !== void 0) {
            writer.uint32(93).float(message.paddingLeft);
        }
        if (message.lineSpacing !== void 0) {
            writer.uint32(101).float(message.lineSpacing);
        }
        if (message.lineCount !== void 0) {
            writer.uint32(104).int32(message.lineCount);
        }
        if (message.textWrapping !== void 0) {
            writer.uint32(112).bool(message.textWrapping);
        }
        if (message.shadowBlur !== void 0) {
            writer.uint32(125).float(message.shadowBlur);
        }
        if (message.shadowOffsetX !== void 0) {
            writer.uint32(133).float(message.shadowOffsetX);
        }
        if (message.shadowOffsetY !== void 0) {
            writer.uint32(141).float(message.shadowOffsetY);
        }
        if (message.outlineWidth !== void 0) {
            writer.uint32(149).float(message.outlineWidth);
        }
        if (message.shadowColor !== void 0) {
            Color3.encode(message.shadowColor, writer.uint32(154).fork()).ldelim();
        }
        if (message.outlineColor !== void 0) {
            Color3.encode(message.outlineColor, writer.uint32(162).fork()).ldelim();
        }
        if (message.textColor !== void 0) {
            Color4.encode(message.textColor, writer.uint32(170).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal24.default.Reader ? input : import_minimal24.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBTextShape();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.text = reader.string();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.font = reader.int32();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.fontSize = reader.float();
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.fontAutoSize = reader.bool();
                    continue;
                case 5:
                    if (tag != 40) {
                        break;
                    }
                    message.textAlign = reader.int32();
                    continue;
                case 6:
                    if (tag != 53) {
                        break;
                    }
                    message.width = reader.float();
                    continue;
                case 7:
                    if (tag != 61) {
                        break;
                    }
                    message.height = reader.float();
                    continue;
                case 8:
                    if (tag != 69) {
                        break;
                    }
                    message.paddingTop = reader.float();
                    continue;
                case 9:
                    if (tag != 77) {
                        break;
                    }
                    message.paddingRight = reader.float();
                    continue;
                case 10:
                    if (tag != 85) {
                        break;
                    }
                    message.paddingBottom = reader.float();
                    continue;
                case 11:
                    if (tag != 93) {
                        break;
                    }
                    message.paddingLeft = reader.float();
                    continue;
                case 12:
                    if (tag != 101) {
                        break;
                    }
                    message.lineSpacing = reader.float();
                    continue;
                case 13:
                    if (tag != 104) {
                        break;
                    }
                    message.lineCount = reader.int32();
                    continue;
                case 14:
                    if (tag != 112) {
                        break;
                    }
                    message.textWrapping = reader.bool();
                    continue;
                case 15:
                    if (tag != 125) {
                        break;
                    }
                    message.shadowBlur = reader.float();
                    continue;
                case 16:
                    if (tag != 133) {
                        break;
                    }
                    message.shadowOffsetX = reader.float();
                    continue;
                case 17:
                    if (tag != 141) {
                        break;
                    }
                    message.shadowOffsetY = reader.float();
                    continue;
                case 18:
                    if (tag != 149) {
                        break;
                    }
                    message.outlineWidth = reader.float();
                    continue;
                case 19:
                    if (tag != 154) {
                        break;
                    }
                    message.shadowColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 20:
                    if (tag != 162) {
                        break;
                    }
                    message.outlineColor = Color3.decode(reader, reader.uint32());
                    continue;
                case 21:
                    if (tag != 170) {
                        break;
                    }
                    message.textColor = Color4.decode(reader, reader.uint32());
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/TextShape.gen.js
var TextShapeSchema = {
    COMPONENT_ID: 1030,
    serialize(value, builder) {
        const writer = PBTextShape.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBTextShape.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBTextShape.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBTextShape"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_background.gen.js
var import_minimal26 = __toESM(require_minimal2());

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/common/border_rect.gen.js
var import_minimal25 = __toESM(require_minimal2());
function createBaseBorderRect() {
    return { top: 0, left: 0, right: 0, bottom: 0 };
}
var BorderRect = {
    encode(message, writer = import_minimal25.default.Writer.create()) {
        if (message.top !== 0) {
            writer.uint32(13).float(message.top);
        }
        if (message.left !== 0) {
            writer.uint32(21).float(message.left);
        }
        if (message.right !== 0) {
            writer.uint32(29).float(message.right);
        }
        if (message.bottom !== 0) {
            writer.uint32(37).float(message.bottom);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal25.default.Reader ? input : import_minimal25.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseBorderRect();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 13) {
                        break;
                    }
                    message.top = reader.float();
                    continue;
                case 2:
                    if (tag != 21) {
                        break;
                    }
                    message.left = reader.float();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.right = reader.float();
                    continue;
                case 4:
                    if (tag != 37) {
                        break;
                    }
                    message.bottom = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_background.gen.js
var BackgroundTextureMode;
(function(BackgroundTextureMode2) {
    BackgroundTextureMode2[BackgroundTextureMode2["NINE_SLICES"] = 0] = "NINE_SLICES";
    BackgroundTextureMode2[BackgroundTextureMode2["CENTER"] = 1] = "CENTER";
    BackgroundTextureMode2[BackgroundTextureMode2["STRETCH"] = 2] = "STRETCH";
})(BackgroundTextureMode || (BackgroundTextureMode = {}));
function createBasePBUiBackground() {
    return { color: void 0, texture: void 0, textureMode: 0, textureSlices: void 0, uvs: [] };
}
var PBUiBackground = {
    encode(message, writer = import_minimal26.default.Writer.create()) {
        if (message.color !== void 0) {
            Color4.encode(message.color, writer.uint32(10).fork()).ldelim();
        }
        if (message.texture !== void 0) {
            TextureUnion.encode(message.texture, writer.uint32(18).fork()).ldelim();
        }
        if (message.textureMode !== 0) {
            writer.uint32(24).int32(message.textureMode);
        }
        if (message.textureSlices !== void 0) {
            BorderRect.encode(message.textureSlices, writer.uint32(34).fork()).ldelim();
        }
        writer.uint32(42).fork();
        for (const v of message.uvs) {
            writer.float(v);
        }
        writer.ldelim();
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal26.default.Reader ? input : import_minimal26.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiBackground();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.color = Color4.decode(reader, reader.uint32());
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.texture = TextureUnion.decode(reader, reader.uint32());
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.textureMode = reader.int32();
                    continue;
                case 4:
                    if (tag != 34) {
                        break;
                    }
                    message.textureSlices = BorderRect.decode(reader, reader.uint32());
                    continue;
                case 5:
                    if (tag == 45) {
                        message.uvs.push(reader.float());
                        continue;
                    }
                    if (tag == 42) {
                        const end2 = reader.uint32() + reader.pos;
                        while (reader.pos < end2) {
                            message.uvs.push(reader.float());
                        }
                        continue;
                    }
                    break;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiBackground.gen.js
var UiBackgroundSchema = {
    COMPONENT_ID: 1053,
    serialize(value, builder) {
        const writer = PBUiBackground.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiBackground.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiBackground.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiBackground"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_dropdown.gen.js
var import_minimal27 = __toESM(require_minimal2());
function createBasePBUiDropdown() {
    return {
        acceptEmpty: false,
        emptyLabel: void 0,
        options: [],
        selectedIndex: void 0,
        disabled: false,
        color: void 0,
        textAlign: void 0,
        font: void 0,
        fontSize: void 0
    };
}
var PBUiDropdown = {
    encode(message, writer = import_minimal27.default.Writer.create()) {
        if (message.acceptEmpty === true) {
            writer.uint32(8).bool(message.acceptEmpty);
        }
        if (message.emptyLabel !== void 0) {
            writer.uint32(18).string(message.emptyLabel);
        }
        for (const v of message.options) {
            writer.uint32(26).string(v);
        }
        if (message.selectedIndex !== void 0) {
            writer.uint32(32).int32(message.selectedIndex);
        }
        if (message.disabled === true) {
            writer.uint32(40).bool(message.disabled);
        }
        if (message.color !== void 0) {
            Color4.encode(message.color, writer.uint32(50).fork()).ldelim();
        }
        if (message.textAlign !== void 0) {
            writer.uint32(80).int32(message.textAlign);
        }
        if (message.font !== void 0) {
            writer.uint32(88).int32(message.font);
        }
        if (message.fontSize !== void 0) {
            writer.uint32(96).int32(message.fontSize);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal27.default.Reader ? input : import_minimal27.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiDropdown();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.acceptEmpty = reader.bool();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.emptyLabel = reader.string();
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.options.push(reader.string());
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.selectedIndex = reader.int32();
                    continue;
                case 5:
                    if (tag != 40) {
                        break;
                    }
                    message.disabled = reader.bool();
                    continue;
                case 6:
                    if (tag != 50) {
                        break;
                    }
                    message.color = Color4.decode(reader, reader.uint32());
                    continue;
                case 10:
                    if (tag != 80) {
                        break;
                    }
                    message.textAlign = reader.int32();
                    continue;
                case 11:
                    if (tag != 88) {
                        break;
                    }
                    message.font = reader.int32();
                    continue;
                case 12:
                    if (tag != 96) {
                        break;
                    }
                    message.fontSize = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiDropdown.gen.js
var UiDropdownSchema = {
    COMPONENT_ID: 1094,
    serialize(value, builder) {
        const writer = PBUiDropdown.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiDropdown.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiDropdown.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiDropdown"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_dropdown_result.gen.js
var import_minimal28 = __toESM(require_minimal2());
function createBasePBUiDropdownResult() {
    return { value: 0 };
}
var PBUiDropdownResult = {
    encode(message, writer = import_minimal28.default.Writer.create()) {
        if (message.value !== 0) {
            writer.uint32(8).int32(message.value);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal28.default.Reader ? input : import_minimal28.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiDropdownResult();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.value = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiDropdownResult.gen.js
var UiDropdownResultSchema = {
    COMPONENT_ID: 1096,
    serialize(value, builder) {
        const writer = PBUiDropdownResult.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiDropdownResult.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiDropdownResult.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiDropdownResult"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_input.gen.js
var import_minimal29 = __toESM(require_minimal2());
function createBasePBUiInput() {
    return {
        placeholder: "",
        color: void 0,
        placeholderColor: void 0,
        disabled: false,
        textAlign: void 0,
        font: void 0,
        fontSize: void 0,
        value: void 0
    };
}
var PBUiInput = {
    encode(message, writer = import_minimal29.default.Writer.create()) {
        if (message.placeholder !== "") {
            writer.uint32(10).string(message.placeholder);
        }
        if (message.color !== void 0) {
            Color4.encode(message.color, writer.uint32(18).fork()).ldelim();
        }
        if (message.placeholderColor !== void 0) {
            Color4.encode(message.placeholderColor, writer.uint32(26).fork()).ldelim();
        }
        if (message.disabled === true) {
            writer.uint32(32).bool(message.disabled);
        }
        if (message.textAlign !== void 0) {
            writer.uint32(80).int32(message.textAlign);
        }
        if (message.font !== void 0) {
            writer.uint32(88).int32(message.font);
        }
        if (message.fontSize !== void 0) {
            writer.uint32(96).int32(message.fontSize);
        }
        if (message.value !== void 0) {
            writer.uint32(106).string(message.value);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal29.default.Reader ? input : import_minimal29.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiInput();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.placeholder = reader.string();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.color = Color4.decode(reader, reader.uint32());
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.placeholderColor = Color4.decode(reader, reader.uint32());
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.disabled = reader.bool();
                    continue;
                case 10:
                    if (tag != 80) {
                        break;
                    }
                    message.textAlign = reader.int32();
                    continue;
                case 11:
                    if (tag != 88) {
                        break;
                    }
                    message.font = reader.int32();
                    continue;
                case 12:
                    if (tag != 96) {
                        break;
                    }
                    message.fontSize = reader.int32();
                    continue;
                case 13:
                    if (tag != 106) {
                        break;
                    }
                    message.value = reader.string();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiInput.gen.js
var UiInputSchema = {
    COMPONENT_ID: 1093,
    serialize(value, builder) {
        const writer = PBUiInput.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiInput.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiInput.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiInput"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_input_result.gen.js
var import_minimal30 = __toESM(require_minimal2());
function createBasePBUiInputResult() {
    return { value: "" };
}
var PBUiInputResult = {
    encode(message, writer = import_minimal30.default.Writer.create()) {
        if (message.value !== "") {
            writer.uint32(10).string(message.value);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal30.default.Reader ? input : import_minimal30.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiInputResult();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.value = reader.string();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiInputResult.gen.js
var UiInputResultSchema = {
    COMPONENT_ID: 1095,
    serialize(value, builder) {
        const writer = PBUiInputResult.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiInputResult.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiInputResult.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiInputResult"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_text.gen.js
var import_minimal31 = __toESM(require_minimal2());
function createBasePBUiText() {
    return { value: "", color: void 0, textAlign: void 0, font: void 0, fontSize: void 0 };
}
var PBUiText = {
    encode(message, writer = import_minimal31.default.Writer.create()) {
        if (message.value !== "") {
            writer.uint32(10).string(message.value);
        }
        if (message.color !== void 0) {
            Color4.encode(message.color, writer.uint32(18).fork()).ldelim();
        }
        if (message.textAlign !== void 0) {
            writer.uint32(24).int32(message.textAlign);
        }
        if (message.font !== void 0) {
            writer.uint32(32).int32(message.font);
        }
        if (message.fontSize !== void 0) {
            writer.uint32(40).int32(message.fontSize);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal31.default.Reader ? input : import_minimal31.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiText();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.value = reader.string();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.color = Color4.decode(reader, reader.uint32());
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.textAlign = reader.int32();
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.font = reader.int32();
                    continue;
                case 5:
                    if (tag != 40) {
                        break;
                    }
                    message.fontSize = reader.int32();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiText.gen.js
var UiTextSchema = {
    COMPONENT_ID: 1052,
    serialize(value, builder) {
        const writer = PBUiText.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiText.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiText.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiText"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/ui_transform.gen.js
var import_minimal32 = __toESM(require_minimal2());
var YGPositionType;
(function(YGPositionType2) {
    YGPositionType2[YGPositionType2["YGPT_RELATIVE"] = 0] = "YGPT_RELATIVE";
    YGPositionType2[YGPositionType2["YGPT_ABSOLUTE"] = 1] = "YGPT_ABSOLUTE";
})(YGPositionType || (YGPositionType = {}));
var YGAlign;
(function(YGAlign2) {
    YGAlign2[YGAlign2["YGA_AUTO"] = 0] = "YGA_AUTO";
    YGAlign2[YGAlign2["YGA_FLEX_START"] = 1] = "YGA_FLEX_START";
    YGAlign2[YGAlign2["YGA_CENTER"] = 2] = "YGA_CENTER";
    YGAlign2[YGAlign2["YGA_FLEX_END"] = 3] = "YGA_FLEX_END";
    YGAlign2[YGAlign2["YGA_STRETCH"] = 4] = "YGA_STRETCH";
    YGAlign2[YGAlign2["YGA_BASELINE"] = 5] = "YGA_BASELINE";
    YGAlign2[YGAlign2["YGA_SPACE_BETWEEN"] = 6] = "YGA_SPACE_BETWEEN";
    YGAlign2[YGAlign2["YGA_SPACE_AROUND"] = 7] = "YGA_SPACE_AROUND";
})(YGAlign || (YGAlign = {}));
var YGUnit;
(function(YGUnit2) {
    YGUnit2[YGUnit2["YGU_UNDEFINED"] = 0] = "YGU_UNDEFINED";
    YGUnit2[YGUnit2["YGU_POINT"] = 1] = "YGU_POINT";
    YGUnit2[YGUnit2["YGU_PERCENT"] = 2] = "YGU_PERCENT";
    YGUnit2[YGUnit2["YGU_AUTO"] = 3] = "YGU_AUTO";
})(YGUnit || (YGUnit = {}));
var YGFlexDirection;
(function(YGFlexDirection2) {
    YGFlexDirection2[YGFlexDirection2["YGFD_ROW"] = 0] = "YGFD_ROW";
    YGFlexDirection2[YGFlexDirection2["YGFD_COLUMN"] = 1] = "YGFD_COLUMN";
    YGFlexDirection2[YGFlexDirection2["YGFD_COLUMN_REVERSE"] = 2] = "YGFD_COLUMN_REVERSE";
    YGFlexDirection2[YGFlexDirection2["YGFD_ROW_REVERSE"] = 3] = "YGFD_ROW_REVERSE";
})(YGFlexDirection || (YGFlexDirection = {}));
var YGWrap;
(function(YGWrap2) {
    YGWrap2[YGWrap2["YGW_NO_WRAP"] = 0] = "YGW_NO_WRAP";
    YGWrap2[YGWrap2["YGW_WRAP"] = 1] = "YGW_WRAP";
    YGWrap2[YGWrap2["YGW_WRAP_REVERSE"] = 2] = "YGW_WRAP_REVERSE";
})(YGWrap || (YGWrap = {}));
var YGJustify;
(function(YGJustify2) {
    YGJustify2[YGJustify2["YGJ_FLEX_START"] = 0] = "YGJ_FLEX_START";
    YGJustify2[YGJustify2["YGJ_CENTER"] = 1] = "YGJ_CENTER";
    YGJustify2[YGJustify2["YGJ_FLEX_END"] = 2] = "YGJ_FLEX_END";
    YGJustify2[YGJustify2["YGJ_SPACE_BETWEEN"] = 3] = "YGJ_SPACE_BETWEEN";
    YGJustify2[YGJustify2["YGJ_SPACE_AROUND"] = 4] = "YGJ_SPACE_AROUND";
    YGJustify2[YGJustify2["YGJ_SPACE_EVENLY"] = 5] = "YGJ_SPACE_EVENLY";
})(YGJustify || (YGJustify = {}));
var YGOverflow;
(function(YGOverflow2) {
    YGOverflow2[YGOverflow2["YGO_VISIBLE"] = 0] = "YGO_VISIBLE";
    YGOverflow2[YGOverflow2["YGO_HIDDEN"] = 1] = "YGO_HIDDEN";
    YGOverflow2[YGOverflow2["YGO_SCROLL"] = 2] = "YGO_SCROLL";
})(YGOverflow || (YGOverflow = {}));
var YGDisplay;
(function(YGDisplay2) {
    YGDisplay2[YGDisplay2["YGD_FLEX"] = 0] = "YGD_FLEX";
    YGDisplay2[YGDisplay2["YGD_NONE"] = 1] = "YGD_NONE";
})(YGDisplay || (YGDisplay = {}));
var YGEdge;
(function(YGEdge2) {
    YGEdge2[YGEdge2["YGE_LEFT"] = 0] = "YGE_LEFT";
    YGEdge2[YGEdge2["YGE_TOP"] = 1] = "YGE_TOP";
    YGEdge2[YGEdge2["YGE_RIGHT"] = 2] = "YGE_RIGHT";
    YGEdge2[YGEdge2["YGE_BOTTOM"] = 3] = "YGE_BOTTOM";
    YGEdge2[YGEdge2["YGE_START"] = 4] = "YGE_START";
    YGEdge2[YGEdge2["YGE_END"] = 5] = "YGE_END";
    YGEdge2[YGEdge2["YGE_HORIZONTAL"] = 6] = "YGE_HORIZONTAL";
    YGEdge2[YGEdge2["YGE_VERTICAL"] = 7] = "YGE_VERTICAL";
    YGEdge2[YGEdge2["YGE_ALL"] = 8] = "YGE_ALL";
})(YGEdge || (YGEdge = {}));
function createBasePBUiTransform() {
    return {
        parent: 0,
        rightOf: 0,
        alignContent: void 0,
        alignItems: void 0,
        flexWrap: void 0,
        flexShrink: void 0,
        positionType: 0,
        alignSelf: 0,
        flexDirection: 0,
        justifyContent: 0,
        overflow: 0,
        display: 0,
        flexBasisUnit: 0,
        flexBasis: 0,
        flexGrow: 0,
        widthUnit: 0,
        width: 0,
        heightUnit: 0,
        height: 0,
        minWidthUnit: 0,
        minWidth: 0,
        minHeightUnit: 0,
        minHeight: 0,
        maxWidthUnit: 0,
        maxWidth: 0,
        maxHeightUnit: 0,
        maxHeight: 0,
        positionLeftUnit: 0,
        positionLeft: 0,
        positionTopUnit: 0,
        positionTop: 0,
        positionRightUnit: 0,
        positionRight: 0,
        positionBottomUnit: 0,
        positionBottom: 0,
        marginLeftUnit: 0,
        marginLeft: 0,
        marginTopUnit: 0,
        marginTop: 0,
        marginRightUnit: 0,
        marginRight: 0,
        marginBottomUnit: 0,
        marginBottom: 0,
        paddingLeftUnit: 0,
        paddingLeft: 0,
        paddingTopUnit: 0,
        paddingTop: 0,
        paddingRightUnit: 0,
        paddingRight: 0,
        paddingBottomUnit: 0,
        paddingBottom: 0
    };
}
var PBUiTransform = {
    encode(message, writer = import_minimal32.default.Writer.create()) {
        if (message.parent !== 0) {
            writer.uint32(8).int32(message.parent);
        }
        if (message.rightOf !== 0) {
            writer.uint32(16).int32(message.rightOf);
        }
        if (message.alignContent !== void 0) {
            writer.uint32(24).int32(message.alignContent);
        }
        if (message.alignItems !== void 0) {
            writer.uint32(32).int32(message.alignItems);
        }
        if (message.flexWrap !== void 0) {
            writer.uint32(40).int32(message.flexWrap);
        }
        if (message.flexShrink !== void 0) {
            writer.uint32(53).float(message.flexShrink);
        }
        if (message.positionType !== 0) {
            writer.uint32(56).int32(message.positionType);
        }
        if (message.alignSelf !== 0) {
            writer.uint32(64).int32(message.alignSelf);
        }
        if (message.flexDirection !== 0) {
            writer.uint32(72).int32(message.flexDirection);
        }
        if (message.justifyContent !== 0) {
            writer.uint32(80).int32(message.justifyContent);
        }
        if (message.overflow !== 0) {
            writer.uint32(88).int32(message.overflow);
        }
        if (message.display !== 0) {
            writer.uint32(96).int32(message.display);
        }
        if (message.flexBasisUnit !== 0) {
            writer.uint32(104).int32(message.flexBasisUnit);
        }
        if (message.flexBasis !== 0) {
            writer.uint32(117).float(message.flexBasis);
        }
        if (message.flexGrow !== 0) {
            writer.uint32(125).float(message.flexGrow);
        }
        if (message.widthUnit !== 0) {
            writer.uint32(128).int32(message.widthUnit);
        }
        if (message.width !== 0) {
            writer.uint32(141).float(message.width);
        }
        if (message.heightUnit !== 0) {
            writer.uint32(144).int32(message.heightUnit);
        }
        if (message.height !== 0) {
            writer.uint32(157).float(message.height);
        }
        if (message.minWidthUnit !== 0) {
            writer.uint32(160).int32(message.minWidthUnit);
        }
        if (message.minWidth !== 0) {
            writer.uint32(173).float(message.minWidth);
        }
        if (message.minHeightUnit !== 0) {
            writer.uint32(176).int32(message.minHeightUnit);
        }
        if (message.minHeight !== 0) {
            writer.uint32(189).float(message.minHeight);
        }
        if (message.maxWidthUnit !== 0) {
            writer.uint32(192).int32(message.maxWidthUnit);
        }
        if (message.maxWidth !== 0) {
            writer.uint32(205).float(message.maxWidth);
        }
        if (message.maxHeightUnit !== 0) {
            writer.uint32(208).int32(message.maxHeightUnit);
        }
        if (message.maxHeight !== 0) {
            writer.uint32(221).float(message.maxHeight);
        }
        if (message.positionLeftUnit !== 0) {
            writer.uint32(224).int32(message.positionLeftUnit);
        }
        if (message.positionLeft !== 0) {
            writer.uint32(237).float(message.positionLeft);
        }
        if (message.positionTopUnit !== 0) {
            writer.uint32(240).int32(message.positionTopUnit);
        }
        if (message.positionTop !== 0) {
            writer.uint32(253).float(message.positionTop);
        }
        if (message.positionRightUnit !== 0) {
            writer.uint32(256).int32(message.positionRightUnit);
        }
        if (message.positionRight !== 0) {
            writer.uint32(269).float(message.positionRight);
        }
        if (message.positionBottomUnit !== 0) {
            writer.uint32(272).int32(message.positionBottomUnit);
        }
        if (message.positionBottom !== 0) {
            writer.uint32(285).float(message.positionBottom);
        }
        if (message.marginLeftUnit !== 0) {
            writer.uint32(288).int32(message.marginLeftUnit);
        }
        if (message.marginLeft !== 0) {
            writer.uint32(301).float(message.marginLeft);
        }
        if (message.marginTopUnit !== 0) {
            writer.uint32(304).int32(message.marginTopUnit);
        }
        if (message.marginTop !== 0) {
            writer.uint32(317).float(message.marginTop);
        }
        if (message.marginRightUnit !== 0) {
            writer.uint32(320).int32(message.marginRightUnit);
        }
        if (message.marginRight !== 0) {
            writer.uint32(333).float(message.marginRight);
        }
        if (message.marginBottomUnit !== 0) {
            writer.uint32(336).int32(message.marginBottomUnit);
        }
        if (message.marginBottom !== 0) {
            writer.uint32(349).float(message.marginBottom);
        }
        if (message.paddingLeftUnit !== 0) {
            writer.uint32(352).int32(message.paddingLeftUnit);
        }
        if (message.paddingLeft !== 0) {
            writer.uint32(365).float(message.paddingLeft);
        }
        if (message.paddingTopUnit !== 0) {
            writer.uint32(368).int32(message.paddingTopUnit);
        }
        if (message.paddingTop !== 0) {
            writer.uint32(381).float(message.paddingTop);
        }
        if (message.paddingRightUnit !== 0) {
            writer.uint32(384).int32(message.paddingRightUnit);
        }
        if (message.paddingRight !== 0) {
            writer.uint32(397).float(message.paddingRight);
        }
        if (message.paddingBottomUnit !== 0) {
            writer.uint32(400).int32(message.paddingBottomUnit);
        }
        if (message.paddingBottom !== 0) {
            writer.uint32(413).float(message.paddingBottom);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal32.default.Reader ? input : import_minimal32.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBUiTransform();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.parent = reader.int32();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.rightOf = reader.int32();
                    continue;
                case 3:
                    if (tag != 24) {
                        break;
                    }
                    message.alignContent = reader.int32();
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.alignItems = reader.int32();
                    continue;
                case 5:
                    if (tag != 40) {
                        break;
                    }
                    message.flexWrap = reader.int32();
                    continue;
                case 6:
                    if (tag != 53) {
                        break;
                    }
                    message.flexShrink = reader.float();
                    continue;
                case 7:
                    if (tag != 56) {
                        break;
                    }
                    message.positionType = reader.int32();
                    continue;
                case 8:
                    if (tag != 64) {
                        break;
                    }
                    message.alignSelf = reader.int32();
                    continue;
                case 9:
                    if (tag != 72) {
                        break;
                    }
                    message.flexDirection = reader.int32();
                    continue;
                case 10:
                    if (tag != 80) {
                        break;
                    }
                    message.justifyContent = reader.int32();
                    continue;
                case 11:
                    if (tag != 88) {
                        break;
                    }
                    message.overflow = reader.int32();
                    continue;
                case 12:
                    if (tag != 96) {
                        break;
                    }
                    message.display = reader.int32();
                    continue;
                case 13:
                    if (tag != 104) {
                        break;
                    }
                    message.flexBasisUnit = reader.int32();
                    continue;
                case 14:
                    if (tag != 117) {
                        break;
                    }
                    message.flexBasis = reader.float();
                    continue;
                case 15:
                    if (tag != 125) {
                        break;
                    }
                    message.flexGrow = reader.float();
                    continue;
                case 16:
                    if (tag != 128) {
                        break;
                    }
                    message.widthUnit = reader.int32();
                    continue;
                case 17:
                    if (tag != 141) {
                        break;
                    }
                    message.width = reader.float();
                    continue;
                case 18:
                    if (tag != 144) {
                        break;
                    }
                    message.heightUnit = reader.int32();
                    continue;
                case 19:
                    if (tag != 157) {
                        break;
                    }
                    message.height = reader.float();
                    continue;
                case 20:
                    if (tag != 160) {
                        break;
                    }
                    message.minWidthUnit = reader.int32();
                    continue;
                case 21:
                    if (tag != 173) {
                        break;
                    }
                    message.minWidth = reader.float();
                    continue;
                case 22:
                    if (tag != 176) {
                        break;
                    }
                    message.minHeightUnit = reader.int32();
                    continue;
                case 23:
                    if (tag != 189) {
                        break;
                    }
                    message.minHeight = reader.float();
                    continue;
                case 24:
                    if (tag != 192) {
                        break;
                    }
                    message.maxWidthUnit = reader.int32();
                    continue;
                case 25:
                    if (tag != 205) {
                        break;
                    }
                    message.maxWidth = reader.float();
                    continue;
                case 26:
                    if (tag != 208) {
                        break;
                    }
                    message.maxHeightUnit = reader.int32();
                    continue;
                case 27:
                    if (tag != 221) {
                        break;
                    }
                    message.maxHeight = reader.float();
                    continue;
                case 28:
                    if (tag != 224) {
                        break;
                    }
                    message.positionLeftUnit = reader.int32();
                    continue;
                case 29:
                    if (tag != 237) {
                        break;
                    }
                    message.positionLeft = reader.float();
                    continue;
                case 30:
                    if (tag != 240) {
                        break;
                    }
                    message.positionTopUnit = reader.int32();
                    continue;
                case 31:
                    if (tag != 253) {
                        break;
                    }
                    message.positionTop = reader.float();
                    continue;
                case 32:
                    if (tag != 256) {
                        break;
                    }
                    message.positionRightUnit = reader.int32();
                    continue;
                case 33:
                    if (tag != 269) {
                        break;
                    }
                    message.positionRight = reader.float();
                    continue;
                case 34:
                    if (tag != 272) {
                        break;
                    }
                    message.positionBottomUnit = reader.int32();
                    continue;
                case 35:
                    if (tag != 285) {
                        break;
                    }
                    message.positionBottom = reader.float();
                    continue;
                case 36:
                    if (tag != 288) {
                        break;
                    }
                    message.marginLeftUnit = reader.int32();
                    continue;
                case 37:
                    if (tag != 301) {
                        break;
                    }
                    message.marginLeft = reader.float();
                    continue;
                case 38:
                    if (tag != 304) {
                        break;
                    }
                    message.marginTopUnit = reader.int32();
                    continue;
                case 39:
                    if (tag != 317) {
                        break;
                    }
                    message.marginTop = reader.float();
                    continue;
                case 40:
                    if (tag != 320) {
                        break;
                    }
                    message.marginRightUnit = reader.int32();
                    continue;
                case 41:
                    if (tag != 333) {
                        break;
                    }
                    message.marginRight = reader.float();
                    continue;
                case 42:
                    if (tag != 336) {
                        break;
                    }
                    message.marginBottomUnit = reader.int32();
                    continue;
                case 43:
                    if (tag != 349) {
                        break;
                    }
                    message.marginBottom = reader.float();
                    continue;
                case 44:
                    if (tag != 352) {
                        break;
                    }
                    message.paddingLeftUnit = reader.int32();
                    continue;
                case 45:
                    if (tag != 365) {
                        break;
                    }
                    message.paddingLeft = reader.float();
                    continue;
                case 46:
                    if (tag != 368) {
                        break;
                    }
                    message.paddingTopUnit = reader.int32();
                    continue;
                case 47:
                    if (tag != 381) {
                        break;
                    }
                    message.paddingTop = reader.float();
                    continue;
                case 48:
                    if (tag != 384) {
                        break;
                    }
                    message.paddingRightUnit = reader.int32();
                    continue;
                case 49:
                    if (tag != 397) {
                        break;
                    }
                    message.paddingRight = reader.float();
                    continue;
                case 50:
                    if (tag != 400) {
                        break;
                    }
                    message.paddingBottomUnit = reader.int32();
                    continue;
                case 51:
                    if (tag != 413) {
                        break;
                    }
                    message.paddingBottom = reader.float();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/UiTransform.gen.js
var UiTransformSchema = {
    COMPONENT_ID: 1050,
    serialize(value, builder) {
        const writer = PBUiTransform.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBUiTransform.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBUiTransform.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBUiTransform"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/video_player.gen.js
var import_minimal33 = __toESM(require_minimal2());
function createBasePBVideoPlayer() {
    return {
        src: "",
        playing: void 0,
        position: void 0,
        volume: void 0,
        playbackRate: void 0,
        loop: void 0
    };
}
var PBVideoPlayer = {
    encode(message, writer = import_minimal33.default.Writer.create()) {
        if (message.src !== "") {
            writer.uint32(10).string(message.src);
        }
        if (message.playing !== void 0) {
            writer.uint32(16).bool(message.playing);
        }
        if (message.position !== void 0) {
            writer.uint32(29).float(message.position);
        }
        if (message.volume !== void 0) {
            writer.uint32(37).float(message.volume);
        }
        if (message.playbackRate !== void 0) {
            writer.uint32(45).float(message.playbackRate);
        }
        if (message.loop !== void 0) {
            writer.uint32(48).bool(message.loop);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal33.default.Reader ? input : import_minimal33.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBVideoPlayer();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.src = reader.string();
                    continue;
                case 2:
                    if (tag != 16) {
                        break;
                    }
                    message.playing = reader.bool();
                    continue;
                case 3:
                    if (tag != 29) {
                        break;
                    }
                    message.position = reader.float();
                    continue;
                case 4:
                    if (tag != 37) {
                        break;
                    }
                    message.volume = reader.float();
                    continue;
                case 5:
                    if (tag != 45) {
                        break;
                    }
                    message.playbackRate = reader.float();
                    continue;
                case 6:
                    if (tag != 48) {
                        break;
                    }
                    message.loop = reader.bool();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/VideoPlayer.gen.js
var VideoPlayerSchema = {
    COMPONENT_ID: 1043,
    serialize(value, builder) {
        const writer = PBVideoPlayer.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBVideoPlayer.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBVideoPlayer.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBVideoPlayer"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/visibility_component.gen.js
var import_minimal34 = __toESM(require_minimal2());
function createBasePBVisibilityComponent() {
    return { visible: void 0 };
}
var PBVisibilityComponent = {
    encode(message, writer = import_minimal34.default.Writer.create()) {
        if (message.visible !== void 0) {
            writer.uint32(8).bool(message.visible);
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal34.default.Reader ? input : import_minimal34.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBasePBVisibilityComponent();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.visible = reader.bool();
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    }
};

// node_modules/@dcl/ecs/dist/components/generated/VisibilityComponent.gen.js
var VisibilityComponentSchema = {
    COMPONENT_ID: 1081,
    serialize(value, builder) {
        const writer = PBVisibilityComponent.encode(value);
        const buffer = new Uint8Array(writer.finish(), 0, writer.len);
        builder.writeBuffer(buffer, false);
    },
    deserialize(reader) {
        return PBVisibilityComponent.decode(reader.buffer(), reader.remainingBytes());
    },
    create() {
        return PBVisibilityComponent.decode(new Uint8Array());
    },
    jsonSchema: {
        type: "object",
        properties: {},
        serializationType: "protocol-buffer",
        protocolBuffer: "PBVisibilityComponent"
    }
};

// node_modules/@dcl/ecs/dist/components/generated/index.gen.js
var Animator = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::Animator", AnimatorSchema);
var AudioSource = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::AudioSource", AudioSourceSchema);
var AudioStream = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::AudioStream", AudioStreamSchema);
var AvatarAttach = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::AvatarAttach", AvatarAttachSchema);
var AvatarModifierArea = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::AvatarModifierArea", AvatarModifierAreaSchema);
var AvatarShape = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::AvatarShape", AvatarShapeSchema);
var Billboard = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::Billboard", BillboardSchema);
var CameraMode = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::CameraMode", CameraModeSchema);
var CameraModeArea = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::CameraModeArea", CameraModeAreaSchema);
var GltfContainer = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::GltfContainer", GltfContainerSchema);
var Material = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::Material", MaterialSchema);
var MeshCollider = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::MeshCollider", MeshColliderSchema);
var MeshRenderer = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::MeshRenderer", MeshRendererSchema);
var NftShape = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::NftShape", NftShapeSchema);
var PointerEvents = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::PointerEvents", PointerEventsSchema);
var PointerEventsResult = (engine2) => /* @__PURE__ */ engine2.defineValueSetComponentFromSchema("core::PointerEventsResult", PointerEventsResultSchema, {
    timestampFunction: (t) => t.timestamp,
    maxElements: 100
});
var PointerLock = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::PointerLock", PointerLockSchema);
var Raycast = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::Raycast", RaycastSchema);
var RaycastResult = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::RaycastResult", RaycastResultSchema);
var TextShape = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::TextShape", TextShapeSchema);
var UiBackground = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiBackground", UiBackgroundSchema);
var UiDropdown = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiDropdown", UiDropdownSchema);
var UiDropdownResult = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiDropdownResult", UiDropdownResultSchema);
var UiInput = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiInput", UiInputSchema);
var UiInputResult = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiInputResult", UiInputResultSchema);
var UiText = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiText", UiTextSchema);
var UiTransform = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::UiTransform", UiTransformSchema);
var VideoPlayer = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::VideoPlayer", VideoPlayerSchema);
var VisibilityComponent = (engine2) => /* @__PURE__ */ engine2.defineComponentFromSchema("core::VisibilityComponent", VisibilityComponentSchema);
var componentDefinitionByName = {
    "core::Animator": Animator,
    "core::AudioSource": AudioSource,
    "core::AudioStream": AudioStream,
    "core::AvatarAttach": AvatarAttach,
    "core::AvatarModifierArea": AvatarModifierArea,
    "core::AvatarShape": AvatarShape,
    "core::Billboard": Billboard,
    "core::CameraMode": CameraMode,
    "core::CameraModeArea": CameraModeArea,
    "core::GltfContainer": GltfContainer,
    "core::Material": Material,
    "core::MeshCollider": MeshCollider,
    "core::MeshRenderer": MeshRenderer,
    "core::NftShape": NftShape,
    "core::PointerEvents": PointerEvents,
    "core::PointerEventsResult": PointerEventsResult,
    "core::PointerLock": PointerLock,
    "core::Raycast": Raycast,
    "core::RaycastResult": RaycastResult,
    "core::TextShape": TextShape,
    "core::UiBackground": UiBackground,
    "core::UiDropdown": UiDropdown,
    "core::UiDropdownResult": UiDropdownResult,
    "core::UiInput": UiInput,
    "core::UiInputResult": UiInputResult,
    "core::UiText": UiText,
    "core::UiTransform": UiTransform,
    "core::VideoPlayer": VideoPlayer,
    "core::VisibilityComponent": VisibilityComponent
};

// node_modules/@dcl/ecs/dist/components/extended/Material.js
var TextureHelper = {
    Common(texture) {
        return {
            tex: {
                $case: "texture",
                texture
            }
        };
    },
    Avatar(avatarTexture) {
        return {
            tex: {
                $case: "avatarTexture",
                avatarTexture
            }
        };
    },
    Video(videoTexture) {
        return {
            tex: {
                $case: "videoTexture",
                videoTexture
            }
        };
    }
};
function defineMaterialComponent(engine2) {
    const theComponent = Material(engine2);
    return {
        ...theComponent,
        Texture: TextureHelper,
        setBasicMaterial(entity, material) {
            theComponent.createOrReplace(entity, {
                material: {
                    $case: "unlit",
                    unlit: material
                }
            });
        },
        setPbrMaterial(entity, material) {
            theComponent.createOrReplace(entity, {
                material: {
                    $case: "pbr",
                    pbr: material
                }
            });
        }
    };
}

// node_modules/@dcl/ecs/dist/components/extended/MeshCollider.js
function defineMeshColliderComponent(engine2) {
    const theComponent = MeshCollider(engine2);
    function getCollisionMask(layers) {
        if (Array.isArray(layers)) {
            return layers.map((item) => item).reduce((prev, item) => prev | item, 0);
        } else if (layers) {
            return layers;
        }
    }
    return {
        ...theComponent,
        setBox(entity, colliderLayers) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "box", box: {} },
                collisionMask: getCollisionMask(colliderLayers)
            });
        },
        setPlane(entity, colliderLayers) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "plane", plane: {} },
                collisionMask: getCollisionMask(colliderLayers)
            });
        },
        setCylinder(entity, radiusBottom, radiusTop, colliderLayers) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "cylinder", cylinder: { radiusBottom, radiusTop } },
                collisionMask: getCollisionMask(colliderLayers)
            });
        },
        setSphere(entity, colliderLayers) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "sphere", sphere: {} },
                collisionMask: getCollisionMask(colliderLayers)
            });
        }
    };
}

// node_modules/@dcl/ecs/dist/components/extended/MeshRenderer.js
function defineMeshRendererComponent(engine2) {
    const theComponent = MeshRenderer(engine2);
    return {
        ...theComponent,
        setBox(entity, uvs) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "box", box: { uvs: uvs || [] } }
            });
        },
        setPlane(entity, uvs) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "plane", plane: { uvs: uvs || [] } }
            });
        },
        setCylinder(entity, radiusBottom, radiusTop) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "cylinder", cylinder: { radiusBottom, radiusTop } }
            });
        },
        setSphere(entity) {
            theComponent.createOrReplace(entity, {
                mesh: { $case: "sphere", sphere: {} }
            });
        }
    };
}

// node_modules/@dcl/ecs/dist/components/manual/Transform.js
var TRANSFORM_LENGTH = 44;
var TransformSchema = {
    serialize(value, builder) {
        const ptr = builder.incrementWriteOffset(TRANSFORM_LENGTH);
        builder.setFloat32(ptr, value.position.x);
        builder.setFloat32(ptr + 4, value.position.y);
        builder.setFloat32(ptr + 8, value.position.z);
        builder.setFloat32(ptr + 12, value.rotation.x);
        builder.setFloat32(ptr + 16, value.rotation.y);
        builder.setFloat32(ptr + 20, value.rotation.z);
        builder.setFloat32(ptr + 24, value.rotation.w);
        builder.setFloat32(ptr + 28, value.scale.x);
        builder.setFloat32(ptr + 32, value.scale.y);
        builder.setFloat32(ptr + 36, value.scale.z);
        builder.setUint32(ptr + 40, value.parent || 0);
    },
    deserialize(reader) {
        const ptr = reader.incrementReadOffset(TRANSFORM_LENGTH);
        return {
            position: {
                x: reader.getFloat32(ptr),
                y: reader.getFloat32(ptr + 4),
                z: reader.getFloat32(ptr + 8)
            },
            rotation: {
                x: reader.getFloat32(ptr + 12),
                y: reader.getFloat32(ptr + 16),
                z: reader.getFloat32(ptr + 20),
                w: reader.getFloat32(ptr + 24)
            },
            scale: {
                x: reader.getFloat32(ptr + 28),
                y: reader.getFloat32(ptr + 32),
                z: reader.getFloat32(ptr + 36)
            },
            parent: reader.getUint32(ptr + 40)
        };
    },
    create() {
        return {
            position: { x: 0, y: 0, z: 0 },
            scale: { x: 1, y: 1, z: 1 },
            rotation: { x: 0, y: 0, z: 0, w: 1 },
            parent: 0
        };
    },
    extend(value) {
        return {
            position: { x: 0, y: 0, z: 0 },
            scale: { x: 1, y: 1, z: 1 },
            rotation: { x: 0, y: 0, z: 0, w: 1 },
            parent: 0,
            ...value
        };
    },
    jsonSchema: {
        type: "object",
        properties: {
            position: {
                type: "object",
                properties: {
                    x: { type: "number" },
                    y: { type: "number" },
                    z: { type: "number" }
                }
            },
            scale: {
                type: "object",
                properties: {
                    x: { type: "number" },
                    y: { type: "number" },
                    z: { type: "number" }
                }
            },
            rotation: {
                type: "object",
                properties: {
                    x: { type: "number" },
                    y: { type: "number" },
                    z: { type: "number" },
                    w: { type: "number" }
                }
            },
            parent: { type: "integer" }
        },
        serializationType: "transform"
    }
};
function defineTransformComponent(engine2) {
    const transformDef = engine2.defineComponentFromSchema("core::Transform", TransformSchema);
    return {
        ...transformDef,
        create(entity, val) {
            return transformDef.create(entity, TransformSchema.extend(val));
        },
        createOrReplace(entity, val) {
            return transformDef.createOrReplace(entity, TransformSchema.extend(val));
        }
    };
}

// node_modules/@dcl/ecs/dist/components/index.js
var Transform = (engine2) => defineTransformComponent(engine2);
var Material2 = (engine2) => defineMaterialComponent(engine2);
var MeshRenderer2 = (engine2) => defineMeshRendererComponent(engine2);
var MeshCollider2 = (engine2) => defineMeshColliderComponent(engine2);

// node_modules/@dcl/ecs/dist/components/generated/component-names.gen.js
var coreComponentMappings = {
    "core::Transform": 1,
    "core::Animator": 1042,
    "core::AudioSource": 1020,
    "core::AudioStream": 1021,
    "core::AvatarAttach": 1073,
    "core::AvatarModifierArea": 1070,
    "core::AvatarShape": 1080,
    "core::Billboard": 1090,
    "core::CameraMode": 1072,
    "core::CameraModeArea": 1071,
    "core::GltfContainer": 1041,
    "core::Material": 1017,
    "core::MeshCollider": 1019,
    "core::MeshRenderer": 1018,
    "core::NftShape": 1040,
    "core::PointerEvents": 1062,
    "core::PointerEventsResult": 1063,
    "core::PointerLock": 1074,
    "core::Raycast": 1067,
    "core::RaycastResult": 1068,
    "core::TextShape": 1030,
    "core::UiBackground": 1053,
    "core::UiDropdown": 1094,
    "core::UiDropdownResult": 1096,
    "core::UiInput": 1093,
    "core::UiInputResult": 1095,
    "core::UiText": 1052,
    "core::UiTransform": 1050,
    "core::VideoPlayer": 1043,
    "core::VisibilityComponent": 1081
};

// node_modules/@dcl/ecs/dist/components/component-number.js
var utf8 = __toESM(require_utf8());

// node_modules/@dcl/ecs/dist/runtime/crc.js
var CRC_TABLE = new Int32Array([
    0,
    1996959894,
    3993919788,
    2567524794,
    124634137,
    1886057615,
    3915621685,
    2657392035,
    249268274,
    2044508324,
    3772115230,
    2547177864,
    162941995,
    2125561021,
    3887607047,
    2428444049,
    498536548,
    1789927666,
    4089016648,
    2227061214,
    450548861,
    1843258603,
    4107580753,
    2211677639,
    325883990,
    1684777152,
    4251122042,
    2321926636,
    335633487,
    1661365465,
    4195302755,
    2366115317,
    997073096,
    1281953886,
    3579855332,
    2724688242,
    1006888145,
    1258607687,
    3524101629,
    2768942443,
    901097722,
    1119000684,
    3686517206,
    2898065728,
    853044451,
    1172266101,
    3705015759,
    2882616665,
    651767980,
    1373503546,
    3369554304,
    3218104598,
    565507253,
    1454621731,
    3485111705,
    3099436303,
    671266974,
    1594198024,
    3322730930,
    2970347812,
    795835527,
    1483230225,
    3244367275,
    3060149565,
    1994146192,
    31158534,
    2563907772,
    4023717930,
    1907459465,
    112637215,
    2680153253,
    3904427059,
    2013776290,
    251722036,
    2517215374,
    3775830040,
    2137656763,
    141376813,
    2439277719,
    3865271297,
    1802195444,
    476864866,
    2238001368,
    4066508878,
    1812370925,
    453092731,
    2181625025,
    4111451223,
    1706088902,
    314042704,
    2344532202,
    4240017532,
    1658658271,
    366619977,
    2362670323,
    4224994405,
    1303535960,
    984961486,
    2747007092,
    3569037538,
    1256170817,
    1037604311,
    2765210733,
    3554079995,
    1131014506,
    879679996,
    2909243462,
    3663771856,
    1141124467,
    855842277,
    2852801631,
    3708648649,
    1342533948,
    654459306,
    3188396048,
    3373015174,
    1466479909,
    544179635,
    3110523913,
    3462522015,
    1591671054,
    702138776,
    2966460450,
    3352799412,
    1504918807,
    783551873,
    3082640443,
    3233442989,
    3988292384,
    2596254646,
    62317068,
    1957810842,
    3939845945,
    2647816111,
    81470997,
    1943803523,
    3814918930,
    2489596804,
    225274430,
    2053790376,
    3826175755,
    2466906013,
    167816743,
    2097651377,
    4027552580,
    2265490386,
    503444072,
    1762050814,
    4150417245,
    2154129355,
    426522225,
    1852507879,
    4275313526,
    2312317920,
    282753626,
    1742555852,
    4189708143,
    2394877945,
    397917763,
    1622183637,
    3604390888,
    2714866558,
    953729732,
    1340076626,
    3518719985,
    2797360999,
    1068828381,
    1219638859,
    3624741850,
    2936675148,
    906185462,
    1090812512,
    3747672003,
    2825379669,
    829329135,
    1181335161,
    3412177804,
    3160834842,
    628085408,
    1382605366,
    3423369109,
    3138078467,
    570562233,
    1426400815,
    3317316542,
    2998733608,
    733239954,
    1555261956,
    3268935591,
    3050360625,
    752459403,
    1541320221,
    2607071920,
    3965973030,
    1969922972,
    40735498,
    2617837225,
    3943577151,
    1913087877,
    83908371,
    2512341634,
    3803740692,
    2075208622,
    213261112,
    2463272603,
    3855990285,
    2094854071,
    198958881,
    2262029012,
    4057260610,
    1759359992,
    534414190,
    2176718541,
    4139329115,
    1873836001,
    414664567,
    2282248934,
    4279200368,
    1711684554,
    285281116,
    2405801727,
    4167216745,
    1634467795,
    376229701,
    2685067896,
    3608007406,
    1308918612,
    956543938,
    2808555105,
    3495958263,
    1231636301,
    1047427035,
    2932959818,
    3654703836,
    1088359270,
    936918e3,
    2847714899,
    3736837829,
    1202900863,
    817233897,
    3183342108,
    3401237130,
    1404277552,
    615818150,
    3134207493,
    3453421203,
    1423857449,
    601450431,
    3009837614,
    3294710456,
    1567103746,
    711928724,
    3020668471,
    3272380065,
    1510334235,
    755167117
]);
function _crc32(buf, previous) {
    let crc = ~~previous ^ -1;
    for (let n = 0; n < buf.length; n++) {
        crc = CRC_TABLE[(crc ^ buf[n]) & 255] ^ crc >>> 8;
    }
    return crc ^ -1;
}
function unsignedCRC32(data, prev = 0) {
    return _crc32(data, prev) >>> 0;
}

// node_modules/@dcl/ecs/dist/components/component-number.js
var MAX_STATIC_COMPONENT = 1 << 11;
function componentNumberFromName(componentName) {
    if (coreComponentMappings[componentName])
        return coreComponentMappings[componentName];
    const bytes = new Uint8Array(128);
    utf8.write(componentName, bytes, 0);
    return (unsignedCRC32(bytes) + MAX_STATIC_COMPONENT & 4294967295) >>> 0;
}

// node_modules/@dcl/ecs/dist/runtime/invariant.js
var __DEV__ = true;
function checkNotThenable(t, error) {
    if (__DEV__) {
        if (t && typeof t === "object" && typeof t.then === "function") {
            throw new Error(error);
        }
    }
    return t;
}

// node_modules/@dcl/ecs/dist/schemas/Array.js
var IArray = (type) => {
    return {
        serialize(value, builder) {
            builder.writeUint32(value.length);
            for (const item of value) {
                type.serialize(item, builder);
            }
        },
        deserialize(reader) {
            const newArray = [];
            const length2 = reader.readUint32();
            for (let index = 0; index < length2; index++) {
                newArray.push(type.deserialize(reader));
            }
            return newArray;
        },
        create() {
            return [];
        },
        jsonSchema: {
            type: "array",
            items: type.jsonSchema,
            serializationType: "array"
        }
    };
};

// node_modules/@dcl/ecs/dist/schemas/basic/Boolean.js
var Bool = {
    serialize(value, builder) {
        builder.writeInt8(value ? 1 : 0);
    },
    deserialize(reader) {
        return reader.readInt8() === 1;
    },
    create() {
        return false;
    },
    jsonSchema: {
        type: "boolean",
        serializationType: "boolean"
    }
};

// node_modules/@dcl/ecs/dist/schemas/basic/Integer.js
var Int64 = {
    serialize(value, builder) {
        builder.writeInt64(BigInt(value));
    },
    deserialize(reader) {
        return Number(reader.readInt64());
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "integer",
        serializationType: "int64"
    }
};
var Int32 = {
    serialize(value, builder) {
        builder.writeInt32(value);
    },
    deserialize(reader) {
        return reader.readInt32();
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "integer",
        serializationType: "int32"
    }
};
var Int16 = {
    serialize(value, builder) {
        builder.writeInt16(value);
    },
    deserialize(reader) {
        return reader.readInt16();
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "integer",
        serializationType: "int16"
    }
};
var Int8 = {
    serialize(value, builder) {
        builder.writeInt8(value);
    },
    deserialize(reader) {
        return reader.readInt8();
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "integer",
        serializationType: "int8"
    }
};

// node_modules/@dcl/ecs/dist/schemas/basic/String.js
var FlatString = {
    serialize(value, builder) {
        builder.writeUtf8String(value);
    },
    deserialize(reader) {
        return reader.readUtf8String();
    },
    create() {
        return "";
    },
    jsonSchema: {
        type: "string",
        serializationType: "utf8-string"
    }
};
var EcsString = FlatString;

// node_modules/@dcl/ecs/dist/schemas/basic/Enum.js
function validateMemberValuesAreNumbersAndInRangeInt32(enumValue) {
    const MIN_VALUE = -(2 ** 31), MAX_VALUE = 2 ** 31 - 1;
    let valueCount = 0, totalCount = 0;
    for (const key in enumValue) {
        if (typeof enumValue[key] === "number") {
            if (enumValue[key] > MAX_VALUE || enumValue[key] < MIN_VALUE) {
                throw new Error(`Enum member values must be numbers within the range of ${MIN_VALUE} to ${MAX_VALUE}.`);
            }
            valueCount++;
        }
        totalCount++;
    }
    if (totalCount !== valueCount * 2) {
        throw new Error("All enum member values must be of numeric type.");
    }
}
function validateMemberValuesAreStrings(enumValue) {
    for (const key in enumValue) {
        if (typeof enumValue[key] !== "string") {
            throw new Error("All enum member values must be of string type.");
        }
    }
}
var IntEnumReflectionType = "enum-int";
var IntEnum = (enumObject, defaultValue) => {
    validateMemberValuesAreNumbersAndInRangeInt32(enumObject);
    return {
        serialize(value, builder) {
            Int32.serialize(value, builder);
        },
        deserialize(reader) {
            return Int32.deserialize(reader);
        },
        create() {
            return defaultValue;
        },
        jsonSchema: {
            // JSON-schema
            type: "integer",
            enum: Object.values(enumObject).filter((item) => Number.isInteger(item)),
            default: defaultValue,
            // @dcl/ecs Schema Spec
            serializationType: IntEnumReflectionType,
            enumObject
        }
    };
};
var StringEnumReflectionType = "enum-string";
var StringEnum = (enumObject, defaultValue) => {
    validateMemberValuesAreStrings(enumObject);
    return {
        serialize(value, builder) {
            FlatString.serialize(value, builder);
        },
        deserialize(reader) {
            return FlatString.deserialize(reader);
        },
        create() {
            return defaultValue;
        },
        jsonSchema: {
            // JSON-schema
            type: "string",
            enum: Object.values(enumObject),
            default: defaultValue,
            // @dcl/ecs Schema Spec
            serializationType: StringEnumReflectionType,
            enumObject
        }
    };
};

// node_modules/@dcl/ecs/dist/schemas/basic/Float.js
var Float32 = {
    serialize(value, builder) {
        builder.writeFloat32(value);
    },
    deserialize(reader) {
        return reader.readFloat32();
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "number",
        serializationType: "float32"
    }
};
var Float64 = {
    serialize(value, builder) {
        builder.writeFloat64(value);
    },
    deserialize(reader) {
        return reader.readFloat64();
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "number",
        serializationType: "float64"
    }
};

// node_modules/@dcl/ecs/dist/schemas/custom/Color3.js
var Color3Schema = {
    serialize(value, builder) {
        builder.writeFloat32(value.r);
        builder.writeFloat32(value.g);
        builder.writeFloat32(value.b);
    },
    deserialize(reader) {
        return {
            r: reader.readFloat32(),
            g: reader.readFloat32(),
            b: reader.readFloat32()
        };
    },
    create() {
        return { r: 0, g: 0, b: 0 };
    },
    jsonSchema: {
        type: "object",
        properties: {
            r: { type: "number" },
            g: { type: "number" },
            b: { type: "number" }
        },
        serializationType: "color3"
    }
};

// node_modules/@dcl/ecs/dist/schemas/custom/Color4.js
var Color4Schema = {
    serialize(value, builder) {
        builder.writeFloat32(value.r);
        builder.writeFloat32(value.g);
        builder.writeFloat32(value.b);
        builder.writeFloat32(value.a);
    },
    deserialize(reader) {
        return {
            r: reader.readFloat32(),
            g: reader.readFloat32(),
            b: reader.readFloat32(),
            a: reader.readFloat32()
        };
    },
    create() {
        return { r: 0, g: 0, b: 0, a: 0 };
    },
    jsonSchema: {
        type: "object",
        properties: {
            r: { type: "number" },
            g: { type: "number" },
            b: { type: "number" },
            a: { type: "number" }
        },
        serializationType: "color4"
    }
};

// node_modules/@dcl/ecs/dist/schemas/custom/Entity.js
var EntitySchema = {
    serialize(value, builder) {
        builder.writeInt32(value);
    },
    deserialize(reader) {
        return reader.readInt32();
    },
    create() {
        return 0;
    },
    jsonSchema: {
        type: "integer",
        serializationType: "entity"
    }
};

// node_modules/@dcl/ecs/dist/schemas/custom/Quaternion.js
var QuaternionSchema = {
    serialize(value, builder) {
        builder.writeFloat32(value.x);
        builder.writeFloat32(value.y);
        builder.writeFloat32(value.z);
        builder.writeFloat32(value.w);
    },
    deserialize(reader) {
        return {
            x: reader.readFloat32(),
            y: reader.readFloat32(),
            z: reader.readFloat32(),
            w: reader.readFloat32()
        };
    },
    create() {
        return { x: 0, y: 0, z: 0, w: 0 };
    },
    jsonSchema: {
        type: "object",
        properties: {
            x: { type: "number" },
            y: { type: "number" },
            z: { type: "number" },
            w: { type: "number" }
        },
        serializationType: "quaternion"
    }
};

// node_modules/@dcl/ecs/dist/schemas/custom/Vector3.js
var Vector3Schema = {
    serialize(value, builder) {
        builder.writeFloat32(value.x);
        builder.writeFloat32(value.y);
        builder.writeFloat32(value.z);
    },
    deserialize(reader) {
        return {
            x: reader.readFloat32(),
            y: reader.readFloat32(),
            z: reader.readFloat32()
        };
    },
    create() {
        return { x: 0, y: 0, z: 0 };
    },
    jsonSchema: {
        type: "object",
        properties: {
            x: { type: "number" },
            y: { type: "number" },
            z: { type: "number" },
            w: { type: "number" }
        },
        serializationType: "vector3"
    }
};

// node_modules/@dcl/ecs/dist/schemas/Map.js
var IMap = (spec, defaultValue) => {
    const specReflection = Object.keys(spec).reduce((specReflection2, currentKey) => {
        specReflection2[currentKey] = spec[currentKey].jsonSchema;
        return specReflection2;
    }, {});
    return {
        serialize(value, builder) {
            for (const key in spec) {
                spec[key].serialize(value[key], builder);
            }
        },
        deserialize(reader) {
            const newValue = {};
            for (const key in spec) {
                ;
                newValue[key] = spec[key].deserialize(reader);
            }
            return newValue;
        },
        create() {
            const newValue = {};
            for (const key in spec) {
                ;
                newValue[key] = spec[key].create();
            }
            return { ...newValue, ...defaultValue };
        },
        extend: (base) => {
            const newValue = {};
            for (const key in spec) {
                ;
                newValue[key] = spec[key].create();
            }
            return { ...newValue, ...defaultValue, ...base };
        },
        jsonSchema: {
            type: "object",
            properties: specReflection,
            serializationType: "map"
        }
    };
};

// node_modules/@dcl/ecs/dist/schemas/Optional.js
var IOptional = (spec) => {
    return {
        serialize(value, builder) {
            if (value) {
                builder.writeInt8(1);
                spec.serialize(value, builder);
            } else {
                builder.writeInt8(0);
            }
        },
        deserialize(reader) {
            const exists = reader.readInt8();
            if (exists) {
                return spec.deserialize(reader);
            }
        },
        create() {
            return void 0;
        },
        jsonSchema: {
            type: spec.jsonSchema.type,
            serializationType: "optional",
            optionalJsonSchema: spec.jsonSchema
        }
    };
};

// node_modules/@dcl/ecs/dist/schemas/buildSchema.js
var primitiveSchemas = {
    [Bool.jsonSchema.serializationType]: Bool,
    [EcsString.jsonSchema.serializationType]: EcsString,
    [Float32.jsonSchema.serializationType]: Float32,
    [Float64.jsonSchema.serializationType]: Float64,
    [Int8.jsonSchema.serializationType]: Int8,
    [Int16.jsonSchema.serializationType]: Int16,
    [Int32.jsonSchema.serializationType]: Int32,
    [Int64.jsonSchema.serializationType]: Int64,
    [Vector3Schema.jsonSchema.serializationType]: Vector3Schema,
    [QuaternionSchema.jsonSchema.serializationType]: QuaternionSchema,
    [Color3Schema.jsonSchema.serializationType]: Color3Schema,
    [Color4Schema.jsonSchema.serializationType]: Color4Schema,
    [EntitySchema.jsonSchema.serializationType]: EntitySchema
};
function jsonSchemaToSchema(jsonSchema) {
    if (primitiveSchemas[jsonSchema.serializationType]) {
        return primitiveSchemas[jsonSchema.serializationType];
    }
    if (jsonSchema.serializationType === "map") {
        const mapJsonSchema = jsonSchema;
        const spec = {};
        for (const key in mapJsonSchema.properties) {
            spec[key] = jsonSchemaToSchema(mapJsonSchema.properties[key]);
        }
        return IMap(spec);
    }
    if (jsonSchema.serializationType === "optional") {
        const withItemsJsonSchema = jsonSchema;
        return IOptional(jsonSchemaToSchema(withItemsJsonSchema.optionalJsonSchema));
    }
    if (jsonSchema.serializationType === "array") {
        const withItemsJsonSchema = jsonSchema;
        return IArray(jsonSchemaToSchema(withItemsJsonSchema.items));
    }
    if (jsonSchema.serializationType === "enum-int") {
        const enumJsonSchema = jsonSchema;
        return IntEnum(enumJsonSchema.enumObject, enumJsonSchema.default);
    }
    if (jsonSchema.serializationType === "enum-string") {
        const enumJsonSchema = jsonSchema;
        return StringEnum(enumJsonSchema.enumObject, enumJsonSchema.default);
    }
    throw new Error(`${jsonSchema.serializationType} is not supported as reverse schema generation.`);
}
function mutateValues(jsonSchema, value, mutateFn) {
    if (jsonSchema.serializationType === "map") {
        const mapJsonSchema = jsonSchema;
        const mapValue = value;
        for (const key in mapJsonSchema.properties) {
            const valueType = mapJsonSchema.properties[key];
            if (valueType.serializationType === "array" || valueType.serializationType === "map") {
                mutateValues(mapJsonSchema.properties[key], mapValue[key], mutateFn);
            } else {
                const newValue = mutateFn(mapValue[key], valueType);
                if (newValue.changed) {
                    mapValue[key] = newValue.value;
                }
            }
        }
    } else if (jsonSchema.serializationType === "array") {
        const withItemsJsonSchema = jsonSchema;
        const arrayValue = value;
        const nestedMutateValues = withItemsJsonSchema.items.serializationType === "array" || withItemsJsonSchema.items.serializationType === "map";
        for (let i = 0, n = arrayValue.length; i < n; i++) {
            if (nestedMutateValues) {
                mutateValues(withItemsJsonSchema.items, arrayValue[i], mutateFn);
            } else {
                const newValue = mutateFn(arrayValue[i], withItemsJsonSchema.items);
                if (newValue.changed) {
                    arrayValue[i] = newValue.value;
                }
            }
        }
    }
}

// node_modules/@dcl/ecs/dist/schemas/index.js
var Schemas;
(function(Schemas2) {
    Schemas2.Boolean = Bool;
    Schemas2.String = EcsString;
    Schemas2.Float = Float32;
    Schemas2.Double = Float64;
    Schemas2.Byte = Int8;
    Schemas2.Short = Int16;
    Schemas2.Int = Int32;
    Schemas2.Int64 = Int64;
    Schemas2.Number = Float32;
    Schemas2.Vector3 = Vector3Schema;
    Schemas2.Quaternion = QuaternionSchema;
    Schemas2.Color3 = Color3Schema;
    Schemas2.Color4 = Color4Schema;
    Schemas2.Entity = EntitySchema;
    Schemas2.EnumNumber = IntEnum;
    Schemas2.EnumString = StringEnum;
    Schemas2.Array = IArray;
    Schemas2.Map = IMap;
    Schemas2.Optional = IOptional;
    Schemas2.fromJson = jsonSchemaToSchema;
    Schemas2.mutateNestedValues = mutateValues;
})(Schemas || (Schemas = {}));

// node_modules/@dcl/ecs/dist/systems/crdt/gset.js
function createVersionGSet() {
    const lastVersion = /* @__PURE__ */ new Map();
    return {
        /**
         *
         * @param number
         * @param version
         * @returns
         */
        addTo(number, version) {
            if (version < 0) {
                return false;
            }
            const currentValue = lastVersion.get(number);
            if (currentValue !== void 0 && currentValue >= version) {
                return true;
            }
            lastVersion.set(number, version);
            return true;
        },
        /**
         * @returns the set with [number, version] of each value
         */
        has(n, v) {
            const currentValue = lastVersion.get(n);
            if (currentValue !== void 0 && currentValue >= v) {
                return true;
            }
            return false;
        },
        /**
         * Warning: this function returns the reference to the internal map,
         *  if you need to mutate some value, make a copy.
         * For optimization purpose the copy isn't made here.
         *
         * @returns the map of number to version
         */
        getMap() {
            return lastVersion;
        }
    };
}

// node_modules/@dcl/ecs/dist/engine/entity.js
var MAX_U16 = 65535;
var MASK_UPPER_16_ON_32 = 4294901760;
var AMOUNT_VERSION_AVAILABLE = MAX_U16 + 1;
var RESERVED_STATIC_ENTITIES = 512;
var MAX_ENTITY_NUMBER = MAX_U16;
var EntityUtils;
(function(EntityUtils2) {
    function fromEntityId(entityId) {
        return [(entityId & MAX_U16) >>> 0, ((entityId & MASK_UPPER_16_ON_32) >> 16 & MAX_U16) >>> 0];
    }
    EntityUtils2.fromEntityId = fromEntityId;
    function toEntityId(entityNumber, entityVersion) {
        return (entityNumber & MAX_U16 | (entityVersion & MAX_U16) << 16) >>> 0;
    }
    EntityUtils2.toEntityId = toEntityId;
})(EntityUtils || (EntityUtils = {}));
var EntityState;
(function(EntityState2) {
    EntityState2[EntityState2["Unknown"] = 0] = "Unknown";
    EntityState2[EntityState2["UsedEntity"] = 1] = "UsedEntity";
    EntityState2[EntityState2["Removed"] = 2] = "Removed";
    EntityState2[EntityState2["Reserved"] = 3] = "Reserved";
})(EntityState || (EntityState = {}));
function EntityContainer() {
    let entityCounter = RESERVED_STATIC_ENTITIES;
    const usedEntities = /* @__PURE__ */ new Set();
    let toRemoveEntities = [];
    const removedEntities = createVersionGSet();
    function generateNewEntity() {
        if (entityCounter > MAX_ENTITY_NUMBER - 1) {
            throw new Error(`It fails trying to generate an entity out of range ${MAX_ENTITY_NUMBER}.`);
        }
        const entityNumber = entityCounter++;
        const entityVersion = removedEntities.getMap().has(entityNumber) ? removedEntities.getMap().get(entityNumber) + 1 : 0;
        const entity = EntityUtils.toEntityId(entityNumber, entityVersion);
        if (usedEntities.has(entity)) {
            return generateNewEntity();
        }
        usedEntities.add(entity);
        return entity;
    }
    function generateEntity() {
        if (usedEntities.size + RESERVED_STATIC_ENTITIES >= entityCounter) {
            return generateNewEntity();
        }
        for (const [number, version] of removedEntities.getMap()) {
            if (version < MAX_U16) {
                const entity = EntityUtils.toEntityId(number, version + 1);
                if (!usedEntities.has(entity) && !toRemoveEntities.includes(entity)) {
                    usedEntities.add(entity);
                    return entity;
                }
            }
        }
        return generateNewEntity();
    }
    function removeEntity(entity) {
        if (entity < RESERVED_STATIC_ENTITIES)
            return false;
        if (usedEntities.has(entity)) {
            usedEntities.delete(entity);
            toRemoveEntities.push(entity);
        } else {
            updateRemovedEntity(entity);
        }
        return true;
    }
    function releaseRemovedEntities() {
        const arr = toRemoveEntities;
        if (arr.length) {
            toRemoveEntities = [];
            for (const entity of arr) {
                const [n, v] = EntityUtils.fromEntityId(entity);
                removedEntities.addTo(n, v);
            }
        }
        return arr;
    }
    function updateRemovedEntity(entity) {
        const [n, v] = EntityUtils.fromEntityId(entity);
        removedEntities.addTo(n, v);
        for (let i = 0; i <= v; i++) {
            usedEntities.delete(EntityUtils.toEntityId(n, i));
        }
        return true;
    }
    function updateUsedEntity(entity) {
        const [n, v] = EntityUtils.fromEntityId(entity);
        if (removedEntities.has(n, v))
            return false;
        if (v > 0) {
            for (let i = 0; i <= v - 1; i++) {
                usedEntities.delete(EntityUtils.toEntityId(n, i));
            }
            removedEntities.addTo(n, v - 1);
        }
        usedEntities.add(entity);
        return true;
    }
    function getEntityState(entity) {
        const [n, v] = EntityUtils.fromEntityId(entity);
        if (n < RESERVED_STATIC_ENTITIES) {
            return EntityState.Reserved;
        }
        if (usedEntities.has(entity)) {
            return EntityState.UsedEntity;
        }
        const removedVersion = removedEntities.getMap().get(n);
        if (removedVersion !== void 0 && removedVersion >= v) {
            return EntityState.Removed;
        }
        return EntityState.Unknown;
    }
    return {
        generateEntity,
        removeEntity,
        getExistingEntities() {
            return new Set(usedEntities);
        },
        getEntityState,
        releaseRemovedEntities,
        updateRemovedEntity,
        updateUsedEntity
    };
}

// node_modules/@dcl/ecs/dist/serialization/ByteBuffer/index.js
var utf82 = __toESM(require_utf8());
var __classPrivateFieldGet = function(receiver, state, kind, f) {
    if (kind === "a" && !f)
        throw new TypeError("Private accessor was defined without a getter");
    if (typeof state === "function" ? receiver !== state || !f : !state.has(receiver))
        throw new TypeError("Cannot read private member from an object whose class did not declare it");
    return kind === "m" ? f : kind === "a" ? f.call(receiver) : f ? f.value : state.get(receiver);
};
var _ReadWriteByteBuffer_instances;
var _ReadWriteByteBuffer_woAdd;
var _ReadWriteByteBuffer_roAdd;
function getNextSize(currentSize, intendedSize) {
    const minNewSize = Math.max(currentSize, intendedSize) + 1024;
    return Math.ceil(minNewSize / 1024) * 1024;
}
var defaultInitialCapacity = 10240;
var ReadWriteByteBuffer = class {
    /**
     * @param buffer - The initial buffer, provide a buffer if you need to set "initial capacity"
     * @param readingOffset - Set the cursor where begins to read. Default 0
     * @param writingOffset - Set the cursor to not start writing from the begin of it. Defaults to the buffer size
     */
    constructor(buffer, readingOffset, writingOffset) {
        _ReadWriteByteBuffer_instances.add(this);
        this._buffer = buffer || new Uint8Array(defaultInitialCapacity);
        this.view = new DataView(this._buffer.buffer, this._buffer.byteOffset);
        this.woffset = writingOffset ?? (buffer ? this._buffer.length : null) ?? 0;
        this.roffset = readingOffset ?? 0;
    }
    buffer() {
        return this._buffer;
    }
    bufferLength() {
        return this._buffer.length;
    }
    resetBuffer() {
        this.roffset = 0;
        this.woffset = 0;
    }
    currentReadOffset() {
        return this.roffset;
    }
    currentWriteOffset() {
        return this.woffset;
    }
    incrementReadOffset(amount) {
        return __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, amount);
    }
    remainingBytes() {
        return this.woffset - this.roffset;
    }
    readFloat32() {
        return this.view.getFloat32(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 4), true);
    }
    readFloat64() {
        return this.view.getFloat64(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 8), true);
    }
    readInt8() {
        return this.view.getInt8(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 1));
    }
    readInt16() {
        return this.view.getInt16(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 2), true);
    }
    readInt32() {
        return this.view.getInt32(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 4), true);
    }
    readInt64() {
        return this.view.getBigInt64(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 8), true);
    }
    readUint8() {
        return this.view.getUint8(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 1));
    }
    readUint16() {
        return this.view.getUint16(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 2), true);
    }
    readUint32() {
        return this.view.getUint32(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 4), true);
    }
    readUint64() {
        return this.view.getBigUint64(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 8), true);
    }
    readBuffer() {
        const length2 = this.view.getUint32(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 4), true);
        return this._buffer.subarray(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, length2), __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 0));
    }
    readUtf8String() {
        const length2 = this.view.getUint32(__classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 4), true);
        return utf82.read(this._buffer, __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, length2), __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_roAdd).call(this, 0));
    }
    incrementWriteOffset(amount) {
        return __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, amount);
    }
    toBinary() {
        return this._buffer.subarray(0, this.woffset);
    }
    toCopiedBinary() {
        return new Uint8Array(this.toBinary());
    }
    writeBuffer(value, writeLength = true) {
        if (writeLength) {
            this.writeUint32(value.byteLength);
        }
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, value.byteLength);
        this._buffer.set(value, o);
    }
    writeUtf8String(value, writeLength = true) {
        const byteLength = utf82.length(value);
        if (writeLength) {
            this.writeUint32(byteLength);
        }
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, byteLength);
        utf82.write(value, this._buffer, o);
    }
    writeFloat32(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 4);
        this.view.setFloat32(o, value, true);
    }
    writeFloat64(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 8);
        this.view.setFloat64(o, value, true);
    }
    writeInt8(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 1);
        this.view.setInt8(o, value);
    }
    writeInt16(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 2);
        this.view.setInt16(o, value, true);
    }
    writeInt32(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 4);
        this.view.setInt32(o, value, true);
    }
    writeInt64(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 8);
        this.view.setBigInt64(o, value, true);
    }
    writeUint8(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 1);
        this.view.setUint8(o, value);
    }
    writeUint16(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 2);
        this.view.setUint16(o, value, true);
    }
    writeUint32(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 4);
        this.view.setUint32(o, value, true);
    }
    writeUint64(value) {
        const o = __classPrivateFieldGet(this, _ReadWriteByteBuffer_instances, "m", _ReadWriteByteBuffer_woAdd).call(this, 8);
        this.view.setBigUint64(o, value, true);
    }
    // DataView Proxy
    getFloat32(offset) {
        return this.view.getFloat32(offset, true);
    }
    getFloat64(offset) {
        return this.view.getFloat64(offset, true);
    }
    getInt8(offset) {
        return this.view.getInt8(offset);
    }
    getInt16(offset) {
        return this.view.getInt16(offset, true);
    }
    getInt32(offset) {
        return this.view.getInt32(offset, true);
    }
    getInt64(offset) {
        return this.view.getBigInt64(offset, true);
    }
    getUint8(offset) {
        return this.view.getUint8(offset);
    }
    getUint16(offset) {
        return this.view.getUint16(offset, true);
    }
    getUint32(offset) {
        return this.view.getUint32(offset, true);
    }
    getUint64(offset) {
        return this.view.getBigUint64(offset, true);
    }
    setFloat32(offset, value) {
        this.view.setFloat32(offset, value, true);
    }
    setFloat64(offset, value) {
        this.view.setFloat64(offset, value, true);
    }
    setInt8(offset, value) {
        this.view.setInt8(offset, value);
    }
    setInt16(offset, value) {
        this.view.setInt16(offset, value, true);
    }
    setInt32(offset, value) {
        this.view.setInt32(offset, value, true);
    }
    setInt64(offset, value) {
        this.view.setBigInt64(offset, value, true);
    }
    setUint8(offset, value) {
        this.view.setUint8(offset, value);
    }
    setUint16(offset, value) {
        this.view.setUint16(offset, value, true);
    }
    setUint32(offset, value) {
        this.view.setUint32(offset, value, true);
    }
    setUint64(offset, value) {
        this.view.setBigUint64(offset, value, true);
    }
};
_ReadWriteByteBuffer_instances = /* @__PURE__ */ new WeakSet(), _ReadWriteByteBuffer_woAdd = function _ReadWriteByteBuffer_woAdd2(amount) {
    if (this.woffset + amount > this._buffer.byteLength) {
        const newsize = getNextSize(this._buffer.byteLength, this.woffset + amount);
        const newBuffer = new Uint8Array(newsize);
        newBuffer.set(this._buffer);
        const oldOffset = this._buffer.byteOffset;
        this._buffer = newBuffer;
        this.view = new DataView(this._buffer.buffer, oldOffset);
    }
    this.woffset += amount;
    return this.woffset - amount;
}, _ReadWriteByteBuffer_roAdd = function _ReadWriteByteBuffer_roAdd2(amount) {
    if (this.roffset + amount > this.woffset) {
        throw new Error("Outside of the bounds of writen data.");
    }
    this.roffset += amount;
    return this.roffset - amount;
};

// node_modules/@dcl/ecs/dist/serialization/crdt/types.js
var CrdtMessageType;
(function(CrdtMessageType2) {
    CrdtMessageType2[CrdtMessageType2["RESERVED"] = 0] = "RESERVED";
    CrdtMessageType2[CrdtMessageType2["PUT_COMPONENT"] = 1] = "PUT_COMPONENT";
    CrdtMessageType2[CrdtMessageType2["DELETE_COMPONENT"] = 2] = "DELETE_COMPONENT";
    CrdtMessageType2[CrdtMessageType2["DELETE_ENTITY"] = 3] = "DELETE_ENTITY";
    CrdtMessageType2[CrdtMessageType2["APPEND_VALUE"] = 4] = "APPEND_VALUE";
    CrdtMessageType2[CrdtMessageType2["MAX_MESSAGE_TYPE"] = 5] = "MAX_MESSAGE_TYPE";
})(CrdtMessageType || (CrdtMessageType = {}));
var CRDT_MESSAGE_HEADER_LENGTH = 8;
var ProcessMessageResultType;
(function(ProcessMessageResultType2) {
    ProcessMessageResultType2[ProcessMessageResultType2["StateUpdatedTimestamp"] = 1] = "StateUpdatedTimestamp";
    ProcessMessageResultType2[ProcessMessageResultType2["StateOutdatedTimestamp"] = 2] = "StateOutdatedTimestamp";
    ProcessMessageResultType2[ProcessMessageResultType2["NoChanges"] = 3] = "NoChanges";
    ProcessMessageResultType2[ProcessMessageResultType2["StateOutdatedData"] = 4] = "StateOutdatedData";
    ProcessMessageResultType2[ProcessMessageResultType2["StateUpdatedData"] = 5] = "StateUpdatedData";
    ProcessMessageResultType2[ProcessMessageResultType2["EntityWasDeleted"] = 6] = "EntityWasDeleted";
    ProcessMessageResultType2[ProcessMessageResultType2["EntityDeleted"] = 7] = "EntityDeleted";
})(ProcessMessageResultType || (ProcessMessageResultType = {}));

// node_modules/@dcl/ecs/dist/serialization/crdt/crdtMessageProtocol.js
var CrdtMessageProtocol;
(function(CrdtMessageProtocol2) {
    function validate(buf) {
        const rem = buf.remainingBytes();
        if (rem < CRDT_MESSAGE_HEADER_LENGTH) {
            return false;
        }
        const messageLength = buf.getUint32(buf.currentReadOffset());
        if (rem < messageLength) {
            return false;
        }
        return true;
    }
    CrdtMessageProtocol2.validate = validate;
    function readHeader(buf) {
        if (!validate(buf)) {
            return null;
        }
        return {
            length: buf.readUint32(),
            type: buf.readUint32()
        };
    }
    CrdtMessageProtocol2.readHeader = readHeader;
    function getHeader(buf) {
        if (!validate(buf)) {
            return null;
        }
        const currentOffset = buf.currentReadOffset();
        return {
            length: buf.getUint32(currentOffset),
            type: buf.getUint32(currentOffset + 4)
        };
    }
    CrdtMessageProtocol2.getHeader = getHeader;
    function consumeMessage(buf) {
        const header = getHeader(buf);
        if (!header) {
            return false;
        }
        buf.incrementReadOffset(header.length);
        return true;
    }
    CrdtMessageProtocol2.consumeMessage = consumeMessage;
})(CrdtMessageProtocol || (CrdtMessageProtocol = {}));

// node_modules/@dcl/ecs/dist/serialization/crdt/deleteComponent.js
var DeleteComponent;
(function(DeleteComponent2) {
    DeleteComponent2.MESSAGE_HEADER_LENGTH = 12;
    function write3(entity, componentId, timestamp, buf) {
        const messageLength = CRDT_MESSAGE_HEADER_LENGTH + DeleteComponent2.MESSAGE_HEADER_LENGTH;
        const startMessageOffset = buf.incrementWriteOffset(messageLength);
        buf.setUint32(startMessageOffset, messageLength);
        buf.setUint32(startMessageOffset + 4, CrdtMessageType.DELETE_COMPONENT);
        buf.setUint32(startMessageOffset + 8, entity);
        buf.setUint32(startMessageOffset + 12, componentId);
        buf.setUint32(startMessageOffset + 16, timestamp);
    }
    DeleteComponent2.write = write3;
    function read2(buf) {
        const header = CrdtMessageProtocol.readHeader(buf);
        if (!header) {
            return null;
        }
        if (header.type !== CrdtMessageType.DELETE_COMPONENT) {
            throw new Error("DeleteComponentOperation tried to read another message type.");
        }
        const msg = {
            ...header,
            entityId: buf.readUint32(),
            componentId: buf.readUint32(),
            timestamp: buf.readUint32()
        };
        return msg;
    }
    DeleteComponent2.read = read2;
})(DeleteComponent || (DeleteComponent = {}));

// node_modules/@dcl/ecs/dist/serialization/crdt/appendValue.js
var AppendValueOperation;
(function(AppendValueOperation2) {
    AppendValueOperation2.MESSAGE_HEADER_LENGTH = 16;
    function write3(entity, timestamp, componentId, data, buf) {
        const startMessageOffset = buf.incrementWriteOffset(CRDT_MESSAGE_HEADER_LENGTH + AppendValueOperation2.MESSAGE_HEADER_LENGTH);
        buf.writeBuffer(data, false);
        const messageLength = buf.currentWriteOffset() - startMessageOffset;
        buf.setUint32(startMessageOffset, messageLength);
        buf.setUint32(startMessageOffset + 4, CrdtMessageType.APPEND_VALUE);
        buf.setUint32(startMessageOffset + 8, entity);
        buf.setUint32(startMessageOffset + 12, componentId);
        buf.setUint32(startMessageOffset + 16, timestamp);
        const newLocal = messageLength - AppendValueOperation2.MESSAGE_HEADER_LENGTH - CRDT_MESSAGE_HEADER_LENGTH;
        buf.setUint32(startMessageOffset + 20, newLocal);
    }
    AppendValueOperation2.write = write3;
    function read2(buf) {
        const header = CrdtMessageProtocol.readHeader(buf);
        if (!header) {
            return null;
        }
        if (header.type !== CrdtMessageType.APPEND_VALUE) {
            throw new Error("AppendValueOperation tried to read another message type.");
        }
        return {
            ...header,
            entityId: buf.readUint32(),
            componentId: buf.readUint32(),
            timestamp: buf.readUint32(),
            data: buf.readBuffer()
        };
    }
    AppendValueOperation2.read = read2;
})(AppendValueOperation || (AppendValueOperation = {}));

// node_modules/@dcl/ecs/dist/serialization/crdt/deleteEntity.js
var DeleteEntity;
(function(DeleteEntity2) {
    DeleteEntity2.MESSAGE_HEADER_LENGTH = 4;
    function write3(entity, buf) {
        buf.writeUint32(CRDT_MESSAGE_HEADER_LENGTH + 4);
        buf.writeUint32(CrdtMessageType.DELETE_ENTITY);
        buf.writeUint32(entity);
    }
    DeleteEntity2.write = write3;
    function read2(buf) {
        const header = CrdtMessageProtocol.readHeader(buf);
        if (!header) {
            return null;
        }
        if (header.type !== CrdtMessageType.DELETE_ENTITY) {
            throw new Error("DeleteEntity tried to read another message type.");
        }
        return {
            ...header,
            entityId: buf.readUint32()
        };
    }
    DeleteEntity2.read = read2;
})(DeleteEntity || (DeleteEntity = {}));

// node_modules/@dcl/ecs/dist/serialization/crdt/putComponent.js
var PutComponentOperation;
(function(PutComponentOperation2) {
    PutComponentOperation2.MESSAGE_HEADER_LENGTH = 16;
    function write3(entity, timestamp, componentId, data, buf) {
        const startMessageOffset = buf.incrementWriteOffset(CRDT_MESSAGE_HEADER_LENGTH + PutComponentOperation2.MESSAGE_HEADER_LENGTH);
        buf.writeBuffer(data, false);
        const messageLength = buf.currentWriteOffset() - startMessageOffset;
        buf.setUint32(startMessageOffset, messageLength);
        buf.setUint32(startMessageOffset + 4, CrdtMessageType.PUT_COMPONENT);
        buf.setUint32(startMessageOffset + 8, entity);
        buf.setUint32(startMessageOffset + 12, componentId);
        buf.setUint32(startMessageOffset + 16, timestamp);
        const newLocal = messageLength - PutComponentOperation2.MESSAGE_HEADER_LENGTH - CRDT_MESSAGE_HEADER_LENGTH;
        buf.setUint32(startMessageOffset + 20, newLocal);
    }
    PutComponentOperation2.write = write3;
    function read2(buf) {
        const header = CrdtMessageProtocol.readHeader(buf);
        if (!header) {
            return null;
        }
        if (header.type !== CrdtMessageType.PUT_COMPONENT) {
            throw new Error("PutComponentOperation tried to read another message type.");
        }
        return {
            ...header,
            entityId: buf.readUint32(),
            componentId: buf.readUint32(),
            timestamp: buf.readUint32(),
            data: buf.readBuffer()
        };
    }
    PutComponentOperation2.read = read2;
})(PutComponentOperation || (PutComponentOperation = {}));

// node_modules/@dcl/ecs/dist/systems/crdt/index.js
function crdtSceneSystem(engine2, onProcessEntityComponentChange) {
    const transports = [];
    const receivedMessages = [];
    const broadcastMessages = [];
    const outdatedMessages = [];
    function parseChunkMessage(transportId) {
        return function parseChunkMessage2(chunkMessage) {
            const buffer = new ReadWriteByteBuffer(chunkMessage);
            let header;
            while (header = CrdtMessageProtocol.getHeader(buffer)) {
                const offset = buffer.currentReadOffset();
                if (header.type === CrdtMessageType.DELETE_COMPONENT) {
                    const message = DeleteComponent.read(buffer);
                    receivedMessages.push({
                        ...message,
                        transportId,
                        messageBuffer: buffer.buffer().subarray(offset, buffer.currentReadOffset())
                    });
                } else if (header.type === CrdtMessageType.PUT_COMPONENT) {
                    const message = PutComponentOperation.read(buffer);
                    receivedMessages.push({
                        ...message,
                        transportId,
                        messageBuffer: buffer.buffer().subarray(offset, buffer.currentReadOffset())
                    });
                } else if (header.type === CrdtMessageType.DELETE_ENTITY) {
                    const message = DeleteEntity.read(buffer);
                    receivedMessages.push({
                        ...message,
                        transportId,
                        messageBuffer: buffer.buffer().subarray(offset, buffer.currentReadOffset())
                    });
                } else if (header.type === CrdtMessageType.APPEND_VALUE) {
                    const message = AppendValueOperation.read(buffer);
                    receivedMessages.push({
                        ...message,
                        transportId,
                        messageBuffer: buffer.buffer().subarray(offset, buffer.currentReadOffset())
                    });
                } else {
                    buffer.incrementReadOffset(header.length);
                }
            }
        };
    }
    function getMessages(value) {
        const messagesToProcess = value.splice(0, value.length);
        return messagesToProcess;
    }
    async function receiveMessages() {
        const messagesToProcess = getMessages(receivedMessages);
        const bufferForOutdated = new ReadWriteByteBuffer();
        const entitiesShouldBeCleaned = [];
        for (const msg of messagesToProcess) {
            if (msg.type === CrdtMessageType.DELETE_ENTITY) {
                entitiesShouldBeCleaned.push(msg.entityId);
            } else {
                const entityState = engine2.entityContainer.getEntityState(msg.entityId);
                if (entityState === EntityState.Removed)
                    continue;
                if (entityState === EntityState.Unknown) {
                    engine2.entityContainer.updateUsedEntity(msg.entityId);
                }
                const component = engine2.getComponentOrNull(msg.componentId);
                if (component) {
                    const [conflictMessage, value] = component.updateFromCrdt(msg);
                    if (conflictMessage) {
                        const offset = bufferForOutdated.currentWriteOffset();
                        if (conflictMessage.type === CrdtMessageType.PUT_COMPONENT) {
                            PutComponentOperation.write(msg.entityId, conflictMessage.timestamp, conflictMessage.componentId, conflictMessage.data, bufferForOutdated);
                        } else if (conflictMessage.type === CrdtMessageType.DELETE_COMPONENT) {
                            DeleteComponent.write(msg.entityId, component.componentId, conflictMessage.timestamp, bufferForOutdated);
                        }
                        outdatedMessages.push({
                            ...msg,
                            messageBuffer: bufferForOutdated.buffer().subarray(offset, bufferForOutdated.currentWriteOffset())
                        });
                    } else {
                        broadcastMessages.push(msg);
                        onProcessEntityComponentChange && onProcessEntityComponentChange(msg.entityId, msg.type, component, value);
                    }
                } else {
                    broadcastMessages.push(msg);
                }
            }
        }
        for (const entity of entitiesShouldBeCleaned) {
            for (let i = outdatedMessages.length - 1; i >= 0; i--) {
                if (outdatedMessages[i].entityId === entity && outdatedMessages[i].type !== CrdtMessageType.DELETE_ENTITY) {
                    outdatedMessages.splice(i, 1);
                }
            }
            for (const definition of engine2.componentsIter()) {
                definition.entityDeleted(entity, false);
            }
            engine2.entityContainer.updateRemovedEntity(entity);
            onProcessEntityComponentChange && onProcessEntityComponentChange(entity, CrdtMessageType.DELETE_ENTITY);
        }
    }
    async function sendMessages(entitiesDeletedThisTick) {
        const crdtMessages = getMessages(broadcastMessages);
        const outdatedMessagesBkp = getMessages(outdatedMessages);
        const buffer = new ReadWriteByteBuffer();
        for (const component of engine2.componentsIter()) {
            for (const message of component.getCrdtUpdates()) {
                const offset = buffer.currentWriteOffset();
                if (transports.some((t) => t.filter(message))) {
                    if (message.type === CrdtMessageType.PUT_COMPONENT) {
                        PutComponentOperation.write(message.entityId, message.timestamp, message.componentId, message.data, buffer);
                    } else if (message.type === CrdtMessageType.DELETE_COMPONENT) {
                        DeleteComponent.write(message.entityId, component.componentId, message.timestamp, buffer);
                    } else if (message.type === CrdtMessageType.APPEND_VALUE) {
                        AppendValueOperation.write(message.entityId, message.timestamp, message.componentId, message.data, buffer);
                    }
                    crdtMessages.push({
                        ...message,
                        messageBuffer: buffer.buffer().subarray(offset, buffer.currentWriteOffset())
                    });
                    if (onProcessEntityComponentChange) {
                        const rawValue = message.type === CrdtMessageType.PUT_COMPONENT || message.type === CrdtMessageType.APPEND_VALUE ? component.get(message.entityId) : void 0;
                        onProcessEntityComponentChange(message.entityId, message.type, component, rawValue);
                    }
                }
            }
        }
        for (const entityId of entitiesDeletedThisTick) {
            const offset = buffer.currentWriteOffset();
            DeleteEntity.write(entityId, buffer);
            crdtMessages.push({
                type: CrdtMessageType.DELETE_ENTITY,
                entityId,
                messageBuffer: buffer.buffer().subarray(offset, buffer.currentWriteOffset())
            });
            onProcessEntityComponentChange && onProcessEntityComponentChange(entityId, CrdtMessageType.DELETE_ENTITY);
        }
        const transportBuffer = new ReadWriteByteBuffer();
        for (const index in transports) {
            const transportIndex = Number(index);
            const transport = transports[transportIndex];
            transportBuffer.resetBuffer();
            for (const message2 of outdatedMessagesBkp) {
                if (message2.transportId === transportIndex && // TODO: This is an optimization, the state should converge anyway, whatever the message is sent.
                    // Avoid sending multiple messages for the same entity-componentId
                    !crdtMessages.find((m) => m.entityId === message2.entityId && // TODO: as any, with multiple type of messages, it should have many checks before the check for similar messages
                        m.componentId && m.componentId === message2.componentId)) {
                    transportBuffer.writeBuffer(message2.messageBuffer, false);
                }
            }
            for (const message2 of crdtMessages) {
                if (message2.transportId !== transportIndex && transport.filter(message2)) {
                    transportBuffer.writeBuffer(message2.messageBuffer, false);
                }
            }
            const message = transportBuffer.currentWriteOffset() ? transportBuffer.toBinary() : new Uint8Array([]);
            await transport.send(message);
        }
    }
    function addTransport(transport) {
        const id = transports.push(transport) - 1;
        transport.onmessage = parseChunkMessage(id);
    }
    return {
        sendMessages,
        receiveMessages,
        addTransport
    };
}

// node_modules/@dcl/ecs/dist/systems/crdt/utils.js
var CrdtUtils;
(function(CrdtUtils2) {
    let SynchronizedEntityType;
    (function(SynchronizedEntityType2) {
        SynchronizedEntityType2[SynchronizedEntityType2["NETWORKED"] = 0] = "NETWORKED";
        SynchronizedEntityType2[SynchronizedEntityType2["RENDERER"] = 1] = "RENDERER";
    })(SynchronizedEntityType = CrdtUtils2.SynchronizedEntityType || (CrdtUtils2.SynchronizedEntityType = {}));
})(CrdtUtils || (CrdtUtils = {}));
function dataCompare(a, b) {
    if (a === b)
        return 0;
    if (a === null && b !== null)
        return -1;
    if (a !== null && b === null)
        return 1;
    if (a instanceof Uint8Array && b instanceof Uint8Array) {
        const lengthDifference = a.byteLength - b.byteLength;
        if (lengthDifference !== 0) {
            return lengthDifference > 0 ? 1 : -1;
        }
        let res;
        for (let i = 0, n = a.byteLength; i < n; i++) {
            res = a[i] - b[i];
            if (res !== 0) {
                return res > 0 ? 1 : -1;
            }
        }
        return 0;
    }
    if (typeof a === "string") {
        const lengthDifference = a.length - b.length;
        if (lengthDifference !== 0) {
            return lengthDifference > 0 ? 1 : -1;
        }
        return a.localeCompare(b);
    }
    return a > b ? 1 : -1;
}

// node_modules/@dcl/ecs/dist/engine/readonly.js
function deepReadonly(val) {
    return Object.freeze({ ...val });
}

// node_modules/@dcl/ecs/dist/engine/lww-element-set-component-definition.js
function incrementTimestamp(entity, timestamps) {
    const newTimestamp = (timestamps.get(entity) || 0) + 1;
    timestamps.set(entity, newTimestamp);
    return newTimestamp;
}
function createDumpLwwFunctionFromCrdt(componentId, timestamps, schema, data) {
    return function dumpCrdtState(buffer) {
        for (const [entity, timestamp] of timestamps) {
            if (data.has(entity)) {
                const it = data.get(entity);
                const buf = new ReadWriteByteBuffer();
                schema.serialize(it, buf);
                PutComponentOperation.write(entity, timestamp, componentId, buf.toBinary(), buffer);
            } else {
                DeleteComponent.write(entity, componentId, timestamp, buffer);
            }
        }
    };
}
function createUpdateLwwFromCrdt(componentId, timestamps, schema, data) {
    function crdtRuleForCurrentState(message) {
        const { entityId, timestamp } = message;
        const currentTimestamp = timestamps.get(entityId);
        if (currentTimestamp === void 0 || currentTimestamp < timestamp) {
            return ProcessMessageResultType.StateUpdatedTimestamp;
        }
        if (currentTimestamp > timestamp) {
            return ProcessMessageResultType.StateOutdatedTimestamp;
        }
        if (message.type === CrdtMessageType.DELETE_COMPONENT && !data.has(entityId)) {
            return ProcessMessageResultType.NoChanges;
        }
        let currentDataGreater = 0;
        if (data.has(entityId)) {
            const writeBuffer = new ReadWriteByteBuffer();
            schema.serialize(data.get(entityId), writeBuffer);
            currentDataGreater = dataCompare(writeBuffer.toBinary(), message.data || null);
        } else {
            currentDataGreater = dataCompare(null, message.data);
        }
        if (currentDataGreater === 0) {
            return ProcessMessageResultType.NoChanges;
        } else if (currentDataGreater > 0) {
            return ProcessMessageResultType.StateOutdatedData;
        } else {
            return ProcessMessageResultType.StateUpdatedData;
        }
    }
    return (msg) => {
        if (msg.type !== CrdtMessageType.PUT_COMPONENT && msg.type !== CrdtMessageType.DELETE_COMPONENT)
            return [null, data.get(msg.entityId)];
        const action = crdtRuleForCurrentState(msg);
        const entity = msg.entityId;
        switch (action) {
            case ProcessMessageResultType.StateUpdatedData:
            case ProcessMessageResultType.StateUpdatedTimestamp: {
                timestamps.set(entity, msg.timestamp);
                if (msg.type === CrdtMessageType.PUT_COMPONENT) {
                    const buf = new ReadWriteByteBuffer(msg.data);
                    data.set(entity, schema.deserialize(buf));
                } else {
                    data.delete(entity);
                }
                return [null, data.get(entity)];
            }
            case ProcessMessageResultType.StateOutdatedTimestamp:
            case ProcessMessageResultType.StateOutdatedData: {
                if (data.has(entity)) {
                    const writeBuffer = new ReadWriteByteBuffer();
                    schema.serialize(data.get(entity), writeBuffer);
                    return [
                        {
                            type: CrdtMessageType.PUT_COMPONENT,
                            componentId,
                            data: writeBuffer.toBinary(),
                            entityId: entity,
                            timestamp: timestamps.get(entity)
                        },
                        data.get(entity)
                    ];
                } else {
                    return [
                        {
                            type: CrdtMessageType.DELETE_COMPONENT,
                            componentId,
                            entityId: entity,
                            timestamp: timestamps.get(entity)
                        },
                        void 0
                    ];
                }
            }
        }
        return [null, data.get(entity)];
    };
}
function createGetCrdtMessagesForLww(componentId, timestamps, dirtyIterator, schema, data) {
    return function* () {
        for (const entity of dirtyIterator) {
            const newTimestamp = incrementTimestamp(entity, timestamps);
            if (data.has(entity)) {
                const writeBuffer = new ReadWriteByteBuffer();
                schema.serialize(data.get(entity), writeBuffer);
                const msg = {
                    type: CrdtMessageType.PUT_COMPONENT,
                    componentId,
                    entityId: entity,
                    data: writeBuffer.toBinary(),
                    timestamp: newTimestamp
                };
                yield msg;
            } else {
                const msg = {
                    type: CrdtMessageType.DELETE_COMPONENT,
                    componentId,
                    entityId: entity,
                    timestamp: newTimestamp
                };
                yield msg;
            }
        }
        dirtyIterator.clear();
    };
}
function createComponentDefinitionFromSchema(componentName, componentId, schema) {
    const data = /* @__PURE__ */ new Map();
    const dirtyIterator = /* @__PURE__ */ new Set();
    const timestamps = /* @__PURE__ */ new Map();
    return {
        get componentId() {
            return componentId;
        },
        get componentName() {
            return componentName;
        },
        get componentType() {
            return 0;
        },
        schema,
        has(entity) {
            return data.has(entity);
        },
        deleteFrom(entity, markAsDirty = true) {
            const component = data.get(entity);
            if (data.delete(entity) && markAsDirty) {
                dirtyIterator.add(entity);
            }
            return component || null;
        },
        entityDeleted(entity, markAsDirty) {
            if (data.delete(entity) && markAsDirty) {
                dirtyIterator.add(entity);
            }
        },
        getOrNull(entity) {
            const component = data.get(entity);
            return component ? deepReadonly(component) : null;
        },
        get(entity) {
            const component = data.get(entity);
            if (!component) {
                throw new Error(`[getFrom] Component ${componentName} for entity #${entity} not found`);
            }
            return deepReadonly(component);
        },
        create(entity, value) {
            const component = data.get(entity);
            if (component) {
                throw new Error(`[create] Component ${componentName} for ${entity} already exists`);
            }
            const usedValue = value === void 0 ? schema.create() : schema.extend ? schema.extend(value) : value;
            data.set(entity, usedValue);
            dirtyIterator.add(entity);
            return usedValue;
        },
        createOrReplace(entity, value) {
            const usedValue = value === void 0 ? schema.create() : schema.extend ? schema.extend(value) : value;
            data.set(entity, usedValue);
            dirtyIterator.add(entity);
            return usedValue;
        },
        getMutableOrNull(entity) {
            const component = data.get(entity);
            if (!component) {
                return null;
            }
            dirtyIterator.add(entity);
            return component;
        },
        getOrCreateMutable(entity, value) {
            const component = data.get(entity);
            if (!component) {
                return this.create(entity, value);
            } else {
                dirtyIterator.add(entity);
                return component;
            }
        },
        getMutable(entity) {
            const component = this.getMutableOrNull(entity);
            if (component === null) {
                throw new Error(`[mutable] Component ${componentName} for ${entity} not found`);
            }
            return component;
        },
        *iterator() {
            for (const [entity, component] of data) {
                yield [entity, component];
            }
        },
        *dirtyIterator() {
            for (const entity of dirtyIterator) {
                yield entity;
            }
        },
        getCrdtUpdates: createGetCrdtMessagesForLww(componentId, timestamps, dirtyIterator, schema, data),
        updateFromCrdt: createUpdateLwwFromCrdt(componentId, timestamps, schema, data),
        dumpCrdtStateToBuffer: createDumpLwwFunctionFromCrdt(componentId, timestamps, schema, data)
    };
}

// node_modules/@dcl/ecs/dist/engine/systems.js
var SYSTEMS_REGULAR_PRIORITY = 1e5;
function SystemContainer() {
    const systems = [];
    function sort() {
        systems.sort((a, b) => b.priority - a.priority);
    }
    function add(fn, priority, name) {
        const systemName = name ?? fn.name;
        if (systems.find((item) => item.fn === fn)) {
            throw new Error(`System ${JSON.stringify(systemName)} already added to the engine`);
        }
        systems.push({
            fn,
            priority,
            name: systemName
        });
        sort();
    }
    function remove(selector) {
        let index = -1;
        if (typeof selector === "string") {
            index = systems.findIndex((item) => item.name === selector);
        } else {
            index = systems.findIndex((item) => item.fn === selector);
        }
        if (index === -1) {
            return false;
        }
        systems.splice(index, 1);
        sort();
        return true;
    }
    return {
        add,
        remove,
        getSystems() {
            return systems;
        }
    };
}

// node_modules/@dcl/ecs/dist/engine/grow-only-value-set-component-definition.js
var emptyReadonlySet = freezeSet(/* @__PURE__ */ new Set());
function frozenError() {
    throw new Error("The set is frozen");
}
function freezeSet(set) {
    ;
    set.add = frozenError;
    set.clear = frozenError;
    return set;
}
function sortByTimestamp(a, b) {
    return a.timestamp > b.timestamp ? 1 : -1;
}
function createValueSetComponentDefinitionFromSchema(componentName, componentId, schema, options) {
    const data = /* @__PURE__ */ new Map();
    const dirtyIterator = /* @__PURE__ */ new Set();
    const queuedCommands = [];
    function shouldSort(row) {
        const len = row.raw.length;
        if (len > 1 && row.raw[len - 1].timestamp <= row.raw[len - 2].timestamp) {
            return true;
        }
        return false;
    }
    function gotUpdated(entity) {
        const row = data.get(entity);
        if (row) {
            if (shouldSort(row)) {
                row.raw.sort(sortByTimestamp);
            }
            while (row.raw.length > options.maxElements) {
                row.raw.shift();
            }
            const frozenSet = freezeSet(new Set(row?.raw.map(($) => $.value)));
            row.frozenSet = frozenSet;
            return frozenSet;
        } else {
            return emptyReadonlySet;
        }
    }
    function append(entity, value) {
        let row = data.get(entity);
        if (!row) {
            row = { raw: [], frozenSet: emptyReadonlySet };
            data.set(entity, row);
        }
        const usedValue = schema.extend ? schema.extend(value) : value;
        const timestamp = options.timestampFunction(usedValue);
        if (__DEV__) {
            Object.freeze(usedValue);
        }
        row.raw.push({ value: usedValue, timestamp });
        return { set: gotUpdated(entity), value: usedValue };
    }
    const ret = {
        get componentId() {
            return componentId;
        },
        get componentName() {
            return componentName;
        },
        get componentType() {
            return 1;
        },
        schema,
        has(entity) {
            return data.has(entity);
        },
        entityDeleted(entity) {
            data.delete(entity);
        },
        get(entity) {
            const values = data.get(entity);
            if (values) {
                return values.frozenSet;
            } else {
                return emptyReadonlySet;
            }
        },
        addValue(entity, rawValue) {
            const { set, value } = append(entity, rawValue);
            dirtyIterator.add(entity);
            const buf = new ReadWriteByteBuffer();
            schema.serialize(value, buf);
            queuedCommands.push({
                componentId,
                data: buf.toBinary(),
                entityId: entity,
                timestamp: 0,
                type: CrdtMessageType.APPEND_VALUE
            });
            return set;
        },
        *iterator() {
            for (const [entity, component] of data) {
                yield [entity, component.frozenSet];
            }
        },
        *dirtyIterator() {
            for (const entity of dirtyIterator) {
                yield entity;
            }
        },
        getCrdtUpdates() {
            dirtyIterator.clear();
            return queuedCommands.splice(0, queuedCommands.length);
        },
        updateFromCrdt(_body) {
            if (_body.type === CrdtMessageType.APPEND_VALUE) {
                const buf = new ReadWriteByteBuffer(_body.data);
                append(_body.entityId, schema.deserialize(buf));
            }
            return [null, void 0];
        },
        dumpCrdtStateToBuffer: function(buffer) {
            for (const [entity, { raw }] of data) {
                for (const it of raw) {
                    const buf = new ReadWriteByteBuffer();
                    schema.serialize(it.value, buf);
                    AppendValueOperation.write(entity, 0, componentId, buf.toBinary(), buffer);
                }
            }
        }
    };
    return ret;
}

// node_modules/@dcl/ecs/dist/engine/input.js
var InputCommands = [
    0,
    1,
    2,
    4,
    5,
    6,
    7,
    8,
    9,
    10,
    11,
    12,
    13
    /* InputAction.IA_ACTION_6 */
];
var InputStateUpdateSystemPriority = 1 << 20;
function createInputSystem(engine2) {
    const PointerEventsResult2 = PointerEventsResult(engine2);
    const globalState = {
        previousFrameMaxTimestamp: 0,
        currentFrameMaxTimestamp: 0,
        buttonState: /* @__PURE__ */ new Map(),
        thisFrameCommands: []
    };
    function findLastAction(pointerEventType, inputAction, entity) {
        const ascendingTimestampIterator = PointerEventsResult2.get(entity);
        for (const command of Array.from(ascendingTimestampIterator).reverse()) {
            if (command.button === inputAction && command.state === pointerEventType) {
                return command;
            }
        }
    }
    function* findCommandsByActionDescending(inputAction, entity) {
        const ascendingTimestampIterator = PointerEventsResult2.get(entity);
        for (const command of Array.from(ascendingTimestampIterator).reverse()) {
            if (command.button === inputAction) {
                yield command;
            }
        }
    }
    function buttonStateUpdateSystem() {
        let maxTimestamp = globalState.currentFrameMaxTimestamp;
        globalState.previousFrameMaxTimestamp = maxTimestamp;
        if (globalState.thisFrameCommands.length) {
            globalState.thisFrameCommands = [];
        }
        for (const [, commands] of engine2.getEntitiesWith(PointerEventsResult2)) {
            const arrayCommands = Array.from(commands);
            for (let i = arrayCommands.length - 1; i >= 0; i--) {
                const command = arrayCommands[i];
                if (command.timestamp > maxTimestamp) {
                    maxTimestamp = command.timestamp;
                }
                if (command.timestamp > globalState.previousFrameMaxTimestamp) {
                    globalState.thisFrameCommands.push(command);
                }
                if (command.state === 0 || command.state === 1) {
                    const prevCommand = globalState.buttonState.get(command.button);
                    if (!prevCommand || command.timestamp > prevCommand.timestamp) {
                        globalState.buttonState.set(command.button, command);
                    } else {
                        break;
                    }
                }
            }
        }
        globalState.currentFrameMaxTimestamp = maxTimestamp;
    }
    engine2.addSystem(buttonStateUpdateSystem, InputStateUpdateSystemPriority, "@dcl/ecs#inputSystem");
    function timestampIsCurrentFrame(timestamp) {
        if (timestamp > globalState.previousFrameMaxTimestamp && timestamp <= globalState.currentFrameMaxTimestamp) {
            return true;
        } else {
            return false;
        }
    }
    function getClick(inputAction, entity) {
        if (inputAction !== 3) {
            return findClick(inputAction, entity);
        }
        for (const input of InputCommands) {
            const cmd = findClick(input, entity);
            if (cmd)
                return cmd;
        }
        return null;
    }
    function findClick(inputAction, entity) {
        let down = null;
        let up = null;
        for (const it of findCommandsByActionDescending(inputAction, entity)) {
            if (!up) {
                if (it.state === 0) {
                    up = it;
                    continue;
                }
            } else if (!down) {
                if (it.state === 1) {
                    down = it;
                    break;
                }
            }
        }
        if (!up || !down)
            return null;
        if (down.timestamp < up.timestamp && timestampIsCurrentFrame(up.timestamp)) {
            return { up, down };
        }
        return null;
    }
    function getInputCommandFromEntity(inputAction, pointerEventType, entity) {
        if (inputAction !== 3) {
            return findInputCommand(inputAction, pointerEventType, entity);
        }
        for (const input of InputCommands) {
            const cmd = findInputCommand(input, pointerEventType, entity);
            if (cmd)
                return cmd;
        }
        return null;
    }
    function getInputCommand(inputAction, pointerEventType, entity) {
        if (entity) {
            return getInputCommandFromEntity(inputAction, pointerEventType, entity);
        } else {
            for (const command of globalState.thisFrameCommands) {
                if (command.button === inputAction && command.state === pointerEventType) {
                    return command;
                }
            }
            return null;
        }
    }
    function findInputCommand(inputAction, pointerEventType, entity) {
        const command = findLastAction(pointerEventType, inputAction, entity);
        if (!command)
            return null;
        if (timestampIsCurrentFrame(command.timestamp)) {
            return command;
        } else {
            return null;
        }
    }
    function isClicked(inputAction, entity) {
        return getClick(inputAction, entity) !== null;
    }
    function isTriggered(inputAction, pointerEventType, entity) {
        if (entity) {
            const command = findLastAction(pointerEventType, inputAction, entity);
            return command && timestampIsCurrentFrame(command.timestamp) || false;
        } else {
            for (const command of globalState.thisFrameCommands) {
                if (command.button === inputAction && command.state === pointerEventType) {
                    return true;
                }
            }
            return false;
        }
    }
    function isPressed(inputAction) {
        return globalState.buttonState.get(inputAction)?.state === 1;
    }
    return {
        isPressed,
        getClick,
        getInputCommand,
        isClicked,
        isTriggered
    };
}

// node_modules/@dcl/ecs/dist/engine/component.js
var ComponentType;
(function(ComponentType2) {
    ComponentType2[ComponentType2["LastWriteWinElementSet"] = 0] = "LastWriteWinElementSet";
    ComponentType2[ComponentType2["GrowOnlyValueSet"] = 1] = "GrowOnlyValueSet";
})(ComponentType || (ComponentType = {}));

// node_modules/@dcl/ecs/dist/engine/index.js
function preEngine() {
    const entityContainer = EntityContainer();
    const componentsDefinition = /* @__PURE__ */ new Map();
    const systems = SystemContainer();
    let sealed = false;
    function addSystem(fn, priority = SYSTEMS_REGULAR_PRIORITY, name) {
        systems.add(fn, priority, name);
    }
    function removeSystem(selector) {
        return systems.remove(selector);
    }
    function addEntity() {
        const entity = entityContainer.generateEntity();
        return entity;
    }
    function removeEntity(entity) {
        for (const [, component] of componentsDefinition) {
            component.entityDeleted(entity, true);
        }
        return entityContainer.removeEntity(entity);
    }
    function registerComponentDefinition(componentName, component) {
        if (sealed)
            throw new Error("Engine is already sealed. No components can be added at this stage");
        const componentId = componentNumberFromName(componentName);
        const prev = componentsDefinition.get(componentId);
        if (prev) {
            throw new Error(`Component number ${componentId} was already registered.`);
        }
        if (component.componentName !== componentName) {
            throw new Error(`Component name doesn't match componentDefinition.componentName ${componentName} != ${component.componentName}`);
        }
        if (component.componentId !== componentId) {
            throw new Error(`Component number doesn't match componentDefinition.componentId ${componentId} != ${component.componentId}`);
        }
        componentsDefinition.set(componentId, component);
        return component;
    }
    function defineComponentFromSchema(componentName, schema) {
        if (sealed)
            throw new Error("Engine is already sealed. No components can be added at this stage");
        const componentId = componentNumberFromName(componentName);
        const prev = componentsDefinition.get(componentId);
        if (prev) {
            return prev;
        }
        const newComponent = createComponentDefinitionFromSchema(componentName, componentId, schema);
        componentsDefinition.set(componentId, newComponent);
        return newComponent;
    }
    function defineValueSetComponentFromSchema(componentName, schema, options) {
        if (sealed)
            throw new Error("Engine is already sealed. No components can be added at this stage");
        const componentId = componentNumberFromName(componentName);
        const prev = componentsDefinition.get(componentId);
        if (prev) {
            return prev;
        }
        const newComponent = createValueSetComponentDefinitionFromSchema(componentName, componentId, schema, options);
        componentsDefinition.set(componentId, newComponent);
        return newComponent;
    }
    function defineComponent(componentName, mapSpec, constructorDefault) {
        if (sealed)
            throw new Error("Engine is already sealed. No components can be added at this stage");
        const componentId = componentNumberFromName(componentName);
        const prev = componentsDefinition.get(componentId);
        if (prev) {
            return prev;
        }
        const schemaSpec = Schemas.Map(mapSpec, constructorDefault);
        const def = createComponentDefinitionFromSchema(componentName, componentId, schemaSpec);
        const newComponent = {
            ...def,
            create(entity, val) {
                return def.create(entity, val);
            },
            createOrReplace(entity, val) {
                return def.createOrReplace(entity, val);
            }
        };
        componentsDefinition.set(componentId, newComponent);
        return newComponent;
    }
    function getComponent(componentIdOrName) {
        const componentId = typeof componentIdOrName === "number" ? componentIdOrName : componentNumberFromName(componentIdOrName);
        const component = componentsDefinition.get(componentId);
        if (!component) {
            throw new Error(`Component ${componentId} not found. You need to declare the components at the beginnig of the engine declaration`);
        }
        return component;
    }
    function getComponentOrNull(componentIdOrName) {
        const componentId = typeof componentIdOrName === "number" ? componentIdOrName : componentNumberFromName(componentIdOrName);
        return componentsDefinition.get(componentId) ?? /* istanbul ignore next */
            null;
    }
    function* getEntitiesWith(...components) {
        for (const [entity, ...groupComp] of getComponentDefGroup(...components)) {
            yield [entity, ...groupComp.map((c) => c.get(entity))];
        }
    }
    function* getComponentDefGroup(...args) {
        const [firstComponentDef, ...componentDefinitions] = args;
        for (const [entity] of firstComponentDef.iterator()) {
            let matches = true;
            for (const componentDef of componentDefinitions) {
                if (!componentDef.has(entity)) {
                    matches = false;
                    break;
                }
            }
            if (matches) {
                yield [entity, ...args];
            }
        }
    }
    function getSystems() {
        return systems.getSystems();
    }
    function componentsIter() {
        return componentsDefinition.values();
    }
    function removeComponentDefinition(componentIdOrName) {
        if (sealed)
            throw new Error("Engine is already sealed. No components can be removed at this stage");
        const componentId = typeof componentIdOrName === "number" ? componentIdOrName : componentNumberFromName(componentIdOrName);
        componentsDefinition.delete(componentId);
    }
    const Transform3 = Transform({ defineComponentFromSchema });
    function* getTreeEntityArray(firstEntity, proccesedEntities) {
        if (proccesedEntities.find((value) => firstEntity === value))
            return;
        proccesedEntities.push(firstEntity);
        for (const [entity, value] of getEntitiesWith(Transform3)) {
            if (value.parent === firstEntity) {
                yield* getTreeEntityArray(entity, proccesedEntities);
            }
        }
        yield firstEntity;
    }
    function removeEntityWithChildren(firstEntity) {
        for (const entity of getTreeEntityArray(firstEntity, [])) {
            removeEntity(entity);
        }
    }
    function seal() {
        if (!sealed) {
            sealed = true;
        }
    }
    return {
        addEntity,
        removeEntity,
        addSystem,
        getSystems,
        removeSystem,
        defineComponent,
        defineComponentFromSchema,
        defineValueSetComponentFromSchema,
        getEntitiesWith,
        getComponent,
        getComponentOrNull,
        removeComponentDefinition,
        removeEntityWithChildren,
        registerComponentDefinition,
        entityContainer,
        componentsIter,
        seal
    };
}
function Engine(options) {
    const partialEngine = preEngine();
    const crdtSystem = crdtSceneSystem(partialEngine, options?.onChangeFunction || null);
    async function update(dt) {
        await crdtSystem.receiveMessages();
        for (const system of partialEngine.getSystems()) {
            const ret = system.fn(dt);
            checkNotThenable(ret, `A system (${system.name || "anonymous"}) returned a thenable. Systems cannot be async functions. Documentation: https://dcl.gg/sdk/sync-systems`);
        }
        const deletedEntites = partialEngine.entityContainer.releaseRemovedEntities();
        await crdtSystem.sendMessages(deletedEntites);
    }
    return {
        addEntity: partialEngine.addEntity,
        removeEntity: partialEngine.removeEntity,
        removeEntityWithChildren: partialEngine.removeEntityWithChildren,
        addSystem: partialEngine.addSystem,
        removeSystem: partialEngine.removeSystem,
        defineComponent: partialEngine.defineComponent,
        defineComponentFromSchema: partialEngine.defineComponentFromSchema,
        defineValueSetComponentFromSchema: partialEngine.defineValueSetComponentFromSchema,
        registerComponentDefinition: partialEngine.registerComponentDefinition,
        getEntitiesWith: partialEngine.getEntitiesWith,
        getComponent: partialEngine.getComponent,
        getComponentOrNull: partialEngine.getComponentOrNull,
        removeComponentDefinition: partialEngine.removeComponentDefinition,
        componentsIter: partialEngine.componentsIter,
        seal: partialEngine.seal,
        update,
        RootEntity: 0,
        PlayerEntity: 1,
        CameraEntity: 2,
        getEntityState: partialEngine.entityContainer.getEntityState,
        addTransport: crdtSystem.addTransport,
        entityContainer: partialEngine.entityContainer
    };
}

// node_modules/@dcl/ecs/dist/systems/async-task.js
function getAndClean(value) {
    const messagesToProcess = Array.from(value);
    value.length = 0;
    return messagesToProcess;
}
function createTaskSystem(engine2) {
    const tasks = [];
    async function runTask(task) {
        try {
            const resp = await task();
            return resp;
        } catch (e) {
            console.error(e);
        }
    }
    function executeTasks() {
        for (const task of getAndClean(tasks)) {
            runTask(task).catch(console.error);
        }
    }
    engine2.addSystem(executeTasks);
    return function(task) {
        tasks.push(task);
    };
}

// node_modules/@dcl/ecs/dist/runtime/initialization/index.js
var engine = /* @__PURE__ */ Engine();
var inputSystem = /* @__PURE__ */ createInputSystem(engine);
var executeTask = /* @__PURE__ */ createTaskSystem(engine);

// node_modules/@dcl/ecs/dist/components/generated/global.gen.js
var PointerEvents2 = /* @__PURE__ */ PointerEvents(engine);

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/common/camera_type.gen.js
var CameraType;
(function(CameraType2) {
    CameraType2[CameraType2["CT_FIRST_PERSON"] = 0] = "CT_FIRST_PERSON";
    CameraType2[CameraType2["CT_THIRD_PERSON"] = 1] = "CT_THIRD_PERSON";
    CameraType2[CameraType2["CT_CINEMATIC"] = 2] = "CT_CINEMATIC";
})(CameraType || (CameraType = {}));

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/common/input_action.gen.js
var InputAction;
(function(InputAction2) {
    InputAction2[InputAction2["IA_POINTER"] = 0] = "IA_POINTER";
    InputAction2[InputAction2["IA_PRIMARY"] = 1] = "IA_PRIMARY";
    InputAction2[InputAction2["IA_SECONDARY"] = 2] = "IA_SECONDARY";
    InputAction2[InputAction2["IA_ANY"] = 3] = "IA_ANY";
    InputAction2[InputAction2["IA_FORWARD"] = 4] = "IA_FORWARD";
    InputAction2[InputAction2["IA_BACKWARD"] = 5] = "IA_BACKWARD";
    InputAction2[InputAction2["IA_RIGHT"] = 6] = "IA_RIGHT";
    InputAction2[InputAction2["IA_LEFT"] = 7] = "IA_LEFT";
    InputAction2[InputAction2["IA_JUMP"] = 8] = "IA_JUMP";
    InputAction2[InputAction2["IA_WALK"] = 9] = "IA_WALK";
    InputAction2[InputAction2["IA_ACTION_3"] = 10] = "IA_ACTION_3";
    InputAction2[InputAction2["IA_ACTION_4"] = 11] = "IA_ACTION_4";
    InputAction2[InputAction2["IA_ACTION_5"] = 12] = "IA_ACTION_5";
    InputAction2[InputAction2["IA_ACTION_6"] = 13] = "IA_ACTION_6";
})(InputAction || (InputAction = {}));
var PointerEventType;
(function(PointerEventType2) {
    PointerEventType2[PointerEventType2["PET_UP"] = 0] = "PET_UP";
    PointerEventType2[PointerEventType2["PET_DOWN"] = 1] = "PET_DOWN";
    PointerEventType2[PointerEventType2["PET_HOVER_ENTER"] = 2] = "PET_HOVER_ENTER";
    PointerEventType2[PointerEventType2["PET_HOVER_LEAVE"] = 3] = "PET_HOVER_LEAVE";
})(PointerEventType || (PointerEventType = {}));

// node_modules/@dcl/ecs/dist/components/generated/pb/decentraland/sdk/components/common/texts.gen.js
var TextAlignMode;
(function(TextAlignMode2) {
    TextAlignMode2[TextAlignMode2["TAM_TOP_LEFT"] = 0] = "TAM_TOP_LEFT";
    TextAlignMode2[TextAlignMode2["TAM_TOP_CENTER"] = 1] = "TAM_TOP_CENTER";
    TextAlignMode2[TextAlignMode2["TAM_TOP_RIGHT"] = 2] = "TAM_TOP_RIGHT";
    TextAlignMode2[TextAlignMode2["TAM_MIDDLE_LEFT"] = 3] = "TAM_MIDDLE_LEFT";
    TextAlignMode2[TextAlignMode2["TAM_MIDDLE_CENTER"] = 4] = "TAM_MIDDLE_CENTER";
    TextAlignMode2[TextAlignMode2["TAM_MIDDLE_RIGHT"] = 5] = "TAM_MIDDLE_RIGHT";
    TextAlignMode2[TextAlignMode2["TAM_BOTTOM_LEFT"] = 6] = "TAM_BOTTOM_LEFT";
    TextAlignMode2[TextAlignMode2["TAM_BOTTOM_CENTER"] = 7] = "TAM_BOTTOM_CENTER";
    TextAlignMode2[TextAlignMode2["TAM_BOTTOM_RIGHT"] = 8] = "TAM_BOTTOM_RIGHT";
})(TextAlignMode || (TextAlignMode = {}));
var Font;
(function(Font2) {
    Font2[Font2["F_SANS_SERIF"] = 0] = "F_SANS_SERIF";
    Font2[Font2["F_SERIF"] = 1] = "F_SERIF";
    Font2[Font2["F_MONOSPACE"] = 2] = "F_MONOSPACE";
})(Font || (Font = {}));

// node_modules/@dcl/ecs/dist/composite/components.js
function getCompositeRootComponent(engine2) {
    const component = engine2.getComponentOrNull("composite::root");
    if (component) {
        return component;
    }
    return engine2.defineComponent("composite::root", {
        src: Schemas.String,
        entities: Schemas.Array(Schemas.Map({
            src: Schemas.Entity,
            dest: Schemas.Entity
        }))
    });
}

// node_modules/@dcl/ecs/dist/composite/path.js
var currentWorkingDir = "/";
function normalizeStringPosix(path, allowAboveRoot = false) {
    let res = "";
    let lastSegmentLength = 0;
    let lastSlash = -1;
    let dots = 0;
    let code;
    for (let i = 0; i <= path.length; ++i) {
        if (i < path.length)
            code = path.charCodeAt(i);
        else if (code === 47)
            break;
        else
            code = 47;
        if (code === 47) {
            if (lastSlash === i - 1 || dots === 1) {
            } else if (lastSlash !== i - 1 && dots === 2) {
                if (res.length < 2 || lastSegmentLength !== 2 || res.charCodeAt(res.length - 1) !== 46 || res.charCodeAt(res.length - 2) !== 46) {
                    if (res.length > 2) {
                        const lastSlashIndex = res.lastIndexOf("/");
                        if (lastSlashIndex !== res.length - 1) {
                            if (lastSlashIndex === -1) {
                                res = "";
                                lastSegmentLength = 0;
                            } else {
                                res = res.slice(0, lastSlashIndex);
                                lastSegmentLength = res.length - 1 - res.lastIndexOf("/");
                            }
                            lastSlash = i;
                            dots = 0;
                            continue;
                        }
                    } else if (res.length === 2 || res.length === 1) {
                        res = "";
                        lastSegmentLength = 0;
                        lastSlash = i;
                        dots = 0;
                        continue;
                    }
                }
                if (allowAboveRoot) {
                    if (res.length > 0)
                        res += "/..";
                    else
                        res = "..";
                    lastSegmentLength = 2;
                }
            } else {
                if (res.length > 0)
                    res += "/" + path.slice(lastSlash + 1, i);
                else
                    res = path.slice(lastSlash + 1, i);
                lastSegmentLength = i - lastSlash - 1;
            }
            lastSlash = i;
            dots = 0;
        } else if (code === 46 && dots !== -1) {
            ++dots;
        } else {
            dots = -1;
        }
    }
    return res;
}
function resolve(...args) {
    let resolvedPath = "";
    let resolvedAbsolute = false;
    let cwd;
    for (let i = args.length - 1; i >= -1 && !resolvedAbsolute; i--) {
        let path;
        if (i >= 0)
            path = args[i];
        else {
            if (cwd === void 0)
                cwd = currentWorkingDir;
            path = cwd;
        }
        if (path.length === 0) {
            continue;
        }
        resolvedPath = path + "/" + resolvedPath;
        resolvedAbsolute = path.charCodeAt(0) === 47;
    }
    resolvedPath = normalizeStringPosix(resolvedPath, !resolvedAbsolute);
    if (resolvedAbsolute) {
        if (resolvedPath.length > 0)
            return "/" + resolvedPath;
        else
            return "/";
    } else if (resolvedPath.length > 0) {
        return resolvedPath;
    } else {
        return ".";
    }
}
function dirname(path) {
    if (path.length === 0)
        return ".";
    let code = path.charCodeAt(0);
    const hasRoot = code === 47;
    let end = -1;
    let matchedSlash = true;
    for (let i = path.length - 1; i >= 1; --i) {
        code = path.charCodeAt(i);
        if (code === 47) {
            if (!matchedSlash) {
                end = i;
                break;
            }
        } else {
            matchedSlash = false;
        }
    }
    if (end === -1)
        return hasRoot ? "/" : ".";
    if (hasRoot && end === 1)
        return "//";
    return path.slice(0, end);
}
function resolveComposite(path, cwd) {
    const absolutePath = path.startsWith(".") ? resolve(cwd, path) : resolve(path);
    return absolutePath.substring(1);
}

// node_modules/@dcl/ecs/dist/composite/instance.js
var EntityMappingMode;
(function(EntityMappingMode2) {
    EntityMappingMode2[EntityMappingMode2["EMM_NONE"] = 0] = "EMM_NONE";
    EntityMappingMode2[EntityMappingMode2["EMM_NEXT_AVAILABLE"] = 1] = "EMM_NEXT_AVAILABLE";
    EntityMappingMode2[EntityMappingMode2["EMM_DIRECT_MAPPING"] = 2] = "EMM_DIRECT_MAPPING";
})(EntityMappingMode || (EntityMappingMode = {}));
function getComponentValue(componentDefinition, component) {
    if (component.data?.$case === "json") {
        return component.data.json;
    } else {
        return componentDefinition.schema.deserialize(new ReadWriteByteBuffer(component.data?.binary));
    }
}
function getComponentDefinition(engine2, component) {
    const existingComponentDefinition = engine2.getComponentOrNull(component.name);
    if (!existingComponentDefinition) {
        if (component.jsonSchema) {
            return engine2.defineComponentFromSchema(component.name, Schemas.fromJson(component.jsonSchema));
        } else if (component.name.startsWith("core::")) {
            if (component.name in componentDefinitionByName) {
                return componentDefinitionByName[component.name](engine2);
            } else {
                throw new Error(`The core component ${component.name} was not found.`);
            }
        } else {
            throw new Error(`${component.name} is not defined and there is no schema to define it.`);
        }
    } else {
        return existingComponentDefinition;
    }
}
function getEntityMapping(engine2, compositeEntity, mappedEntities, { entityMapping }) {
    const existingEntity = mappedEntities.get(compositeEntity);
    if (existingEntity) {
        return existingEntity;
    }
    if (entityMapping?.type === EntityMappingMode.EMM_DIRECT_MAPPING) {
        const entity = entityMapping.getCompositeEntity(compositeEntity);
        mappedEntities.set(compositeEntity, entity);
        return entity;
    }
    const newEntity = entityMapping?.type === EntityMappingMode.EMM_NEXT_AVAILABLE ? entityMapping.getNextAvailableEntity() : engine2.addEntity();
    if (newEntity === null) {
        throw new Error("There is no more entities to allocate");
    }
    mappedEntities.set(compositeEntity, newEntity);
    return newEntity;
}
function instanceComposite(engine2, compositeResource, compositeProvider, options) {
    const { rootEntity, alreadyRequestedSrc: optionalAlreadyRequestedSrc, entityMapping } = options;
    const alreadyRequestedSrc = optionalAlreadyRequestedSrc || /* @__PURE__ */ new Set();
    const compositeDirectoryPath = dirname(resolve(compositeResource.src));
    const TransformComponentNumber = componentNumberFromName("core::Transform");
    const CompositeRootComponent = getCompositeRootComponent(engine2);
    const mappedEntities = /* @__PURE__ */ new Map();
    const getCompositeEntity = (compositeEntity) => getEntityMapping(engine2, compositeEntity, mappedEntities, options);
    const compositeRootEntity = rootEntity ?? getCompositeEntity(0);
    if (rootEntity) {
        mappedEntities.set(0, rootEntity);
    }
    const childrenComposite = compositeResource.composite.components.find((item) => item.name === CompositeRootComponent.componentName);
    if (childrenComposite) {
        for (const [childCompositeEntity, compositeRawData] of childrenComposite.data) {
            const childComposite = getComponentValue(CompositeRootComponent, compositeRawData);
            const childCompositePath = resolveComposite(childComposite.src, compositeDirectoryPath);
            const childCompositeResource = compositeProvider.getCompositeOrNull(childCompositePath);
            const targetEntity = getCompositeEntity(childCompositeEntity);
            if (childCompositeResource) {
                if (alreadyRequestedSrc.has(childCompositeResource.src) || childCompositeResource.src === compositeResource.src) {
                    throw new Error(`Composite ${compositeResource.src} has a recursive instanciation while try to instance ${childCompositeResource.src}. Previous instances: ${alreadyRequestedSrc.toString()}`);
                }
                instanceComposite(engine2, childCompositeResource, compositeProvider, {
                    rootEntity: targetEntity,
                    alreadyRequestedSrc: new Set(alreadyRequestedSrc).add(childCompositeResource.src),
                    entityMapping: entityMapping?.type === EntityMappingMode.EMM_NEXT_AVAILABLE ? entityMapping : void 0
                });
            }
        }
    }
    for (const component of compositeResource.composite.components) {
        if (component.name === CompositeRootComponent.componentName)
            continue;
        const componentDefinition = getComponentDefinition(engine2, component);
        for (const [entity, compositeComponentValue] of component.data) {
            const componentValueDeserialized = getComponentValue(componentDefinition, compositeComponentValue);
            const targetEntity = getCompositeEntity(entity);
            const componentValue = componentDefinition.create(targetEntity, componentValueDeserialized);
            if (componentDefinition.componentId === TransformComponentNumber) {
                const transform = componentValue;
                if (transform.parent) {
                    transform.parent = getCompositeEntity(transform.parent);
                } else {
                    transform.parent = getCompositeEntity(0);
                }
            } else {
                Schemas.mutateNestedValues(componentDefinition.schema.jsonSchema, componentValue, (value, valueType) => {
                    if (valueType.serializationType === "entity") {
                        return { changed: true, value: getCompositeEntity(value) };
                    } else {
                        return { changed: false };
                    }
                });
            }
        }
    }
    const composite = CompositeRootComponent.getMutableOrNull(compositeRootEntity) || CompositeRootComponent.create(compositeRootEntity);
    for (const [entitySource, targetEntity] of mappedEntities) {
        composite.entities.push({
            src: entitySource,
            dest: targetEntity
        });
    }
    composite.src = compositeResource.src;
    return compositeRootEntity;
}

// node_modules/@dcl/ecs/dist/composite/proto/gen/composite.gen.js
var import_minimal36 = __toESM(require_minimal2());

// node_modules/@dcl/ecs/dist/composite/proto/gen/google/protobuf/struct.gen.js
var import_minimal35 = __toESM(require_minimal2());
var NullValue;
(function(NullValue2) {
    NullValue2[NullValue2["NULL_VALUE"] = 0] = "NULL_VALUE";
})(NullValue || (NullValue = {}));
function nullValueFromJSON(object) {
    switch (object) {
        case 0:
        case "NULL_VALUE":
            return 0;
        default:
            throw new tsProtoGlobalThis2.Error("Unrecognized enum value " + object + " for enum NullValue");
    }
}
function nullValueToJSON(object) {
    switch (object) {
        case 0:
            return "NULL_VALUE";
        default:
            throw new tsProtoGlobalThis2.Error("Unrecognized enum value " + object + " for enum NullValue");
    }
}
function createBaseStruct() {
    return { fields: /* @__PURE__ */ new Map() };
}
var Struct = {
    encode(message, writer = import_minimal35.default.Writer.create()) {
        message.fields.forEach((value, key) => {
            if (value !== void 0) {
                Struct_FieldsEntry.encode({ key, value }, writer.uint32(10).fork()).ldelim();
            }
        });
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal35.default.Reader ? input : import_minimal35.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseStruct();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    const entry1 = Struct_FieldsEntry.decode(reader, reader.uint32());
                    if (entry1.value !== void 0) {
                        message.fields.set(entry1.key, entry1.value);
                    }
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return {
            fields: isObject(object.fields) ? Object.entries(object.fields).reduce((acc, [key, value]) => {
                acc.set(key, value);
                return acc;
            }, /* @__PURE__ */ new Map()) : /* @__PURE__ */ new Map()
        };
    },
    toJSON(message) {
        const obj = {};
        obj.fields = {};
        if (message.fields) {
            message.fields.forEach((v, k) => {
                obj.fields[k] = v;
            });
        }
        return obj;
    },
    wrap(object) {
        const struct = createBaseStruct();
        if (object !== void 0) {
            Object.keys(object).forEach((key) => {
                struct.fields.set(key, object[key]);
            });
        }
        return struct;
    },
    unwrap(message) {
        const object = {};
        [...message.fields.keys()].forEach((key) => {
            object[key] = message.fields.get(key);
        });
        return object;
    }
};
function createBaseStruct_FieldsEntry() {
    return { key: "", value: void 0 };
}
var Struct_FieldsEntry = {
    encode(message, writer = import_minimal35.default.Writer.create()) {
        if (message.key !== "") {
            writer.uint32(10).string(message.key);
        }
        if (message.value !== void 0) {
            Value.encode(Value.wrap(message.value), writer.uint32(18).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal35.default.Reader ? input : import_minimal35.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseStruct_FieldsEntry();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.key = reader.string();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.value = Value.unwrap(Value.decode(reader, reader.uint32()));
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return { key: isSet(object.key) ? String(object.key) : "", value: isSet(object?.value) ? object.value : void 0 };
    },
    toJSON(message) {
        const obj = {};
        message.key !== void 0 && (obj.key = message.key);
        message.value !== void 0 && (obj.value = message.value);
        return obj;
    }
};
function createBaseValue() {
    return { kind: void 0 };
}
var Value = {
    encode(message, writer = import_minimal35.default.Writer.create()) {
        switch (message.kind?.$case) {
            case "nullValue":
                writer.uint32(8).int32(message.kind.nullValue);
                break;
            case "numberValue":
                writer.uint32(17).double(message.kind.numberValue);
                break;
            case "stringValue":
                writer.uint32(26).string(message.kind.stringValue);
                break;
            case "boolValue":
                writer.uint32(32).bool(message.kind.boolValue);
                break;
            case "structValue":
                Struct.encode(Struct.wrap(message.kind.structValue), writer.uint32(42).fork()).ldelim();
                break;
            case "listValue":
                ListValue.encode(ListValue.wrap(message.kind.listValue), writer.uint32(50).fork()).ldelim();
                break;
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal35.default.Reader ? input : import_minimal35.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseValue();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.kind = { $case: "nullValue", nullValue: reader.int32() };
                    continue;
                case 2:
                    if (tag != 17) {
                        break;
                    }
                    message.kind = { $case: "numberValue", numberValue: reader.double() };
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    message.kind = { $case: "stringValue", stringValue: reader.string() };
                    continue;
                case 4:
                    if (tag != 32) {
                        break;
                    }
                    message.kind = { $case: "boolValue", boolValue: reader.bool() };
                    continue;
                case 5:
                    if (tag != 42) {
                        break;
                    }
                    message.kind = { $case: "structValue", structValue: Struct.unwrap(Struct.decode(reader, reader.uint32())) };
                    continue;
                case 6:
                    if (tag != 50) {
                        break;
                    }
                    message.kind = { $case: "listValue", listValue: ListValue.unwrap(ListValue.decode(reader, reader.uint32())) };
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return {
            kind: isSet(object.nullValue) ? { $case: "nullValue", nullValue: nullValueFromJSON(object.nullValue) } : isSet(object.numberValue) ? { $case: "numberValue", numberValue: Number(object.numberValue) } : isSet(object.stringValue) ? { $case: "stringValue", stringValue: String(object.stringValue) } : isSet(object.boolValue) ? { $case: "boolValue", boolValue: Boolean(object.boolValue) } : isSet(object.structValue) ? { $case: "structValue", structValue: object.structValue } : isSet(object.listValue) ? { $case: "listValue", listValue: [...object.listValue] } : void 0
        };
    },
    toJSON(message) {
        const obj = {};
        message.kind?.$case === "nullValue" && (obj.nullValue = message.kind?.nullValue !== void 0 ? nullValueToJSON(message.kind?.nullValue) : void 0);
        message.kind?.$case === "numberValue" && (obj.numberValue = message.kind?.numberValue);
        message.kind?.$case === "stringValue" && (obj.stringValue = message.kind?.stringValue);
        message.kind?.$case === "boolValue" && (obj.boolValue = message.kind?.boolValue);
        message.kind?.$case === "structValue" && (obj.structValue = message.kind?.structValue);
        message.kind?.$case === "listValue" && (obj.listValue = message.kind?.listValue);
        return obj;
    },
    wrap(value) {
        const result = createBaseValue();
        if (value === null) {
            result.kind = {
                $case: "nullValue",
                nullValue: 0
                /* NullValue.NULL_VALUE */
            };
        } else if (typeof value === "boolean") {
            result.kind = { $case: "boolValue", boolValue: value };
        } else if (typeof value === "number") {
            result.kind = { $case: "numberValue", numberValue: value };
        } else if (typeof value === "string") {
            result.kind = { $case: "stringValue", stringValue: value };
        } else if (Array.isArray(value)) {
            result.kind = { $case: "listValue", listValue: value };
        } else if (typeof value === "object") {
            result.kind = { $case: "structValue", structValue: value };
        } else if (typeof value !== "undefined") {
            throw new Error("Unsupported any value type: " + typeof value);
        }
        return result;
    },
    unwrap(message) {
        if (message.kind?.$case === "nullValue") {
            return null;
        } else if (message.kind?.$case === "numberValue") {
            return message.kind?.numberValue;
        } else if (message.kind?.$case === "stringValue") {
            return message.kind?.stringValue;
        } else if (message.kind?.$case === "boolValue") {
            return message.kind?.boolValue;
        } else if (message.kind?.$case === "structValue") {
            return message.kind?.structValue;
        } else if (message.kind?.$case === "listValue") {
            return message.kind?.listValue;
        } else {
            return void 0;
        }
    }
};
function createBaseListValue() {
    return { values: [] };
}
var ListValue = {
    encode(message, writer = import_minimal35.default.Writer.create()) {
        for (const v of message.values) {
            Value.encode(Value.wrap(v), writer.uint32(10).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal35.default.Reader ? input : import_minimal35.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseListValue();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.values.push(Value.unwrap(Value.decode(reader, reader.uint32())));
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return { values: Array.isArray(object?.values) ? [...object.values] : [] };
    },
    toJSON(message) {
        const obj = {};
        if (message.values) {
            obj.values = message.values.map((e) => e);
        } else {
            obj.values = [];
        }
        return obj;
    },
    wrap(array) {
        const result = createBaseListValue();
        result.values = array ?? [];
        return result;
    },
    unwrap(message) {
        if (message?.hasOwnProperty("values") && Array.isArray(message.values)) {
            return message.values;
        } else {
            return message;
        }
    }
};
var tsProtoGlobalThis2 = (() => {
    if (typeof globalThis !== "undefined") {
        return globalThis;
    }
    if (typeof self !== "undefined") {
        return self;
    }
    if (false) {
        return void 0;
    }
    if (typeof global !== "undefined") {
        return global;
    }
    throw "Unable to locate global object";
})();
function isObject(value) {
    return typeof value === "object" && value !== null;
}
function isSet(value) {
    return value !== null && value !== void 0;
}

// node_modules/@dcl/ecs/dist/composite/proto/gen/composite.gen.js
function createBaseComponentData() {
    return { data: void 0 };
}
var ComponentData = {
    encode(message, writer = import_minimal36.default.Writer.create()) {
        switch (message.data?.$case) {
            case "json":
                Value.encode(Value.wrap(message.data.json), writer.uint32(10).fork()).ldelim();
                break;
            case "binary":
                writer.uint32(18).bytes(message.data.binary);
                break;
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal36.default.Reader ? input : import_minimal36.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseComponentData();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.data = { $case: "json", json: Value.unwrap(Value.decode(reader, reader.uint32())) };
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.data = { $case: "binary", binary: reader.bytes() };
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return {
            data: isSet2(object.json) ? { $case: "json", json: object.json } : isSet2(object.binary) ? { $case: "binary", binary: bytesFromBase64(object.binary) } : void 0
        };
    },
    toJSON(message) {
        const obj = {};
        message.data?.$case === "json" && (obj.json = message.data?.json);
        message.data?.$case === "binary" && (obj.binary = message.data?.binary !== void 0 ? base64FromBytes(message.data?.binary) : void 0);
        return obj;
    }
};
function createBaseCompositeComponent() {
    return { name: "", jsonSchema: void 0, data: /* @__PURE__ */ new Map() };
}
var CompositeComponent = {
    encode(message, writer = import_minimal36.default.Writer.create()) {
        if (message.name !== "") {
            writer.uint32(10).string(message.name);
        }
        if (message.jsonSchema !== void 0) {
            Value.encode(Value.wrap(message.jsonSchema), writer.uint32(18).fork()).ldelim();
        }
        message.data.forEach((value, key) => {
            CompositeComponent_DataEntry.encode({ key, value }, writer.uint32(26).fork()).ldelim();
        });
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal36.default.Reader ? input : import_minimal36.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseCompositeComponent();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 10) {
                        break;
                    }
                    message.name = reader.string();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.jsonSchema = Value.unwrap(Value.decode(reader, reader.uint32()));
                    continue;
                case 3:
                    if (tag != 26) {
                        break;
                    }
                    const entry3 = CompositeComponent_DataEntry.decode(reader, reader.uint32());
                    if (entry3.value !== void 0) {
                        message.data.set(entry3.key, entry3.value);
                    }
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return {
            name: isSet2(object.name) ? String(object.name) : "",
            jsonSchema: isSet2(object?.jsonSchema) ? object.jsonSchema : void 0,
            data: isObject2(object.data) ? Object.entries(object.data).reduce((acc, [key, value]) => {
                acc.set(Number(key), ComponentData.fromJSON(value));
                return acc;
            }, /* @__PURE__ */ new Map()) : /* @__PURE__ */ new Map()
        };
    },
    toJSON(message) {
        const obj = {};
        message.name !== void 0 && (obj.name = message.name);
        message.jsonSchema !== void 0 && (obj.jsonSchema = message.jsonSchema);
        obj.data = {};
        if (message.data) {
            message.data.forEach((v, k) => {
                obj.data[k] = ComponentData.toJSON(v);
            });
        }
        return obj;
    }
};
function createBaseCompositeComponent_DataEntry() {
    return { key: 0, value: void 0 };
}
var CompositeComponent_DataEntry = {
    encode(message, writer = import_minimal36.default.Writer.create()) {
        if (message.key !== 0) {
            writer.uint32(8).int32(message.key);
        }
        if (message.value !== void 0) {
            ComponentData.encode(message.value, writer.uint32(18).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal36.default.Reader ? input : import_minimal36.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseCompositeComponent_DataEntry();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.key = reader.int32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.value = ComponentData.decode(reader, reader.uint32());
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return {
            key: isSet2(object.key) ? Number(object.key) : 0,
            value: isSet2(object.value) ? ComponentData.fromJSON(object.value) : void 0
        };
    },
    toJSON(message) {
        const obj = {};
        message.key !== void 0 && (obj.key = Math.round(message.key));
        message.value !== void 0 && (obj.value = message.value ? ComponentData.toJSON(message.value) : void 0);
        return obj;
    }
};
function createBaseCompositeDefinition() {
    return { version: 0, components: [] };
}
var CompositeDefinition = {
    encode(message, writer = import_minimal36.default.Writer.create()) {
        if (message.version !== 0) {
            writer.uint32(8).int32(message.version);
        }
        for (const v of message.components) {
            CompositeComponent.encode(v, writer.uint32(18).fork()).ldelim();
        }
        return writer;
    },
    decode(input, length2) {
        const reader = input instanceof import_minimal36.default.Reader ? input : import_minimal36.default.Reader.create(input);
        let end = length2 === void 0 ? reader.len : reader.pos + length2;
        const message = createBaseCompositeDefinition();
        while (reader.pos < end) {
            const tag = reader.uint32();
            switch (tag >>> 3) {
                case 1:
                    if (tag != 8) {
                        break;
                    }
                    message.version = reader.int32();
                    continue;
                case 2:
                    if (tag != 18) {
                        break;
                    }
                    message.components.push(CompositeComponent.decode(reader, reader.uint32()));
                    continue;
            }
            if ((tag & 7) == 4 || tag == 0) {
                break;
            }
            reader.skipType(tag & 7);
        }
        return message;
    },
    fromJSON(object) {
        return {
            version: isSet2(object.version) ? Number(object.version) : 0,
            components: Array.isArray(object?.components) ? object.components.map((e) => CompositeComponent.fromJSON(e)) : []
        };
    },
    toJSON(message) {
        const obj = {};
        message.version !== void 0 && (obj.version = Math.round(message.version));
        if (message.components) {
            obj.components = message.components.map((e) => e ? CompositeComponent.toJSON(e) : void 0);
        } else {
            obj.components = [];
        }
        return obj;
    }
};
var tsProtoGlobalThis3 = (() => {
    if (typeof globalThis !== "undefined") {
        return globalThis;
    }
    if (typeof self !== "undefined") {
        return self;
    }
    if (false) {
        return void 0;
    }
    if (typeof global !== "undefined") {
        return global;
    }
    throw "Unable to locate global object";
})();
function bytesFromBase64(b64) {
    if (tsProtoGlobalThis3.Buffer) {
        return Uint8Array.from(tsProtoGlobalThis3.Buffer.from(b64, "base64"));
    } else {
        const bin = tsProtoGlobalThis3.atob(b64);
        const arr = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; ++i) {
            arr[i] = bin.charCodeAt(i);
        }
        return arr;
    }
}
function base64FromBytes(arr) {
    if (tsProtoGlobalThis3.Buffer) {
        return tsProtoGlobalThis3.Buffer.from(arr).toString("base64");
    } else {
        const bin = [];
        arr.forEach((byte) => {
            bin.push(String.fromCharCode(byte));
        });
        return tsProtoGlobalThis3.btoa(bin.join(""));
    }
}
function isObject2(value) {
    return typeof value === "object" && value !== null;
}
function isSet2(value) {
    return value !== null && value !== void 0;
}

// node_modules/@dcl/ecs/dist/composite/index.js
var Composite;
(function(Composite2) {
    function fromJson(object) {
        return CompositeDefinition.fromJSON(object);
    }
    Composite2.fromJson = fromJson;
    function fromBinary(buffer) {
        return CompositeDefinition.decode(buffer);
    }
    Composite2.fromBinary = fromBinary;
    function toJson(composite) {
        return CompositeDefinition.toJSON(composite);
    }
    Composite2.toJson = toJson;
    function toBinary(composite) {
        return CompositeDefinition.encode(composite).finish();
    }
    Composite2.toBinary = toBinary;
    function instance(engine2, compositeData, compositeProvider, options = {}) {
        instanceComposite(engine2, compositeData, compositeProvider, options);
    }
    Composite2.instance = instance;
    function resolveAndNormalizePath(src, cwd = "/") {
        return resolveComposite(src, cwd);
    }
    Composite2.resolveAndNormalizePath = resolveAndNormalizePath;
})(Composite || (Composite = {}));

// node_modules/@dcl/ecs/dist/index.js
var Transform2 = /* @__PURE__ */ Transform(engine);
var Material3 = /* @__PURE__ */ Material2(engine);
var MeshRenderer3 = /* @__PURE__ */ MeshRenderer2(engine);
var MeshCollider3 = /* @__PURE__ */ MeshCollider2(engine);

// node_modules/@dcl/ecs-math/dist/types.js
var ToGammaSpace = 1 / 2.2;
var ToLinearSpace = 2.2;
var Epsilon = 1e-6;
var DEG2RAD = Math.PI / 180;
var RAD2DEG = 360 / (Math.PI * 2);

// node_modules/@dcl/ecs-math/dist/Scalar.js
var Scalar;
(function(Scalar2) {
    Scalar2.TwoPi = Math.PI * 2;
    function withinEpsilon(a, b, epsilon = 1401298e-51) {
        const num = a - b;
        return -epsilon <= num && num <= epsilon;
    }
    Scalar2.withinEpsilon = withinEpsilon;
    function toHex(i) {
        const str = i.toString(16);
        if (i <= 15) {
            return ("0" + str).toUpperCase();
        }
        return str.toUpperCase();
    }
    Scalar2.toHex = toHex;
    function sign(value) {
        const _value = +value;
        if (_value === 0 || isNaN(_value)) {
            return _value;
        }
        return _value > 0 ? 1 : -1;
    }
    Scalar2.sign = sign;
    function clamp(value, min = 0, max = 1) {
        return Math.min(max, Math.max(min, value));
    }
    Scalar2.clamp = clamp;
    function log2(value) {
        return Math.log(value) * Math.LOG2E;
    }
    Scalar2.log2 = log2;
    function repeat(value, length2) {
        return value - Math.floor(value / length2) * length2;
    }
    Scalar2.repeat = repeat;
    function normalize(value, min, max) {
        return (value - min) / (max - min);
    }
    Scalar2.normalize = normalize;
    function denormalize(normalized, min, max) {
        return normalized * (max - min) + min;
    }
    Scalar2.denormalize = denormalize;
    function deltaAngle(current, target) {
        let num = repeat(target - current, 360);
        if (num > 180) {
            num -= 360;
        }
        return num;
    }
    Scalar2.deltaAngle = deltaAngle;
    function pingPong(tx, length2) {
        const t = repeat(tx, length2 * 2);
        return length2 - Math.abs(t - length2);
    }
    Scalar2.pingPong = pingPong;
    function smoothStep(from, to, tx) {
        let t = clamp(tx);
        t = -2 * t * t * t + 3 * t * t;
        return to * t + from * (1 - t);
    }
    Scalar2.smoothStep = smoothStep;
    function moveTowards(current, target, maxDelta) {
        let result = 0;
        if (Math.abs(target - current) <= maxDelta) {
            result = target;
        } else {
            result = current + sign(target - current) * maxDelta;
        }
        return result;
    }
    Scalar2.moveTowards = moveTowards;
    function moveTowardsAngle(current, target, maxDelta) {
        const num = deltaAngle(current, target);
        let result = 0;
        if (-maxDelta < num && num < maxDelta) {
            result = target;
        } else {
            result = moveTowards(current, current + num, maxDelta);
        }
        return result;
    }
    Scalar2.moveTowardsAngle = moveTowardsAngle;
    function lerp(start, end, amount) {
        return start + (end - start) * amount;
    }
    Scalar2.lerp = lerp;
    function lerpAngle(start, end, amount) {
        let num = repeat(end - start, 360);
        if (num > 180) {
            num -= 360;
        }
        return start + num * clamp(amount);
    }
    Scalar2.lerpAngle = lerpAngle;
    function inverseLerp(a, b, value) {
        let result = 0;
        if (a !== b) {
            result = clamp((value - a) / (b - a));
        } else {
            result = 0;
        }
        return result;
    }
    Scalar2.inverseLerp = inverseLerp;
    function hermite(value1, tangent1, value2, tangent2, amount) {
        const squared = amount * amount;
        const cubed = amount * squared;
        const part1 = 2 * cubed - 3 * squared + 1;
        const part2 = -2 * cubed + 3 * squared;
        const part3 = cubed - 2 * squared + amount;
        const part4 = cubed - squared;
        return value1 * part1 + value2 * part2 + tangent1 * part3 + tangent2 * part4;
    }
    Scalar2.hermite = hermite;
    function randomRange(min, max) {
        if (min === max) {
            return min;
        }
        return Math.random() * (max - min) + min;
    }
    Scalar2.randomRange = randomRange;
    function rangeToPercent(num, min, max) {
        return (num - min) / (max - min);
    }
    Scalar2.rangeToPercent = rangeToPercent;
    function percentToRange(percent, min, max) {
        return (max - min) * percent + min;
    }
    Scalar2.percentToRange = percentToRange;
    function normalizeRadians(angle) {
        return angle - Scalar2.TwoPi * Math.floor((angle + Math.PI) / Scalar2.TwoPi);
    }
    Scalar2.normalizeRadians = normalizeRadians;
})(Scalar || (Scalar = {}));

// node_modules/@dcl/ecs-math/dist/Vector3.js
var Vector32;
(function(Vector33) {
    function isNonUniform(vector) {
        const absX = Math.abs(vector.x);
        const absY = Math.abs(vector.y);
        if (absX !== absY) {
            return true;
        }
        const absZ = Math.abs(vector.z);
        if (absX !== absZ) {
            return true;
        }
        return false;
    }
    Vector33.isNonUniform = isNonUniform;
    function create(x = 0, y = 0, z = 0) {
        return { x, y, z };
    }
    Vector33.create = create;
    function add(vector1, vector2) {
        return {
            x: vector1.x + vector2.x,
            y: vector1.y + vector2.y,
            z: vector1.z + vector2.z
        };
    }
    Vector33.add = add;
    function addToRef(vector1, vector2, result) {
        result.x = vector1.x + vector2.x;
        result.y = vector1.y + vector2.y;
        result.z = vector1.z + vector2.z;
    }
    Vector33.addToRef = addToRef;
    function subtract(vector1, vector2) {
        return {
            x: vector1.x - vector2.x,
            y: vector1.y - vector2.y,
            z: vector1.z - vector2.z
        };
    }
    Vector33.subtract = subtract;
    function subtractToRef(vector1, vector2, result) {
        result.x = vector1.x - vector2.x;
        result.y = vector1.y - vector2.y;
        result.z = vector1.z - vector2.z;
    }
    Vector33.subtractToRef = subtractToRef;
    function subtractFromFloatsToRef(vector1, x, y, z, result) {
        result.x = vector1.x - x;
        result.y = vector1.y - y;
        result.z = vector1.z - z;
    }
    Vector33.subtractFromFloatsToRef = subtractFromFloatsToRef;
    function negate(value) {
        return { x: -value.x, y: -value.y, z: -value.z };
    }
    Vector33.negate = negate;
    function copyFrom(source, dest) {
        dest.x = source.x;
        dest.y = source.y;
        dest.z = source.z;
    }
    Vector33.copyFrom = copyFrom;
    function copyFromFloats(x, y, z, dest) {
        dest.x = x;
        dest.y = y;
        dest.z = z;
    }
    Vector33.copyFromFloats = copyFromFloats;
    function clone(source) {
        return create(source.x, source.y, source.z);
    }
    Vector33.clone = clone;
    function getClipFactor(vector0, vector1, axis, size) {
        const d0 = dot(vector0, axis) - size;
        const d1 = dot(vector1, axis) - size;
        const s = d0 / (d0 - d1);
        return s;
    }
    Vector33.getClipFactor = getClipFactor;
    function getAngleBetweenVectors(vector0, vector1, normal) {
        const v0 = normalize(vector0);
        const v1 = normalize(vector1);
        const v0v1dot = dot(v0, v1);
        const n = create();
        crossToRef(v0, v1, n);
        if (dot(n, normal) > 0) {
            return Math.acos(v0v1dot);
        }
        return -Math.acos(v0v1dot);
    }
    Vector33.getAngleBetweenVectors = getAngleBetweenVectors;
    function fromArray(array, offset = 0) {
        return create(array[offset], array[offset + 1], array[offset + 2]);
    }
    Vector33.fromArray = fromArray;
    function fromFloatArray(array, offset) {
        return fromArray(array, offset);
    }
    Vector33.fromFloatArray = fromFloatArray;
    function fromArrayToRef(array, offset, result) {
        result.x = array[offset];
        result.y = array[offset + 1];
        result.z = array[offset + 2];
    }
    Vector33.fromArrayToRef = fromArrayToRef;
    function fromFloatArrayToRef(array, offset, result) {
        return fromArrayToRef(array, offset, result);
    }
    Vector33.fromFloatArrayToRef = fromFloatArrayToRef;
    function length2(vector) {
        return Math.sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
    }
    Vector33.length = length2;
    function lengthSquared(vector) {
        return vector.x * vector.x + vector.y * vector.y + vector.z * vector.z;
    }
    Vector33.lengthSquared = lengthSquared;
    function scaleToRef(vector, scale2, result) {
        result.x = vector.x * scale2;
        result.y = vector.y * scale2;
        result.z = vector.z * scale2;
    }
    Vector33.scaleToRef = scaleToRef;
    function scale(vector, scale2) {
        return create(vector.x * scale2, vector.y * scale2, vector.z * scale2);
    }
    Vector33.scale = scale;
    function normalizeFromLength(vector, len) {
        const result = create(0, 0, 0);
        normalizeFromLengthToRef(vector, len, result);
        return result;
    }
    Vector33.normalizeFromLength = normalizeFromLength;
    function normalizeFromLengthToRef(vector, len, result) {
        if (len === 0 || len === 1) {
            copyFrom(vector, result);
            return;
        }
        scaleToRef(vector, 1 / len, result);
    }
    Vector33.normalizeFromLengthToRef = normalizeFromLengthToRef;
    function normalize(vector) {
        return normalizeFromLength(vector, length2(vector));
    }
    Vector33.normalize = normalize;
    function normalizeToRef(vector, result) {
        normalizeFromLengthToRef(vector, length2(vector), result);
    }
    Vector33.normalizeToRef = normalizeToRef;
    function dot(left, right) {
        return left.x * right.x + left.y * right.y + left.z * right.z;
    }
    Vector33.dot = dot;
    function applyMatrix4(vector, matrix) {
        const result = clone(vector);
        applyMatrix4ToRef(vector, matrix, result);
        return result;
    }
    Vector33.applyMatrix4 = applyMatrix4;
    function applyMatrix4ToRef(vector, matrix, result) {
        const { x, y, z } = vector;
        const m = matrix._m;
        const w = 1 / (m[3] * x + m[7] * y + m[11] * z + m[15]);
        result.x = (m[0] * x + m[4] * y + m[8] * z + m[12]) * w;
        result.y = (m[1] * x + m[5] * y + m[9] * z + m[13]) * w;
        result.z = (m[2] * x + m[6] * y + m[10] * z + m[14]) * w;
    }
    Vector33.applyMatrix4ToRef = applyMatrix4ToRef;
    function rotate(vector, q) {
        const result = create();
        rotateToRef(vector, q, result);
        return result;
    }
    Vector33.rotate = rotate;
    function rotateToRef(vector, q, result) {
        const { x, y, z } = vector;
        const { x: qx, y: qy, z: qz, w: qw } = q;
        const ix = qw * x + qy * z - qz * y;
        const iy = qw * y + qz * x - qx * z;
        const iz = qw * z + qx * y - qy * x;
        const iw = -qx * x - qy * y - qz * z;
        result.x = ix * qw + iw * -qx + iy * -qz - iz * -qy;
        result.y = iy * qw + iw * -qy + iz * -qx - ix * -qz;
        result.z = iz * qw + iw * -qz + ix * -qy - iy * -qx;
    }
    Vector33.rotateToRef = rotateToRef;
    function lerp(start, end, amount) {
        const result = create(0, 0, 0);
        lerpToRef(start, end, amount, result);
        return result;
    }
    Vector33.lerp = lerp;
    function lerpToRef(start, end, amount, result) {
        result.x = start.x + (end.x - start.x) * amount;
        result.y = start.y + (end.y - start.y) * amount;
        result.z = start.z + (end.z - start.z) * amount;
    }
    Vector33.lerpToRef = lerpToRef;
    function cross(left, right) {
        const result = Zero();
        crossToRef(left, right, result);
        return result;
    }
    Vector33.cross = cross;
    function crossToRef(left, right, result) {
        result.x = left.y * right.z - left.z * right.y;
        result.y = left.z * right.x - left.x * right.z;
        result.z = left.x * right.y - left.y * right.x;
    }
    Vector33.crossToRef = crossToRef;
    function transformCoordinates(vector, transformation) {
        const result = Zero();
        transformCoordinatesToRef(vector, transformation, result);
        return result;
    }
    Vector33.transformCoordinates = transformCoordinates;
    function transformCoordinatesToRef(vector, transformation, result) {
        return transformCoordinatesFromFloatsToRef(vector.x, vector.y, vector.z, transformation, result);
    }
    Vector33.transformCoordinatesToRef = transformCoordinatesToRef;
    function transformCoordinatesFromFloatsToRef(x, y, z, transformation, result) {
        const m = transformation._m;
        const rx = x * m[0] + y * m[4] + z * m[8] + m[12];
        const ry = x * m[1] + y * m[5] + z * m[9] + m[13];
        const rz = x * m[2] + y * m[6] + z * m[10] + m[14];
        const rw = 1 / (x * m[3] + y * m[7] + z * m[11] + m[15]);
        result.x = rx * rw;
        result.y = ry * rw;
        result.z = rz * rw;
    }
    Vector33.transformCoordinatesFromFloatsToRef = transformCoordinatesFromFloatsToRef;
    function transformNormal(vector, transformation) {
        const result = Zero();
        transformNormalToRef(vector, transformation, result);
        return result;
    }
    Vector33.transformNormal = transformNormal;
    function transformNormalToRef(vector, transformation, result) {
        transformNormalFromFloatsToRef(vector.x, vector.y, vector.z, transformation, result);
    }
    Vector33.transformNormalToRef = transformNormalToRef;
    function transformNormalFromFloatsToRef(x, y, z, transformation, result) {
        const m = transformation._m;
        result.x = x * m[0] + y * m[4] + z * m[8];
        result.y = x * m[1] + y * m[5] + z * m[9];
        result.z = x * m[2] + y * m[6] + z * m[10];
    }
    Vector33.transformNormalFromFloatsToRef = transformNormalFromFloatsToRef;
    function catmullRom(value1, value2, value3, value4, amount) {
        const squared = amount * amount;
        const cubed = amount * squared;
        const x = 0.5 * (2 * value2.x + (-value1.x + value3.x) * amount + (2 * value1.x - 5 * value2.x + 4 * value3.x - value4.x) * squared + (-value1.x + 3 * value2.x - 3 * value3.x + value4.x) * cubed);
        const y = 0.5 * (2 * value2.y + (-value1.y + value3.y) * amount + (2 * value1.y - 5 * value2.y + 4 * value3.y - value4.y) * squared + (-value1.y + 3 * value2.y - 3 * value3.y + value4.y) * cubed);
        const z = 0.5 * (2 * value2.z + (-value1.z + value3.z) * amount + (2 * value1.z - 5 * value2.z + 4 * value3.z - value4.z) * squared + (-value1.z + 3 * value2.z - 3 * value3.z + value4.z) * cubed);
        return create(x, y, z);
    }
    Vector33.catmullRom = catmullRom;
    function clamp(value, min, max) {
        const v = create();
        clampToRef(value, min, max, v);
        return v;
    }
    Vector33.clamp = clamp;
    function clampToRef(value, min, max, result) {
        let x = value.x;
        x = x > max.x ? max.x : x;
        x = x < min.x ? min.x : x;
        let y = value.y;
        y = y > max.y ? max.y : y;
        y = y < min.y ? min.y : y;
        let z = value.z;
        z = z > max.z ? max.z : z;
        z = z < min.z ? min.z : z;
        copyFromFloats(x, y, z, result);
    }
    Vector33.clampToRef = clampToRef;
    function hermite(value1, tangent1, value2, tangent2, amount) {
        const squared = amount * amount;
        const cubed = amount * squared;
        const part1 = 2 * cubed - 3 * squared + 1;
        const part2 = -2 * cubed + 3 * squared;
        const part3 = cubed - 2 * squared + amount;
        const part4 = cubed - squared;
        const x = value1.x * part1 + value2.x * part2 + tangent1.x * part3 + tangent2.x * part4;
        const y = value1.y * part1 + value2.y * part2 + tangent1.y * part3 + tangent2.y * part4;
        const z = value1.z * part1 + value2.z * part2 + tangent1.z * part3 + tangent2.z * part4;
        return create(x, y, z);
    }
    Vector33.hermite = hermite;
    function minimize(left, right) {
        const min = create();
        minimizeInPlaceFromFloatsToRef(right, left.x, left.y, left.z, min);
        return min;
    }
    Vector33.minimize = minimize;
    function maximize(left, right) {
        const max = create();
        maximizeInPlaceFromFloatsToRef(left, right.x, right.y, right.z, max);
        return max;
    }
    Vector33.maximize = maximize;
    function distance(value1, value2) {
        return Math.sqrt(distanceSquared(value1, value2));
    }
    Vector33.distance = distance;
    function distanceSquared(value1, value2) {
        const x = value1.x - value2.x;
        const y = value1.y - value2.y;
        const z = value1.z - value2.z;
        return x * x + y * y + z * z;
    }
    Vector33.distanceSquared = distanceSquared;
    function center(value1, value2) {
        const center2 = add(value1, value2);
        scaleToRef(center2, 0.5, center2);
        return center2;
    }
    Vector33.center = center;
    function rotationFromAxis(axis1, axis2, axis3) {
        const rotation = Zero();
        rotationFromAxisToRef(axis1, axis2, axis3, rotation);
        return rotation;
    }
    Vector33.rotationFromAxis = rotationFromAxis;
    function rotationFromAxisToRef(axis1, axis2, axis3, result) {
        const quat = Quaternion.create();
        Quaternion.fromAxisToRotationQuaternionToRef(axis1, axis2, axis3, quat);
        copyFrom(Quaternion.toEulerAngles(quat), result);
    }
    Vector33.rotationFromAxisToRef = rotationFromAxisToRef;
    function toString(vector) {
        return `(${vector.x}, ${vector.y}, ${vector.z})`;
    }
    Vector33.toString = toString;
    function getHashCode(vector) {
        let hash = vector.x || 0;
        hash = hash * 397 ^ (vector.y || 0);
        hash = hash * 397 ^ (vector.z || 0);
        return hash;
    }
    Vector33.getHashCode = getHashCode;
    function equals(vector1, vector2) {
        return vector1.x === vector2.x && vector1.y === vector2.y && vector1.z === vector2.z;
    }
    Vector33.equals = equals;
    function equalsWithEpsilon(vector1, vector2, epsilon = Epsilon) {
        return Scalar.withinEpsilon(vector1.x, vector2.x, epsilon) && Scalar.withinEpsilon(vector1.y, vector2.y, epsilon) && Scalar.withinEpsilon(vector1.z, vector2.z, epsilon);
    }
    Vector33.equalsWithEpsilon = equalsWithEpsilon;
    function equalsToFloats(vector, x, y, z) {
        return vector.x === x && vector.y === y && vector.z === z;
    }
    Vector33.equalsToFloats = equalsToFloats;
    function multiply(vector1, vector2) {
        const result = create();
        multiplyToRef(vector1, vector2, result);
        return result;
    }
    Vector33.multiply = multiply;
    function multiplyToRef(vector1, vector2, result) {
        result.x = vector1.x * vector2.x;
        result.y = vector1.y * vector2.y;
        result.z = vector1.z * vector2.z;
    }
    Vector33.multiplyToRef = multiplyToRef;
    function multiplyByFloatsToRef(vector1, x, y, z, result) {
        result.x = vector1.x * x;
        result.y = vector1.y * y;
        result.z = vector1.z * z;
    }
    Vector33.multiplyByFloatsToRef = multiplyByFloatsToRef;
    function multiplyByFloats(vector1, x, y, z) {
        const result = create();
        multiplyByFloatsToRef(vector1, x, y, z, result);
        return result;
    }
    Vector33.multiplyByFloats = multiplyByFloats;
    function divide(vector1, vector2) {
        return {
            x: vector1.x / vector2.x,
            y: vector1.y / vector2.y,
            z: vector1.z / vector2.z
        };
    }
    Vector33.divide = divide;
    function divideToRef(vector1, vector2, result) {
        result.x = vector1.x / vector2.x;
        result.y = vector1.y / vector2.y;
        result.z = vector1.z / vector2.z;
    }
    Vector33.divideToRef = divideToRef;
    function maximizeInPlaceFromFloatsToRef(vector1, x, y, z, result) {
        if (x > vector1.x) {
            result.x = x;
        } else {
            result.x = vector1.x;
        }
        if (y > vector1.y) {
            result.y = y;
        } else {
            result.y = vector1.y;
        }
        if (z > vector1.z) {
            result.z = z;
        } else {
            result.z = vector1.z;
        }
    }
    Vector33.maximizeInPlaceFromFloatsToRef = maximizeInPlaceFromFloatsToRef;
    function minimizeInPlaceFromFloatsToRef(vector1, x, y, z, result) {
        if (x < vector1.x) {
            result.x = x;
        } else {
            result.x = vector1.x;
        }
        if (y < vector1.y) {
            result.y = y;
        } else {
            result.y = vector1.y;
        }
        if (z < vector1.z) {
            result.z = z;
        } else {
            result.z = vector1.z;
        }
    }
    Vector33.minimizeInPlaceFromFloatsToRef = minimizeInPlaceFromFloatsToRef;
    function floor(vector1) {
        return create(Math.floor(vector1.x), Math.floor(vector1.y), Math.floor(vector1.z));
    }
    Vector33.floor = floor;
    function fract(vector1) {
        return create(vector1.x - Math.floor(vector1.x), vector1.y - Math.floor(vector1.y), vector1.z - Math.floor(vector1.z));
    }
    Vector33.fract = fract;
    function Zero() {
        return create(0, 0, 0);
    }
    Vector33.Zero = Zero;
    function One() {
        return create(1, 1, 1);
    }
    Vector33.One = One;
    function Up() {
        return create(0, 1, 0);
    }
    Vector33.Up = Up;
    function Down() {
        return create(0, -1, 0);
    }
    Vector33.Down = Down;
    function Forward() {
        return create(0, 0, 1);
    }
    Vector33.Forward = Forward;
    function Backward() {
        return create(0, 0, -1);
    }
    Vector33.Backward = Backward;
    function Right() {
        return create(1, 0, 0);
    }
    Vector33.Right = Right;
    function Left() {
        return create(-1, 0, 0);
    }
    Vector33.Left = Left;
    function Random() {
        return create(Math.random(), Math.random(), Math.random());
    }
    Vector33.Random = Random;
})(Vector32 || (Vector32 = {}));

// node_modules/@dcl/ecs-math/dist/Plane.js
var Plane;
(function(Plane2) {
    function create(a, b, c, d) {
        return {
            normal: Vector32.create(a, b, c),
            d
        };
    }
    Plane2.create = create;
    function fromArray(array) {
        return create(array[0], array[1], array[2], array[3]);
    }
    Plane2.fromArray = fromArray;
    function fromPoints(_point1, _point2, _point3) {
        const result = create(0, 0, 0, 0);
        return result;
    }
    Plane2.fromPoints = fromPoints;
    function romPositionAndNormal(origin, normal) {
        const result = create(0, 0, 0, 0);
        result.normal = Vector32.normalize(normal);
        result.d = -(normal.x * origin.x + normal.y * origin.y + normal.z * origin.z);
        return result;
    }
    Plane2.romPositionAndNormal = romPositionAndNormal;
    function signedDistanceToPlaneFromPositionAndNormal(origin, normal, point) {
        const d = -(normal.x * origin.x + normal.y * origin.y + normal.z * origin.z);
        return Vector32.dot(point, normal) + d;
    }
    Plane2.signedDistanceToPlaneFromPositionAndNormal = signedDistanceToPlaneFromPositionAndNormal;
    function asArray(plane) {
        return [plane.normal.x, plane.normal.y, plane.normal.z, plane.d];
    }
    Plane2.asArray = asArray;
    function clone(plane) {
        return create(plane.normal.x, plane.normal.y, plane.normal.z, plane.d);
    }
    Plane2.clone = clone;
    function getHashCode(_plane) {
        return 0;
    }
    Plane2.getHashCode = getHashCode;
    function normalize(plane) {
        const result = create(0, 0, 0, 0);
        const norm = Math.sqrt(plane.normal.x * plane.normal.x + plane.normal.y * plane.normal.y + plane.normal.z * plane.normal.z);
        let magnitude = 0;
        if (norm !== 0) {
            magnitude = 1 / norm;
        }
        result.normal.x = plane.normal.x * magnitude;
        result.normal.y = plane.normal.y * magnitude;
        result.normal.z = plane.normal.z * magnitude;
        result.d *= magnitude;
        return plane;
    }
    Plane2.normalize = normalize;
    function transform(plane, transformation) {
        const transposedMatrix = Matrix.create();
        Matrix.transposeToRef(transformation, transposedMatrix);
        const m = transposedMatrix._m;
        const x = plane.normal.x;
        const y = plane.normal.y;
        const z = plane.normal.z;
        const d = plane.d;
        const normalX = x * m[0] + y * m[1] + z * m[2] + d * m[3];
        const normalY = x * m[4] + y * m[5] + z * m[6] + d * m[7];
        const normalZ = x * m[8] + y * m[9] + z * m[10] + d * m[11];
        const finalD = x * m[12] + y * m[13] + z * m[14] + d * m[15];
        return create(normalX, normalY, normalZ, finalD);
    }
    Plane2.transform = transform;
    function dotCoordinate(plane, point) {
        return plane.normal.x * point.x + plane.normal.y * point.y + plane.normal.z * point.z + plane.d;
    }
    Plane2.dotCoordinate = dotCoordinate;
    function copyFromPoints(point1, point2, point3) {
        const x1 = point2.x - point1.x;
        const y1 = point2.y - point1.y;
        const z1 = point2.z - point1.z;
        const x2 = point3.x - point1.x;
        const y2 = point3.y - point1.y;
        const z2 = point3.z - point1.z;
        const yz = y1 * z2 - z1 * y2;
        const xz = z1 * x2 - x1 * z2;
        const xy = x1 * y2 - y1 * x2;
        const pyth = Math.sqrt(yz * yz + xz * xz + xy * xy);
        let invPyth;
        if (pyth !== 0) {
            invPyth = 1 / pyth;
        } else {
            invPyth = 0;
        }
        const normal = Vector32.create(yz * invPyth, xz * invPyth, xy * invPyth);
        return {
            normal,
            d: -(normal.x * point1.x + normal.y * point1.y + normal.z * point1.z)
        };
    }
    Plane2.copyFromPoints = copyFromPoints;
    function isFrontFacingTo(plane, direction, epsilon) {
        const dot = Vector32.dot(plane.normal, direction);
        return dot <= epsilon;
    }
    Plane2.isFrontFacingTo = isFrontFacingTo;
    function signedDistanceTo(plane, point) {
        return Vector32.dot(point, plane.normal) + plane.d;
    }
    Plane2.signedDistanceTo = signedDistanceTo;
})(Plane || (Plane = {}));

// node_modules/@dcl/ecs-math/dist/Matrix.js
var Matrix;
(function(Matrix2) {
    function m(self2) {
        return self2._m;
    }
    Matrix2.m = m;
    let _updateFlagSeed = 0;
    const _identityReadonly = {};
    function IdentityReadonly() {
        return _identityReadonly;
    }
    Matrix2.IdentityReadonly = IdentityReadonly;
    function create() {
        const newMatrix = {
            updateFlag: 0,
            isIdentity: false,
            isIdentity3x2: true,
            _isIdentityDirty: true,
            _isIdentity3x2Dirty: true,
            _m: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
        };
        _updateIdentityStatus(newMatrix, false);
        return newMatrix;
    }
    Matrix2.create = create;
    function fromArray(array, offset = 0) {
        const result = create();
        fromArrayToRef(array, offset, result);
        return result;
    }
    Matrix2.fromArray = fromArray;
    function fromArrayToRef(array, offset, result) {
        for (let index = 0; index < 16; index++) {
            result._m[index] = array[index + offset];
        }
        _markAsUpdated(result);
    }
    Matrix2.fromArrayToRef = fromArrayToRef;
    function fromFloatArrayToRefScaled(array, offset, scale2, result) {
        for (let index = 0; index < 16; index++) {
            result._m[index] = array[index + offset] * scale2;
        }
        _markAsUpdated(result);
    }
    Matrix2.fromFloatArrayToRefScaled = fromFloatArrayToRefScaled;
    function fromValuesToRef(initialM11, initialM12, initialM13, initialM14, initialM21, initialM22, initialM23, initialM24, initialM31, initialM32, initialM33, initialM34, initialM41, initialM42, initialM43, initialM44, result) {
        const m2 = result._m;
        m2[0] = initialM11;
        m2[1] = initialM12;
        m2[2] = initialM13;
        m2[3] = initialM14;
        m2[4] = initialM21;
        m2[5] = initialM22;
        m2[6] = initialM23;
        m2[7] = initialM24;
        m2[8] = initialM31;
        m2[9] = initialM32;
        m2[10] = initialM33;
        m2[11] = initialM34;
        m2[12] = initialM41;
        m2[13] = initialM42;
        m2[14] = initialM43;
        m2[15] = initialM44;
        _markAsUpdated(result);
    }
    Matrix2.fromValuesToRef = fromValuesToRef;
    function fromValues(initialM11, initialM12, initialM13, initialM14, initialM21, initialM22, initialM23, initialM24, initialM31, initialM32, initialM33, initialM34, initialM41, initialM42, initialM43, initialM44) {
        const result = create();
        const m2 = result._m;
        m2[0] = initialM11;
        m2[1] = initialM12;
        m2[2] = initialM13;
        m2[3] = initialM14;
        m2[4] = initialM21;
        m2[5] = initialM22;
        m2[6] = initialM23;
        m2[7] = initialM24;
        m2[8] = initialM31;
        m2[9] = initialM32;
        m2[10] = initialM33;
        m2[11] = initialM34;
        m2[12] = initialM41;
        m2[13] = initialM42;
        m2[14] = initialM43;
        m2[15] = initialM44;
        _markAsUpdated(result);
        return result;
    }
    Matrix2.fromValues = fromValues;
    function compose(scale2, rotation, translation2) {
        const result = create();
        composeToRef(scale2, rotation, translation2, result);
        return result;
    }
    Matrix2.compose = compose;
    function composeToRef(scale2, rotation, translation2, result) {
        const tmpMatrix = [create(), create(), create()];
        scalingToRef(scale2.x, scale2.y, scale2.z, tmpMatrix[1]);
        fromQuaternionToRef(rotation, tmpMatrix[0]);
        multiplyToRef(tmpMatrix[1], tmpMatrix[0], result);
        setTranslation(result, translation2);
    }
    Matrix2.composeToRef = composeToRef;
    function Identity() {
        const identity = fromValues(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
        _updateIdentityStatus(identity, true);
        return identity;
    }
    Matrix2.Identity = Identity;
    function IdentityToRef(result) {
        fromValuesToRef(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, result);
        _updateIdentityStatus(result, true);
    }
    Matrix2.IdentityToRef = IdentityToRef;
    function Zero() {
        const zero = fromValues(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        _updateIdentityStatus(zero, false);
        return zero;
    }
    Matrix2.Zero = Zero;
    function RotationX(angle) {
        const result = create();
        rotationXToRef(angle, result);
        return result;
    }
    Matrix2.RotationX = RotationX;
    function rotationXToRef(angle, result) {
        const s = Math.sin(angle);
        const c = Math.cos(angle);
        fromValuesToRef(1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, result);
        _updateIdentityStatus(result, c === 1 && s === 0);
    }
    Matrix2.rotationXToRef = rotationXToRef;
    function rotationY(angle) {
        const result = create();
        rotationYToRef(angle, result);
        return result;
    }
    Matrix2.rotationY = rotationY;
    function rotationYToRef(angle, result) {
        const s = Math.sin(angle);
        const c = Math.cos(angle);
        fromValuesToRef(c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1, result);
        _updateIdentityStatus(result, c === 1 && s === 0);
    }
    Matrix2.rotationYToRef = rotationYToRef;
    function rotationZ(angle) {
        const result = create();
        rotationZToRef(angle, result);
        return result;
    }
    Matrix2.rotationZ = rotationZ;
    function rotationZToRef(angle, result) {
        const s = Math.sin(angle);
        const c = Math.cos(angle);
        fromValuesToRef(c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, result);
        _updateIdentityStatus(result, c === 1 && s === 0);
    }
    Matrix2.rotationZToRef = rotationZToRef;
    function rotationAxis(axis, angle) {
        const result = create();
        rotationAxisToRef(axis, angle, result);
        return result;
    }
    Matrix2.rotationAxis = rotationAxis;
    function rotationAxisToRef(_axis, angle, result) {
        const s = Math.sin(-angle);
        const c = Math.cos(-angle);
        const c1 = 1 - c;
        const axis = Vector32.normalize(_axis);
        const m2 = result._m;
        m2[0] = axis.x * axis.x * c1 + c;
        m2[1] = axis.x * axis.y * c1 - axis.z * s;
        m2[2] = axis.x * axis.z * c1 + axis.y * s;
        m2[3] = 0;
        m2[4] = axis.y * axis.x * c1 + axis.z * s;
        m2[5] = axis.y * axis.y * c1 + c;
        m2[6] = axis.y * axis.z * c1 - axis.x * s;
        m2[7] = 0;
        m2[8] = axis.z * axis.x * c1 - axis.y * s;
        m2[9] = axis.z * axis.y * c1 + axis.x * s;
        m2[10] = axis.z * axis.z * c1 + c;
        m2[11] = 0;
        m2[12] = 0;
        m2[13] = 0;
        m2[14] = 0;
        m2[15] = 1;
        _markAsUpdated(result);
    }
    Matrix2.rotationAxisToRef = rotationAxisToRef;
    function rotationYawPitchRoll(yaw, pitch, roll) {
        const result = create();
        rotationYawPitchRollToRef(yaw, pitch, roll, result);
        return result;
    }
    Matrix2.rotationYawPitchRoll = rotationYawPitchRoll;
    function rotationYawPitchRollToRef(yaw, pitch, roll, result) {
        const quaternionResult = Quaternion.Zero();
        Quaternion.fromRotationYawPitchRollToRef(yaw, pitch, roll, quaternionResult);
        fromQuaternionToRef(quaternionResult, result);
    }
    Matrix2.rotationYawPitchRollToRef = rotationYawPitchRollToRef;
    function scaling(x, y, z) {
        const result = create();
        scalingToRef(x, y, z, result);
        return result;
    }
    Matrix2.scaling = scaling;
    function scalingToRef(x, y, z, result) {
        fromValuesToRef(x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1, result);
        _updateIdentityStatus(result, x === 1 && y === 1 && z === 1);
    }
    Matrix2.scalingToRef = scalingToRef;
    function translation(x, y, z) {
        const result = create();
        translationToRef(x, y, z, result);
        return result;
    }
    Matrix2.translation = translation;
    function translationToRef(x, y, z, result) {
        fromValuesToRef(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1, result);
        _updateIdentityStatus(result, x === 0 && y === 0 && z === 0);
    }
    Matrix2.translationToRef = translationToRef;
    function lerp(startValue, endValue, gradient) {
        const result = create();
        lerpToRef(startValue, endValue, gradient, result);
        return result;
    }
    Matrix2.lerp = lerp;
    function lerpToRef(startValue, endValue, gradient, result) {
        for (let index = 0; index < 16; index++) {
            result._m[index] = startValue._m[index] * (1 - gradient) + endValue._m[index] * gradient;
        }
        _markAsUpdated(result);
    }
    Matrix2.lerpToRef = lerpToRef;
    function decomposeLerp(startValue, endValue, gradient) {
        const result = create();
        decomposeLerpToRef(startValue, endValue, gradient, result);
        return result;
    }
    Matrix2.decomposeLerp = decomposeLerp;
    function decomposeLerpToRef(startValue, endValue, gradient, result) {
        const startScale = Vector32.Zero();
        const startRotation = Quaternion.Zero();
        const startTranslation = Vector32.Zero();
        decompose(startValue, startScale, startRotation, startTranslation);
        const endScale = Vector32.Zero();
        const endRotation = Quaternion.Zero();
        const endTranslation = Vector32.Zero();
        decompose(endValue, endScale, endRotation, endTranslation);
        const resultScale = Vector32.Zero();
        Vector32.lerpToRef(startScale, endScale, gradient, resultScale);
        const resultRotation = Quaternion.Zero();
        Quaternion.slerpToRef(startRotation, endRotation, gradient, resultRotation);
        const resultTranslation = Vector32.Zero();
        Vector32.lerpToRef(startTranslation, endTranslation, gradient, resultTranslation);
        composeToRef(resultScale, resultRotation, resultTranslation, result);
    }
    Matrix2.decomposeLerpToRef = decomposeLerpToRef;
    function LookAtLH(eye, target, up) {
        const result = create();
        lookAtLHToRef(eye, target, up, result);
        return result;
    }
    Matrix2.LookAtLH = LookAtLH;
    function lookAtLHToRef(eye, target, up, result) {
        const xAxis = Vector32.Zero();
        const yAxis = Vector32.Zero();
        const zAxis = Vector32.Zero();
        Vector32.subtractToRef(target, eye, zAxis);
        Vector32.normalizeToRef(zAxis, zAxis);
        Vector32.crossToRef(up, zAxis, xAxis);
        const xSquareLength = Vector32.lengthSquared(xAxis);
        if (xSquareLength === 0) {
            xAxis.x = 1;
        } else {
            Vector32.normalizeFromLengthToRef(xAxis, Math.sqrt(xSquareLength), xAxis);
        }
        Vector32.crossToRef(zAxis, xAxis, yAxis);
        Vector32.normalizeToRef(yAxis, yAxis);
        const ex = -Vector32.dot(xAxis, eye);
        const ey = -Vector32.dot(yAxis, eye);
        const ez = -Vector32.dot(zAxis, eye);
        fromValuesToRef(xAxis.x, yAxis.x, zAxis.x, 0, xAxis.y, yAxis.y, zAxis.y, 0, xAxis.z, yAxis.z, zAxis.z, 0, ex, ey, ez, 1, result);
    }
    Matrix2.lookAtLHToRef = lookAtLHToRef;
    function lookAtRH(eye, target, up) {
        const result = create();
        lookAtRHToRef(eye, target, up, result);
        return result;
    }
    Matrix2.lookAtRH = lookAtRH;
    function lookAtRHToRef(eye, target, up, result) {
        const xAxis = Vector32.Zero();
        const yAxis = Vector32.Zero();
        const zAxis = Vector32.Zero();
        Vector32.subtractToRef(eye, target, zAxis);
        Vector32.normalizeToRef(zAxis, zAxis);
        Vector32.crossToRef(up, zAxis, xAxis);
        const xSquareLength = Vector32.lengthSquared(xAxis);
        if (xSquareLength === 0) {
            xAxis.x = 1;
        } else {
            Vector32.normalizeFromLengthToRef(xAxis, Math.sqrt(xSquareLength), xAxis);
        }
        Vector32.crossToRef(zAxis, xAxis, yAxis);
        Vector32.normalizeToRef(yAxis, yAxis);
        const ex = -Vector32.dot(xAxis, eye);
        const ey = -Vector32.dot(yAxis, eye);
        const ez = -Vector32.dot(zAxis, eye);
        fromValuesToRef(xAxis.x, yAxis.x, zAxis.x, 0, xAxis.y, yAxis.y, zAxis.y, 0, xAxis.z, yAxis.z, zAxis.z, 0, ex, ey, ez, 1, result);
    }
    Matrix2.lookAtRHToRef = lookAtRHToRef;
    function orthoLH(width, height, znear, zfar) {
        const matrix = create();
        orthoLHToRef(width, height, znear, zfar, matrix);
        return matrix;
    }
    Matrix2.orthoLH = orthoLH;
    function orthoLHToRef(width, height, znear, zfar, result) {
        const n = znear;
        const f = zfar;
        const a = 2 / width;
        const b = 2 / height;
        const c = 2 / (f - n);
        const d = -(f + n) / (f - n);
        fromValuesToRef(a, 0, 0, 0, 0, b, 0, 0, 0, 0, c, 0, 0, 0, d, 1, result);
        _updateIdentityStatus(result, a === 1 && b === 1 && c === 1 && d === 0);
    }
    Matrix2.orthoLHToRef = orthoLHToRef;
    function OrthoOffCenterLH(left, right, bottom, top, znear, zfar) {
        const matrix = create();
        orthoOffCenterLHToRef(left, right, bottom, top, znear, zfar, matrix);
        return matrix;
    }
    Matrix2.OrthoOffCenterLH = OrthoOffCenterLH;
    function orthoOffCenterLHToRef(left, right, bottom, top, znear, zfar, result) {
        const n = znear;
        const f = zfar;
        const a = 2 / (right - left);
        const b = 2 / (top - bottom);
        const c = 2 / (f - n);
        const d = -(f + n) / (f - n);
        const i0 = (left + right) / (left - right);
        const i1 = (top + bottom) / (bottom - top);
        fromValuesToRef(a, 0, 0, 0, 0, b, 0, 0, 0, 0, c, 0, i0, i1, d, 1, result);
        _markAsUpdated(result);
    }
    Matrix2.orthoOffCenterLHToRef = orthoOffCenterLHToRef;
    function orthoOffCenterRH(left, right, bottom, top, znear, zfar) {
        const matrix = create();
        orthoOffCenterRHToRef(left, right, bottom, top, znear, zfar, matrix);
        return matrix;
    }
    Matrix2.orthoOffCenterRH = orthoOffCenterRH;
    function orthoOffCenterRHToRef(left, right, bottom, top, znear, zfar, result) {
        orthoOffCenterLHToRef(left, right, bottom, top, znear, zfar, result);
        result._m[10] *= -1;
    }
    Matrix2.orthoOffCenterRHToRef = orthoOffCenterRHToRef;
    function perspectiveLH(width, height, znear, zfar) {
        const matrix = create();
        const n = znear;
        const f = zfar;
        const a = 2 * n / width;
        const b = 2 * n / height;
        const c = (f + n) / (f - n);
        const d = -2 * f * n / (f - n);
        fromValuesToRef(a, 0, 0, 0, 0, b, 0, 0, 0, 0, c, 1, 0, 0, d, 0, matrix);
        _updateIdentityStatus(matrix, false);
        return matrix;
    }
    Matrix2.perspectiveLH = perspectiveLH;
    function perspectiveFovLH(fov, aspect, znear, zfar) {
        const matrix = create();
        perspectiveFovLHToRef(fov, aspect, znear, zfar, matrix);
        return matrix;
    }
    Matrix2.perspectiveFovLH = perspectiveFovLH;
    function perspectiveFovLHToRef(fov, aspect, znear, zfar, result, isVerticalFovFixed = true) {
        const n = znear;
        const f = zfar;
        const t = 1 / Math.tan(fov * 0.5);
        const a = isVerticalFovFixed ? t / aspect : t;
        const b = isVerticalFovFixed ? t : t * aspect;
        const c = (f + n) / (f - n);
        const d = -2 * f * n / (f - n);
        fromValuesToRef(a, 0, 0, 0, 0, b, 0, 0, 0, 0, c, 1, 0, 0, d, 0, result);
        _updateIdentityStatus(result, false);
    }
    Matrix2.perspectiveFovLHToRef = perspectiveFovLHToRef;
    function PerspectiveFovRH(fov, aspect, znear, zfar) {
        const matrix = create();
        perspectiveFovRHToRef(fov, aspect, znear, zfar, matrix);
        return matrix;
    }
    Matrix2.PerspectiveFovRH = PerspectiveFovRH;
    function perspectiveFovRHToRef(fov, aspect, znear, zfar, result, isVerticalFovFixed = true) {
        const n = znear;
        const f = zfar;
        const t = 1 / Math.tan(fov * 0.5);
        const a = isVerticalFovFixed ? t / aspect : t;
        const b = isVerticalFovFixed ? t : t * aspect;
        const c = -(f + n) / (f - n);
        const d = -2 * f * n / (f - n);
        fromValuesToRef(a, 0, 0, 0, 0, b, 0, 0, 0, 0, c, -1, 0, 0, d, 0, result);
        _updateIdentityStatus(result, false);
    }
    Matrix2.perspectiveFovRHToRef = perspectiveFovRHToRef;
    function perspectiveFovWebVRToRef(fov, znear, zfar, result, rightHanded = false) {
        const rightHandedFactor = rightHanded ? -1 : 1;
        const upTan = Math.tan(fov.upDegrees * Math.PI / 180);
        const downTan = Math.tan(fov.downDegrees * Math.PI / 180);
        const leftTan = Math.tan(fov.leftDegrees * Math.PI / 180);
        const rightTan = Math.tan(fov.rightDegrees * Math.PI / 180);
        const xScale = 2 / (leftTan + rightTan);
        const yScale = 2 / (upTan + downTan);
        const m2 = result._m;
        m2[0] = xScale;
        m2[1] = m2[2] = m2[3] = m2[4] = 0;
        m2[5] = yScale;
        m2[6] = m2[7] = 0;
        m2[8] = (leftTan - rightTan) * xScale * 0.5;
        m2[9] = -((upTan - downTan) * yScale * 0.5);
        m2[10] = -zfar / (znear - zfar);
        m2[11] = 1 * rightHandedFactor;
        m2[12] = m2[13] = m2[15] = 0;
        m2[14] = -(2 * zfar * znear) / (zfar - znear);
        _markAsUpdated(result);
    }
    Matrix2.perspectiveFovWebVRToRef = perspectiveFovWebVRToRef;
    function GetAsMatrix2x2(matrix) {
        return [matrix._m[0], matrix._m[1], matrix._m[4], matrix._m[5]];
    }
    Matrix2.GetAsMatrix2x2 = GetAsMatrix2x2;
    function GetAsMatrix3x3(matrix) {
        return [
            matrix._m[0],
            matrix._m[1],
            matrix._m[2],
            matrix._m[4],
            matrix._m[5],
            matrix._m[6],
            matrix._m[8],
            matrix._m[9],
            matrix._m[10]
        ];
    }
    Matrix2.GetAsMatrix3x3 = GetAsMatrix3x3;
    function transpose(matrix) {
        const result = create();
        transposeToRef(matrix, result);
        return result;
    }
    Matrix2.transpose = transpose;
    function transposeToRef(matrix, result) {
        const rm = result._m;
        const mm = matrix._m;
        rm[0] = mm[0];
        rm[1] = mm[4];
        rm[2] = mm[8];
        rm[3] = mm[12];
        rm[4] = mm[1];
        rm[5] = mm[5];
        rm[6] = mm[9];
        rm[7] = mm[13];
        rm[8] = mm[2];
        rm[9] = mm[6];
        rm[10] = mm[10];
        rm[11] = mm[14];
        rm[12] = mm[3];
        rm[13] = mm[7];
        rm[14] = mm[11];
        rm[15] = mm[15];
        _updateIdentityStatus(result, matrix.isIdentity, matrix._isIdentityDirty);
    }
    Matrix2.transposeToRef = transposeToRef;
    function reflection(plane) {
        const matrix = create();
        reflectionToRef(plane, matrix);
        return matrix;
    }
    Matrix2.reflection = reflection;
    function reflectionToRef(_plane, result) {
        const plane = Plane.normalize(_plane);
        const x = plane.normal.x;
        const y = plane.normal.y;
        const z = plane.normal.z;
        const temp = -2 * x;
        const temp2 = -2 * y;
        const temp3 = -2 * z;
        fromValuesToRef(temp * x + 1, temp2 * x, temp3 * x, 0, temp * y, temp2 * y + 1, temp3 * y, 0, temp * z, temp2 * z, temp3 * z + 1, 0, temp * plane.d, temp2 * plane.d, temp3 * plane.d, 1, result);
    }
    Matrix2.reflectionToRef = reflectionToRef;
    function fromXYZAxesToRef(xaxis, yaxis, zaxis, result) {
        fromValuesToRef(xaxis.x, xaxis.y, xaxis.z, 0, yaxis.x, yaxis.y, yaxis.z, 0, zaxis.x, zaxis.y, zaxis.z, 0, 0, 0, 0, 1, result);
    }
    Matrix2.fromXYZAxesToRef = fromXYZAxesToRef;
    function fromQuaternionToRef(quat, result) {
        const xx = quat.x * quat.x;
        const yy = quat.y * quat.y;
        const zz = quat.z * quat.z;
        const xy = quat.x * quat.y;
        const zw = quat.z * quat.w;
        const zx = quat.z * quat.x;
        const yw = quat.y * quat.w;
        const yz = quat.y * quat.z;
        const xw = quat.x * quat.w;
        result._m[0] = 1 - 2 * (yy + zz);
        result._m[1] = 2 * (xy + zw);
        result._m[2] = 2 * (zx - yw);
        result._m[3] = 0;
        result._m[4] = 2 * (xy - zw);
        result._m[5] = 1 - 2 * (zz + xx);
        result._m[6] = 2 * (yz + xw);
        result._m[7] = 0;
        result._m[8] = 2 * (zx + yw);
        result._m[9] = 2 * (yz - xw);
        result._m[10] = 1 - 2 * (yy + xx);
        result._m[11] = 0;
        result._m[12] = 0;
        result._m[13] = 0;
        result._m[14] = 0;
        result._m[15] = 1;
        _markAsUpdated(result);
    }
    Matrix2.fromQuaternionToRef = fromQuaternionToRef;
    function _markAsUpdated(self2) {
        self2.updateFlag = _updateFlagSeed++;
        self2.isIdentity = false;
        self2.isIdentity3x2 = false;
        self2._isIdentityDirty = true;
        self2._isIdentity3x2Dirty = true;
    }
    function isIdentityUpdate(self2) {
        if (self2._isIdentityDirty) {
            self2._isIdentityDirty = false;
            const m2 = self2._m;
            self2.isIdentity = m2[0] === 1 && m2[1] === 0 && m2[2] === 0 && m2[3] === 0 && m2[4] === 0 && m2[5] === 1 && m2[6] === 0 && m2[7] === 0 && m2[8] === 0 && m2[9] === 0 && m2[10] === 1 && m2[11] === 0 && m2[12] === 0 && m2[13] === 0 && m2[14] === 0 && m2[15] === 1;
        }
        return self2.isIdentity;
    }
    Matrix2.isIdentityUpdate = isIdentityUpdate;
    function isIdentityAs3x2Update(self2) {
        if (self2._isIdentity3x2Dirty) {
            self2._isIdentity3x2Dirty = false;
            if (self2._m[0] !== 1 || self2._m[5] !== 1 || self2._m[15] !== 1) {
                self2.isIdentity3x2 = false;
            } else if (self2._m[1] !== 0 || self2._m[2] !== 0 || self2._m[3] !== 0 || self2._m[4] !== 0 || self2._m[6] !== 0 || self2._m[7] !== 0 || self2._m[8] !== 0 || self2._m[9] !== 0 || self2._m[10] !== 0 || self2._m[11] !== 0 || self2._m[12] !== 0 || self2._m[13] !== 0 || self2._m[14] !== 0) {
                self2.isIdentity3x2 = false;
            } else {
                self2.isIdentity3x2 = true;
            }
        }
        return self2.isIdentity3x2;
    }
    Matrix2.isIdentityAs3x2Update = isIdentityAs3x2Update;
    function determinant(self2) {
        if (self2.isIdentity === true) {
            return 1;
        }
        const m2 = self2._m;
        const m00 = m2[0], m01 = m2[1], m02 = m2[2], m03 = m2[3];
        const m10 = m2[4], m11 = m2[5], m12 = m2[6], m13 = m2[7];
        const m20 = m2[8], m21 = m2[9], m22 = m2[10], m23 = m2[11];
        const m30 = m2[12], m31 = m2[13], m32 = m2[14], m33 = m2[15];
        const det_22_33 = m22 * m33 - m32 * m23;
        const det_21_33 = m21 * m33 - m31 * m23;
        const det_21_32 = m21 * m32 - m31 * m22;
        const det_20_33 = m20 * m33 - m30 * m23;
        const det_20_32 = m20 * m32 - m22 * m30;
        const det_20_31 = m20 * m31 - m30 * m21;
        const cofact_00 = +(m11 * det_22_33 - m12 * det_21_33 + m13 * det_21_32);
        const cofact_01 = -(m10 * det_22_33 - m12 * det_20_33 + m13 * det_20_32);
        const cofact_02 = +(m10 * det_21_33 - m11 * det_20_33 + m13 * det_20_31);
        const cofact_03 = -(m10 * det_21_32 - m11 * det_20_32 + m12 * det_20_31);
        return m00 * cofact_00 + m01 * cofact_01 + m02 * cofact_02 + m03 * cofact_03;
    }
    Matrix2.determinant = determinant;
    function toArray(self2) {
        return self2._m;
    }
    Matrix2.toArray = toArray;
    function asArray(self2) {
        return self2._m;
    }
    Matrix2.asArray = asArray;
    function reset(self2) {
        fromValuesToRef(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, self2);
        _updateIdentityStatus(self2, false);
    }
    Matrix2.reset = reset;
    function add(self2, other) {
        const result = create();
        addToRef(self2, other, result);
        return result;
    }
    Matrix2.add = add;
    function addToRef(self2, other, result) {
        for (let index = 0; index < 16; index++) {
            result._m[index] = self2._m[index] + other._m[index];
        }
        _markAsUpdated(result);
    }
    Matrix2.addToRef = addToRef;
    function addToSelf(self2, other) {
        for (let index = 0; index < 16; index++) {
            self2._m[index] += other._m[index];
        }
        _markAsUpdated(self2);
    }
    Matrix2.addToSelf = addToSelf;
    function invert(source) {
        const result = create();
        invertToRef(source, result);
        return result;
    }
    Matrix2.invert = invert;
    function invertToRef(source, result) {
        if (source.isIdentity === true) {
            copy(source, result);
            return;
        }
        const m2 = source._m;
        const m00 = m2[0], m01 = m2[1], m02 = m2[2], m03 = m2[3];
        const m10 = m2[4], m11 = m2[5], m12 = m2[6], m13 = m2[7];
        const m20 = m2[8], m21 = m2[9], m22 = m2[10], m23 = m2[11];
        const m30 = m2[12], m31 = m2[13], m32 = m2[14], m33 = m2[15];
        const det_22_33 = m22 * m33 - m32 * m23;
        const det_21_33 = m21 * m33 - m31 * m23;
        const det_21_32 = m21 * m32 - m31 * m22;
        const det_20_33 = m20 * m33 - m30 * m23;
        const det_20_32 = m20 * m32 - m22 * m30;
        const det_20_31 = m20 * m31 - m30 * m21;
        const cofact_00 = +(m11 * det_22_33 - m12 * det_21_33 + m13 * det_21_32);
        const cofact_01 = -(m10 * det_22_33 - m12 * det_20_33 + m13 * det_20_32);
        const cofact_02 = +(m10 * det_21_33 - m11 * det_20_33 + m13 * det_20_31);
        const cofact_03 = -(m10 * det_21_32 - m11 * det_20_32 + m12 * det_20_31);
        const det = m00 * cofact_00 + m01 * cofact_01 + m02 * cofact_02 + m03 * cofact_03;
        if (det === 0) {
            copy(source, result);
            return;
        }
        const detInv = 1 / det;
        const det_12_33 = m12 * m33 - m32 * m13;
        const det_11_33 = m11 * m33 - m31 * m13;
        const det_11_32 = m11 * m32 - m31 * m12;
        const det_10_33 = m10 * m33 - m30 * m13;
        const det_10_32 = m10 * m32 - m30 * m12;
        const det_10_31 = m10 * m31 - m30 * m11;
        const det_12_23 = m12 * m23 - m22 * m13;
        const det_11_23 = m11 * m23 - m21 * m13;
        const det_11_22 = m11 * m22 - m21 * m12;
        const det_10_23 = m10 * m23 - m20 * m13;
        const det_10_22 = m10 * m22 - m20 * m12;
        const det_10_21 = m10 * m21 - m20 * m11;
        const cofact_10 = -(m01 * det_22_33 - m02 * det_21_33 + m03 * det_21_32);
        const cofact_11 = +(m00 * det_22_33 - m02 * det_20_33 + m03 * det_20_32);
        const cofact_12 = -(m00 * det_21_33 - m01 * det_20_33 + m03 * det_20_31);
        const cofact_13 = +(m00 * det_21_32 - m01 * det_20_32 + m02 * det_20_31);
        const cofact_20 = +(m01 * det_12_33 - m02 * det_11_33 + m03 * det_11_32);
        const cofact_21 = -(m00 * det_12_33 - m02 * det_10_33 + m03 * det_10_32);
        const cofact_22 = +(m00 * det_11_33 - m01 * det_10_33 + m03 * det_10_31);
        const cofact_23 = -(m00 * det_11_32 - m01 * det_10_32 + m02 * det_10_31);
        const cofact_30 = -(m01 * det_12_23 - m02 * det_11_23 + m03 * det_11_22);
        const cofact_31 = +(m00 * det_12_23 - m02 * det_10_23 + m03 * det_10_22);
        const cofact_32 = -(m00 * det_11_23 - m01 * det_10_23 + m03 * det_10_21);
        const cofact_33 = +(m00 * det_11_22 - m01 * det_10_22 + m02 * det_10_21);
        fromValuesToRef(cofact_00 * detInv, cofact_10 * detInv, cofact_20 * detInv, cofact_30 * detInv, cofact_01 * detInv, cofact_11 * detInv, cofact_21 * detInv, cofact_31 * detInv, cofact_02 * detInv, cofact_12 * detInv, cofact_22 * detInv, cofact_32 * detInv, cofact_03 * detInv, cofact_13 * detInv, cofact_23 * detInv, cofact_33 * detInv, result);
    }
    Matrix2.invertToRef = invertToRef;
    function addAtIndex(self2, index, value) {
        self2._m[index] += value;
        _markAsUpdated(self2);
    }
    Matrix2.addAtIndex = addAtIndex;
    function multiplyAtIndex(self2, index, value) {
        self2._m[index] *= value;
        _markAsUpdated(self2);
        return self2;
    }
    Matrix2.multiplyAtIndex = multiplyAtIndex;
    function setTranslationFromFloats(self2, x, y, z) {
        self2._m[12] = x;
        self2._m[13] = y;
        self2._m[14] = z;
        _markAsUpdated(self2);
    }
    Matrix2.setTranslationFromFloats = setTranslationFromFloats;
    function setTranslation(self2, vector3) {
        setTranslationFromFloats(self2, vector3.x, vector3.y, vector3.z);
    }
    Matrix2.setTranslation = setTranslation;
    function getTranslation(self2) {
        return Vector32.create(self2._m[12], self2._m[13], self2._m[14]);
    }
    Matrix2.getTranslation = getTranslation;
    function getTranslationToRef(self2, result) {
        result.x = self2._m[12];
        result.y = self2._m[13];
        result.z = self2._m[14];
    }
    Matrix2.getTranslationToRef = getTranslationToRef;
    function removeRotationAndScaling(self2) {
        const m2 = self2._m;
        fromValuesToRef(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, m2[12], m2[13], m2[14], m2[15], self2);
        _updateIdentityStatus(self2, m2[12] === 0 && m2[13] === 0 && m2[14] === 0 && m2[15] === 1);
        return self2;
    }
    Matrix2.removeRotationAndScaling = removeRotationAndScaling;
    function multiply(self2, other) {
        const result = create();
        multiplyToRef(self2, other, result);
        return result;
    }
    Matrix2.multiply = multiply;
    function copy(from, dest) {
        copyToArray(from, dest._m);
        _updateIdentityStatus(dest, from.isIdentity, from._isIdentityDirty, from.isIdentity3x2, from._isIdentity3x2Dirty);
    }
    Matrix2.copy = copy;
    function copyToArray(self2, arrayDest, offsetDest = 0) {
        for (let index = 0; index < 16; index++) {
            arrayDest[offsetDest + index] = self2._m[index];
        }
    }
    Matrix2.copyToArray = copyToArray;
    function multiplyToRef(self2, other, result) {
        if (self2.isIdentity) {
            copy(other, result);
            return;
        }
        if (other.isIdentity) {
            copy(self2, result);
            return;
        }
        multiplyToArray(self2, other, result._m, 0);
        _markAsUpdated(result);
    }
    Matrix2.multiplyToRef = multiplyToRef;
    function multiplyToArray(self2, other, result, offset) {
        const m2 = self2._m;
        const otherM = other._m;
        const tm0 = m2[0], tm1 = m2[1], tm2 = m2[2], tm3 = m2[3];
        const tm4 = m2[4], tm5 = m2[5], tm6 = m2[6], tm7 = m2[7];
        const tm8 = m2[8], tm9 = m2[9], tm10 = m2[10], tm11 = m2[11];
        const tm12 = m2[12], tm13 = m2[13], tm14 = m2[14], tm15 = m2[15];
        const om0 = otherM[0], om1 = otherM[1], om2 = otherM[2], om3 = otherM[3];
        const om4 = otherM[4], om5 = otherM[5], om6 = otherM[6], om7 = otherM[7];
        const om8 = otherM[8], om9 = otherM[9], om10 = otherM[10], om11 = otherM[11];
        const om12 = otherM[12], om13 = otherM[13], om14 = otherM[14], om15 = otherM[15];
        result[offset] = tm0 * om0 + tm1 * om4 + tm2 * om8 + tm3 * om12;
        result[offset + 1] = tm0 * om1 + tm1 * om5 + tm2 * om9 + tm3 * om13;
        result[offset + 2] = tm0 * om2 + tm1 * om6 + tm2 * om10 + tm3 * om14;
        result[offset + 3] = tm0 * om3 + tm1 * om7 + tm2 * om11 + tm3 * om15;
        result[offset + 4] = tm4 * om0 + tm5 * om4 + tm6 * om8 + tm7 * om12;
        result[offset + 5] = tm4 * om1 + tm5 * om5 + tm6 * om9 + tm7 * om13;
        result[offset + 6] = tm4 * om2 + tm5 * om6 + tm6 * om10 + tm7 * om14;
        result[offset + 7] = tm4 * om3 + tm5 * om7 + tm6 * om11 + tm7 * om15;
        result[offset + 8] = tm8 * om0 + tm9 * om4 + tm10 * om8 + tm11 * om12;
        result[offset + 9] = tm8 * om1 + tm9 * om5 + tm10 * om9 + tm11 * om13;
        result[offset + 10] = tm8 * om2 + tm9 * om6 + tm10 * om10 + tm11 * om14;
        result[offset + 11] = tm8 * om3 + tm9 * om7 + tm10 * om11 + tm11 * om15;
        result[offset + 12] = tm12 * om0 + tm13 * om4 + tm14 * om8 + tm15 * om12;
        result[offset + 13] = tm12 * om1 + tm13 * om5 + tm14 * om9 + tm15 * om13;
        result[offset + 14] = tm12 * om2 + tm13 * om6 + tm14 * om10 + tm15 * om14;
        result[offset + 15] = tm12 * om3 + tm13 * om7 + tm14 * om11 + tm15 * om15;
    }
    Matrix2.multiplyToArray = multiplyToArray;
    function equals(self2, value) {
        const other = value;
        if (!other) {
            return false;
        }
        if (self2.isIdentity || other.isIdentity) {
            if (!self2._isIdentityDirty && !other._isIdentityDirty) {
                return self2.isIdentity && other.isIdentity;
            }
        }
        const m2 = self2._m;
        const om = other._m;
        return m2[0] === om[0] && m2[1] === om[1] && m2[2] === om[2] && m2[3] === om[3] && m2[4] === om[4] && m2[5] === om[5] && m2[6] === om[6] && m2[7] === om[7] && m2[8] === om[8] && m2[9] === om[9] && m2[10] === om[10] && m2[11] === om[11] && m2[12] === om[12] && m2[13] === om[13] && m2[14] === om[14] && m2[15] === om[15];
    }
    Matrix2.equals = equals;
    function clone(self2) {
        const result = create();
        copy(self2, result);
        return result;
    }
    Matrix2.clone = clone;
    function getHashCode(self2) {
        let hash = self2._m[0] || 0;
        for (let i = 1; i < 16; i++) {
            hash = hash * 397 ^ (self2._m[i] || 0);
        }
        return hash;
    }
    Matrix2.getHashCode = getHashCode;
    function decompose(self2, scale2, rotation, translation2) {
        if (self2.isIdentity) {
            if (translation2) {
                translation2 = Vector32.create(0, 0, 0);
            }
            if (scale2) {
                scale2 = Vector32.create(0, 0, 0);
            }
            if (rotation) {
                rotation = Quaternion.create(0, 0, 0, 1);
            }
            return true;
        }
        const m2 = self2._m;
        if (translation2) {
            translation2 = Vector32.create(m2[12], m2[13], m2[14]);
        }
        const usedScale = scale2 || Vector32.Zero();
        usedScale.x = Math.sqrt(m2[0] * m2[0] + m2[1] * m2[1] + m2[2] * m2[2]);
        usedScale.y = Math.sqrt(m2[4] * m2[4] + m2[5] * m2[5] + m2[6] * m2[6]);
        usedScale.z = Math.sqrt(m2[8] * m2[8] + m2[9] * m2[9] + m2[10] * m2[10]);
        if (determinant(self2) <= 0) {
            usedScale.y *= -1;
        }
        if (usedScale.x === 0 || usedScale.y === 0 || usedScale.z === 0) {
            if (rotation) {
                rotation = Quaternion.create(0, 0, 0, 1);
            }
            return false;
        }
        if (rotation) {
            const sx = 1 / usedScale.x, sy = 1 / usedScale.y, sz = 1 / usedScale.z;
            const tmpMatrix = create();
            fromValuesToRef(m2[0] * sx, m2[1] * sx, m2[2] * sx, 0, m2[4] * sy, m2[5] * sy, m2[6] * sy, 0, m2[8] * sz, m2[9] * sz, m2[10] * sz, 0, 0, 0, 0, 1, tmpMatrix);
            Quaternion.fromRotationMatrixToRef(tmpMatrix, rotation);
        }
        return true;
    }
    Matrix2.decompose = decompose;
    function setRowFromFloats(self2, index, x, y, z, w) {
        if (index < 0 || index > 3) {
            return;
        }
        const i = index * 4;
        self2._m[i + 0] = x;
        self2._m[i + 1] = y;
        self2._m[i + 2] = z;
        self2._m[i + 3] = w;
        _markAsUpdated(self2);
    }
    Matrix2.setRowFromFloats = setRowFromFloats;
    function scale(self2, scale2) {
        const result = create();
        scaleToRef(self2, scale2, result);
        return result;
    }
    Matrix2.scale = scale;
    function scaleToRef(self2, scale2, result) {
        for (let index = 0; index < 16; index++) {
            result._m[index] = self2._m[index] * scale2;
        }
        _markAsUpdated(result);
    }
    Matrix2.scaleToRef = scaleToRef;
    function scaleAndAddToRef(self2, scale2, result) {
        for (let index = 0; index < 16; index++) {
            result._m[index] += self2._m[index] * scale2;
        }
        _markAsUpdated(result);
    }
    Matrix2.scaleAndAddToRef = scaleAndAddToRef;
    function normalMatrixToRef(self2, ref) {
        const tmp = create();
        invertToRef(self2, tmp);
        transposeToRef(tmp, ref);
        const m2 = ref._m;
        fromValuesToRef(m2[0], m2[1], m2[2], 0, m2[4], m2[5], m2[6], 0, m2[8], m2[9], m2[10], 0, 0, 0, 0, 1, ref);
    }
    Matrix2.normalMatrixToRef = normalMatrixToRef;
    function getRotationMatrix(self2) {
        const result = create();
        getRotationMatrixToRef(self2, result);
        return result;
    }
    Matrix2.getRotationMatrix = getRotationMatrix;
    function getRotationMatrixToRef(self2, result) {
        const scale2 = Vector32.Zero();
        if (!decompose(self2, scale2)) {
            result = Identity();
            return;
        }
        const m2 = self2._m;
        const sx = 1 / scale2.x, sy = 1 / scale2.y, sz = 1 / scale2.z;
        fromValuesToRef(m2[0] * sx, m2[1] * sx, m2[2] * sx, 0, m2[4] * sy, m2[5] * sy, m2[6] * sy, 0, m2[8] * sz, m2[9] * sz, m2[10] * sz, 0, 0, 0, 0, 1, result);
    }
    Matrix2.getRotationMatrixToRef = getRotationMatrixToRef;
    function toggleModelMatrixHandInPlace(self2) {
        self2._m[2] *= -1;
        self2._m[6] *= -1;
        self2._m[8] *= -1;
        self2._m[9] *= -1;
        self2._m[14] *= -1;
        _markAsUpdated(self2);
    }
    Matrix2.toggleModelMatrixHandInPlace = toggleModelMatrixHandInPlace;
    function toggleProjectionMatrixHandInPlace(self2) {
        self2._m[8] *= -1;
        self2._m[9] *= -1;
        self2._m[10] *= -1;
        self2._m[11] *= -1;
        _markAsUpdated(self2);
    }
    Matrix2.toggleProjectionMatrixHandInPlace = toggleProjectionMatrixHandInPlace;
    function _updateIdentityStatus(self2, isIdentity, isIdentityDirty = false, isIdentity3x2 = false, isIdentity3x2Dirty = true) {
        self2.updateFlag = _updateFlagSeed++;
        self2.isIdentity = isIdentity;
        self2.isIdentity3x2 = isIdentity || isIdentity3x2;
        self2._isIdentityDirty = self2.isIdentity ? false : isIdentityDirty;
        self2._isIdentity3x2Dirty = self2.isIdentity3x2 ? false : isIdentity3x2Dirty;
    }
})(Matrix || (Matrix = {}));

// node_modules/@dcl/ecs-math/dist/Quaternion.js
var Quaternion;
(function(Quaternion2) {
    function create(x = 0, y = 0, z = 0, w = 1) {
        return { x, y, z, w };
    }
    Quaternion2.create = create;
    function add(q1, q2) {
        return { x: q1.x + q2.x, y: q1.y + q2.y, z: q1.z + q2.z, w: q1.w + q2.w };
    }
    Quaternion2.add = add;
    function fromRotationYawPitchRoll(yaw, pitch, roll) {
        const halfPitch = pitch * 0.5;
        const halfYaw = yaw * 0.5;
        const halfRoll = roll * 0.5;
        const c1 = Math.cos(halfPitch);
        const c2 = Math.cos(halfYaw);
        const c3 = Math.cos(halfRoll);
        const s1 = Math.sin(halfPitch);
        const s2 = Math.sin(halfYaw);
        const s3 = Math.sin(halfRoll);
        return create(c2 * s1 * c3 + s2 * c1 * s3, s2 * c1 * c3 - c2 * s1 * s3, c2 * c1 * s3 - s2 * s1 * c3, c2 * c1 * c3 + s2 * s1 * s3);
    }
    Quaternion2.fromRotationYawPitchRoll = fromRotationYawPitchRoll;
    function fromEulerDegrees(x, y, z) {
        return fromRotationYawPitchRoll(y * DEG2RAD, x * DEG2RAD, z * DEG2RAD);
    }
    Quaternion2.fromEulerDegrees = fromEulerDegrees;
    function length2(q) {
        return Math.sqrt(lengthSquared(q));
    }
    Quaternion2.length = length2;
    function lengthSquared(q) {
        return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    }
    Quaternion2.lengthSquared = lengthSquared;
    function dot(left, right) {
        return left.x * right.x + left.y * right.y + left.z * right.z + left.w * right.w;
    }
    Quaternion2.dot = dot;
    function angle(quat1, quat2) {
        const dotVal = dot(quat1, quat2);
        return Math.acos(Math.min(Math.abs(dotVal), 1)) * 2 * RAD2DEG;
    }
    Quaternion2.angle = angle;
    function rotateTowards(from, to, maxDegreesDelta) {
        const num = angle(from, to);
        if (num === 0) {
            return to;
        }
        const t = Math.min(1, maxDegreesDelta / num);
        return slerp(from, to, t);
    }
    Quaternion2.rotateTowards = rotateTowards;
    function lookRotation(forward, up = { x: 0, y: 1, z: 0 }) {
        const forwardNew = Vector32.normalize(forward);
        const right = Vector32.normalize(Vector32.cross(up, forwardNew));
        const upNew = Vector32.cross(forwardNew, right);
        const m00 = right.x;
        const m01 = right.y;
        const m02 = right.z;
        const m10 = upNew.x;
        const m11 = upNew.y;
        const m12 = upNew.z;
        const m20 = forwardNew.x;
        const m21 = forwardNew.y;
        const m22 = forwardNew.z;
        const num8 = m00 + m11 + m22;
        const quaternion = create();
        if (num8 > 0) {
            let num = Math.sqrt(num8 + 1);
            quaternion.w = num * 0.5;
            num = 0.5 / num;
            quaternion.x = (m12 - m21) * num;
            quaternion.y = (m20 - m02) * num;
            quaternion.z = (m01 - m10) * num;
            return quaternion;
        }
        if (m00 >= m11 && m00 >= m22) {
            const num7 = Math.sqrt(1 + m00 - m11 - m22);
            const num4 = 0.5 / num7;
            quaternion.x = 0.5 * num7;
            quaternion.y = (m01 + m10) * num4;
            quaternion.z = (m02 + m20) * num4;
            quaternion.w = (m12 - m21) * num4;
            return quaternion;
        }
        if (m11 > m22) {
            const num6 = Math.sqrt(1 + m11 - m00 - m22);
            const num3 = 0.5 / num6;
            quaternion.x = (m10 + m01) * num3;
            quaternion.y = 0.5 * num6;
            quaternion.z = (m21 + m12) * num3;
            quaternion.w = (m20 - m02) * num3;
            return quaternion;
        }
        const num5 = Math.sqrt(1 + m22 - m00 - m11);
        const num2 = 0.5 / num5;
        quaternion.x = (m20 + m02) * num2;
        quaternion.y = (m21 + m12) * num2;
        quaternion.z = 0.5 * num5;
        quaternion.w = (m01 - m10) * num2;
        return quaternion;
    }
    Quaternion2.lookRotation = lookRotation;
    function normalize(q) {
        const qLength = 1 / length2(q);
        return create(q.x * qLength, q.y * qLength, q.z * qLength, q.w * qLength);
    }
    Quaternion2.normalize = normalize;
    function fromToRotation(from, to, up = Vector32.Up()) {
        const v0 = Vector32.normalize(from);
        const v1 = Vector32.normalize(to);
        const a = Vector32.cross(v0, v1);
        const w = Math.sqrt(Vector32.lengthSquared(v0) * Vector32.lengthSquared(v1)) + Vector32.dot(v0, v1);
        if (Vector32.lengthSquared(a) < 1e-4) {
            return Math.abs(w) < 1e-4 ? normalize(create(up.x, up.y, up.z, 0)) : Identity();
        } else {
            return normalize(create(a.x, a.y, a.z, w));
        }
    }
    Quaternion2.fromToRotation = fromToRotation;
    function Identity() {
        return create(0, 0, 0, 1);
    }
    Quaternion2.Identity = Identity;
    function toEulerAngles(q) {
        const out = Vector32.create();
        const unit = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        const test = q.x * q.w - q.y * q.z;
        if (test > 0.4995 * unit) {
            out.x = Math.PI / 2;
            out.y = 2 * Math.atan2(q.y, q.x);
            out.z = 0;
        } else if (test < -0.4995 * unit) {
            out.x = -Math.PI / 2;
            out.y = -2 * Math.atan2(q.y, q.x);
            out.z = 0;
        } else {
            out.x = Math.asin(2 * (q.w * q.x - q.y * q.z));
            out.y = Math.atan2(2 * q.w * q.y + 2 * q.z * q.x, 1 - 2 * (q.x * q.x + q.y * q.y));
            out.z = Math.atan2(2 * q.w * q.z + 2 * q.x * q.y, 1 - 2 * (q.z * q.z + q.x * q.x));
        }
        out.x *= RAD2DEG;
        out.y *= RAD2DEG;
        out.z *= RAD2DEG;
        out.x = Scalar.repeat(out.x, 360);
        out.y = Scalar.repeat(out.y, 360);
        out.z = Scalar.repeat(out.z, 360);
        return out;
    }
    Quaternion2.toEulerAngles = toEulerAngles;
    function fromRotationYawPitchRollToRef(yaw, pitch, roll, result) {
        const halfPitch = pitch * 0.5;
        const halfYaw = yaw * 0.5;
        const halfRoll = roll * 0.5;
        const c1 = Math.cos(halfPitch);
        const c2 = Math.cos(halfYaw);
        const c3 = Math.cos(halfRoll);
        const s1 = Math.sin(halfPitch);
        const s2 = Math.sin(halfYaw);
        const s3 = Math.sin(halfRoll);
        result.x = c2 * s1 * c3 + s2 * c1 * s3;
        result.y = s2 * c1 * c3 - c2 * s1 * s3;
        result.z = c2 * c1 * s3 - s2 * s1 * c3;
        result.w = c2 * c1 * c3 + s2 * s1 * s3;
    }
    Quaternion2.fromRotationYawPitchRollToRef = fromRotationYawPitchRollToRef;
    function fromRotationMatrixToRef(matrix, result) {
        const data = matrix._m;
        const m11 = data[0], m12 = data[4], m13 = data[8];
        const m21 = data[1], m22 = data[5], m23 = data[9];
        const m31 = data[2], m32 = data[6], m33 = data[10];
        const trace = m11 + m22 + m33;
        let s;
        if (trace > 0) {
            s = 0.5 / Math.sqrt(trace + 1);
            result.w = 0.25 / s;
            result.x = (m32 - m23) * s;
            result.y = (m13 - m31) * s;
            result.z = (m21 - m12) * s;
        } else if (m11 > m22 && m11 > m33) {
            s = 2 * Math.sqrt(1 + m11 - m22 - m33);
            result.w = (m32 - m23) / s;
            result.x = 0.25 * s;
            result.y = (m12 + m21) / s;
            result.z = (m13 + m31) / s;
        } else if (m22 > m33) {
            s = 2 * Math.sqrt(1 + m22 - m11 - m33);
            result.w = (m13 - m31) / s;
            result.x = (m12 + m21) / s;
            result.y = 0.25 * s;
            result.z = (m23 + m32) / s;
        } else {
            s = 2 * Math.sqrt(1 + m33 - m11 - m22);
            result.w = (m21 - m12) / s;
            result.x = (m13 + m31) / s;
            result.y = (m23 + m32) / s;
            result.z = 0.25 * s;
        }
    }
    Quaternion2.fromRotationMatrixToRef = fromRotationMatrixToRef;
    function slerp(left, right, amount) {
        const result = Quaternion2.Identity();
        Quaternion2.slerpToRef(left, right, amount, result);
        return result;
    }
    Quaternion2.slerp = slerp;
    function slerpToRef(left, right, amount, result) {
        let num2;
        let num3;
        let num4 = left.x * right.x + left.y * right.y + left.z * right.z + left.w * right.w;
        let flag = false;
        if (num4 < 0) {
            flag = true;
            num4 = -num4;
        }
        if (num4 > 0.999999) {
            num3 = 1 - amount;
            num2 = flag ? -amount : amount;
        } else {
            const num5 = Math.acos(num4);
            const num6 = 1 / Math.sin(num5);
            num3 = Math.sin((1 - amount) * num5) * num6;
            num2 = flag ? -Math.sin(amount * num5) * num6 : Math.sin(amount * num5) * num6;
        }
        result.x = num3 * left.x + num2 * right.x;
        result.y = num3 * left.y + num2 * right.y;
        result.z = num3 * left.z + num2 * right.z;
        result.w = num3 * left.w + num2 * right.w;
    }
    Quaternion2.slerpToRef = slerpToRef;
    function multiply(self2, q1) {
        const result = create(0, 0, 0, 1);
        multiplyToRef(self2, q1, result);
        return result;
    }
    Quaternion2.multiply = multiply;
    function multiplyToRef(self2, q1, result) {
        result.x = self2.x * q1.w + self2.y * q1.z - self2.z * q1.y + self2.w * q1.x;
        result.y = -self2.x * q1.z + self2.y * q1.w + self2.z * q1.x + self2.w * q1.y;
        result.z = self2.x * q1.y - self2.y * q1.x + self2.z * q1.w + self2.w * q1.z;
        result.w = -self2.x * q1.x - self2.y * q1.y - self2.z * q1.z + self2.w * q1.w;
    }
    Quaternion2.multiplyToRef = multiplyToRef;
    function fromAngleAxis(degrees, axis) {
        if (Vector32.lengthSquared(axis) === 0) {
            return Quaternion2.Identity();
        }
        const result = Identity();
        let radians = degrees * DEG2RAD;
        radians *= 0.5;
        const a2 = Vector32.normalize(axis);
        Vector32.scaleToRef(a2, Math.sin(radians), a2);
        result.x = a2.x;
        result.y = a2.y;
        result.z = a2.z;
        result.w = Math.cos(radians);
        return normalize(result);
    }
    Quaternion2.fromAngleAxis = fromAngleAxis;
    function fromAxisToRotationQuaternion(axis1, axis2, axis3) {
        const quat = Quaternion2.create(0, 0, 0, 0);
        fromAxisToRotationQuaternionToRef(axis1, axis2, axis3, quat);
        return quat;
    }
    Quaternion2.fromAxisToRotationQuaternion = fromAxisToRotationQuaternion;
    function fromAxisToRotationQuaternionToRef(axis1, axis2, axis3, ref) {
        const rotMat = Matrix.create();
        Matrix.fromXYZAxesToRef(Vector32.normalize(axis1), Vector32.normalize(axis2), Vector32.normalize(axis3), rotMat);
        Quaternion2.fromRotationMatrixToRef(rotMat, ref);
    }
    Quaternion2.fromAxisToRotationQuaternionToRef = fromAxisToRotationQuaternionToRef;
    function Zero() {
        return create(0, 0, 0, 0);
    }
    Quaternion2.Zero = Zero;
    function fromLookAt(position, target, worldUp = Vector32.Up()) {
        const result = Quaternion2.Identity();
        fromLookAtToRef(position, target, worldUp, result);
        return result;
    }
    Quaternion2.fromLookAt = fromLookAt;
    function fromLookAtToRef(position, target, worldUp = Vector32.Up(), result) {
        const m = Matrix.Identity();
        Matrix.lookAtLHToRef(position, target, worldUp, m);
        Matrix.invertToRef(m, m);
        Quaternion2.fromRotationMatrixToRef(m, result);
    }
    Quaternion2.fromLookAtToRef = fromLookAtToRef;
})(Quaternion || (Quaternion = {}));

// node_modules/@dcl/ecs-math/dist/Color4.js
var Color42;
(function(Color43) {
    function create(r = 0, g = 0, b = 0, a = 1) {
        return { r, g, b, a };
    }
    Color43.create = create;
    function fromHexString(hex) {
        if (hex.substring(0, 1) !== "#" || hex.length !== 9) {
            return create(0, 0, 0, 0);
        }
        const r = parseInt(hex.substring(1, 3), 16);
        const g = parseInt(hex.substring(3, 5), 16);
        const b = parseInt(hex.substring(5, 7), 16);
        const a = parseInt(hex.substring(7, 9), 16);
        return Color43.fromInts(r, g, b, a);
    }
    Color43.fromHexString = fromHexString;
    function lerp(left, right, amount) {
        const result = create(0, 0, 0, 0);
        Color43.lerpToRef(left, right, amount, result);
        return result;
    }
    Color43.lerp = lerp;
    function lerpToRef(left, right, amount, result) {
        result.r = left.r + (right.r - left.r) * amount;
        result.g = left.g + (right.g - left.g) * amount;
        result.b = left.b + (right.b - left.b) * amount;
        result.a = left.a + (right.a - left.a) * amount;
    }
    Color43.lerpToRef = lerpToRef;
    function Red() {
        return create(1, 0, 0, 1);
    }
    Color43.Red = Red;
    function Green() {
        return create(0, 1, 0, 1);
    }
    Color43.Green = Green;
    function Blue() {
        return create(0, 0, 1, 1);
    }
    Color43.Blue = Blue;
    function Black() {
        return create(0, 0, 0, 1);
    }
    Color43.Black = Black;
    function White() {
        return create(1, 1, 1, 1);
    }
    Color43.White = White;
    function Purple() {
        return create(0.5, 0, 0.5, 1);
    }
    Color43.Purple = Purple;
    function Magenta() {
        return create(1, 0, 1, 1);
    }
    Color43.Magenta = Magenta;
    function Yellow() {
        return create(1, 1, 0, 1);
    }
    Color43.Yellow = Yellow;
    function Gray() {
        return create(0.5, 0.5, 0.5, 1);
    }
    Color43.Gray = Gray;
    function Teal() {
        return create(0, 1, 1, 1);
    }
    Color43.Teal = Teal;
    function Clear() {
        return create(0, 0, 0, 0);
    }
    Color43.Clear = Clear;
    function fromColor3(color3, alpha = 1) {
        return create(color3.r, color3.g, color3.b, alpha);
    }
    Color43.fromColor3 = fromColor3;
    function fromArray(array, offset = 0) {
        return create(array[offset], array[offset + 1], array[offset + 2], array[offset + 3]);
    }
    Color43.fromArray = fromArray;
    function fromInts(r, g, b, a) {
        return create(r / 255, g / 255, b / 255, a / 255);
    }
    Color43.fromInts = fromInts;
    function checkColors4(colors, count) {
        if (colors.length === count * 3) {
            const colors4 = [];
            for (let index = 0; index < colors.length; index += 3) {
                const newIndex = index / 3 * 4;
                colors4[newIndex] = colors[index];
                colors4[newIndex + 1] = colors[index + 1];
                colors4[newIndex + 2] = colors[index + 2];
                colors4[newIndex + 3] = 1;
            }
            return colors4;
        }
        return colors;
    }
    Color43.checkColors4 = checkColors4;
    function addToRef(a, b, ref) {
        ref.r = a.r + b.r;
        ref.g = a.g + b.g;
        ref.b = a.b + b.b;
        ref.a = a.a + b.a;
    }
    Color43.addToRef = addToRef;
    function toArray(value, array, index = 0) {
        array[index] = value.r;
        array[index + 1] = value.g;
        array[index + 2] = value.b;
        array[index + 3] = value.a;
    }
    Color43.toArray = toArray;
    function add(value, right) {
        const ret = Clear();
        addToRef(value, right, ret);
        return ret;
    }
    Color43.add = add;
    function subtract(value, right) {
        const ret = Clear();
        subtractToRef(value, right, ret);
        return ret;
    }
    Color43.subtract = subtract;
    function subtractToRef(a, b, result) {
        result.r = a.r - b.r;
        result.g = a.g - b.g;
        result.b = a.b - b.b;
        result.a = a.a - b.a;
    }
    Color43.subtractToRef = subtractToRef;
    function scale(value, scale2) {
        return create(value.r * scale2, value.g * scale2, value.b * scale2, value.a * scale2);
    }
    Color43.scale = scale;
    function scaleToRef(value, scale2, result) {
        result.r = value.r * scale2;
        result.g = value.g * scale2;
        result.b = value.b * scale2;
        result.a = value.a * scale2;
    }
    Color43.scaleToRef = scaleToRef;
    function scaleAndAddToRef(value, scale2, result) {
        result.r += value.r * scale2;
        result.g += value.g * scale2;
        result.b += value.b * scale2;
        result.a += value.a * scale2;
    }
    Color43.scaleAndAddToRef = scaleAndAddToRef;
    function clampToRef(value, min = 0, max = 1, result) {
        result.r = Scalar.clamp(value.r, min, max);
        result.g = Scalar.clamp(value.g, min, max);
        result.b = Scalar.clamp(value.b, min, max);
        result.a = Scalar.clamp(value.a, min, max);
    }
    Color43.clampToRef = clampToRef;
    function multiply(value, color) {
        return create(value.r * color.r, value.g * color.g, value.b * color.b, value.a * color.a);
    }
    Color43.multiply = multiply;
    function multiplyToRef(value, color, result) {
        result.r = value.r * color.r;
        result.g = value.g * color.g;
        result.b = value.b * color.b;
        result.a = value.a * color.a;
    }
    Color43.multiplyToRef = multiplyToRef;
    function toString(value) {
        return "{R: " + value.r + " G:" + value.g + " B:" + value.b + " A:" + value.a + "}";
    }
    Color43.toString = toString;
    function getHashCode(value) {
        let hash = value.r || 0;
        hash = hash * 397 ^ (value.g || 0);
        hash = hash * 397 ^ (value.b || 0);
        hash = hash * 397 ^ (value.a || 0);
        return hash;
    }
    Color43.getHashCode = getHashCode;
    function clone(value) {
        return create(value.r, value.g, value.b, value.a);
    }
    Color43.clone = clone;
    function copyFrom(source, dest) {
        dest.r = source.r;
        dest.g = source.g;
        dest.b = source.b;
        dest.a = source.a;
    }
    Color43.copyFrom = copyFrom;
    function copyFromFloats(r, g, b, a, dest) {
        dest.r = r;
        dest.g = g;
        dest.b = b;
        dest.a = a;
    }
    Color43.copyFromFloats = copyFromFloats;
    function set(r, g, b, a, dest) {
        dest.r = r;
        dest.g = g;
        dest.b = b;
        dest.a = a;
    }
    Color43.set = set;
    function toHexString(value) {
        const intR = value.r * 255 | 0;
        const intG = value.g * 255 | 0;
        const intB = value.b * 255 | 0;
        const intA = value.a * 255 | 0;
        return "#" + Scalar.toHex(intR) + Scalar.toHex(intG) + Scalar.toHex(intB) + Scalar.toHex(intA);
    }
    Color43.toHexString = toHexString;
    function toLinearSpace(value) {
        const convertedColor = create();
        toLinearSpaceToRef(value, convertedColor);
        return convertedColor;
    }
    Color43.toLinearSpace = toLinearSpace;
    function toLinearSpaceToRef(value, ref) {
        ref.r = Math.pow(value.r, ToLinearSpace);
        ref.g = Math.pow(value.g, ToLinearSpace);
        ref.b = Math.pow(value.b, ToLinearSpace);
        ref.a = value.a;
    }
    Color43.toLinearSpaceToRef = toLinearSpaceToRef;
    function toGammaSpace(value) {
        const convertedColor = create();
        toGammaSpaceToRef(value, convertedColor);
        return convertedColor;
    }
    Color43.toGammaSpace = toGammaSpace;
    function toGammaSpaceToRef(value, convertedColor) {
        convertedColor.r = Math.pow(value.r, ToGammaSpace);
        convertedColor.g = Math.pow(value.g, ToGammaSpace);
        convertedColor.b = Math.pow(value.b, ToGammaSpace);
        convertedColor.a = value.a;
    }
    Color43.toGammaSpaceToRef = toGammaSpaceToRef;
})(Color42 || (Color42 = {}));

// node_modules/@dcl/ecs-math/dist/Color3.js
var Color32;
(function(Color33) {
    function create(r = 0, g = 0, b = 0) {
        return { r, g, b };
    }
    Color33.create = create;
    function fromHexString(hex) {
        if (hex.substring(0, 1) !== "#" || hex.length !== 7) {
            return create(0, 0, 0);
        }
        const r = parseInt(hex.substring(1, 3), 16);
        const g = parseInt(hex.substring(3, 5), 16);
        const b = parseInt(hex.substring(5, 7), 16);
        return fromInts(r, g, b);
    }
    Color33.fromHexString = fromHexString;
    function fromArray(array, offset = 0) {
        return create(array[offset], array[offset + 1], array[offset + 2]);
    }
    Color33.fromArray = fromArray;
    function fromInts(r, g, b) {
        return create(r / 255, g / 255, b / 255);
    }
    Color33.fromInts = fromInts;
    function lerp(start, end, amount) {
        const result = create(0, 0, 0);
        Color33.lerpToRef(start, end, amount, result);
        return result;
    }
    Color33.lerp = lerp;
    function lerpToRef(left, right, amount, result) {
        result.r = left.r + (right.r - left.r) * amount;
        result.g = left.g + (right.g - left.g) * amount;
        result.b = left.b + (right.b - left.b) * amount;
    }
    Color33.lerpToRef = lerpToRef;
    function Red() {
        return create(1, 0, 0);
    }
    Color33.Red = Red;
    function Green() {
        return create(0, 1, 0);
    }
    Color33.Green = Green;
    function Blue() {
        return create(0, 0, 1);
    }
    Color33.Blue = Blue;
    function Black() {
        return create(0, 0, 0);
    }
    Color33.Black = Black;
    function White() {
        return create(1, 1, 1);
    }
    Color33.White = White;
    function Purple() {
        return create(0.5, 0, 0.5);
    }
    Color33.Purple = Purple;
    function Magenta() {
        return create(1, 0, 1);
    }
    Color33.Magenta = Magenta;
    function Yellow() {
        return create(1, 1, 0);
    }
    Color33.Yellow = Yellow;
    function Gray() {
        return create(0.5, 0.5, 0.5);
    }
    Color33.Gray = Gray;
    function Teal() {
        return create(0, 1, 1);
    }
    Color33.Teal = Teal;
    function Random() {
        return create(Math.random(), Math.random(), Math.random());
    }
    Color33.Random = Random;
    function toString(value) {
        return "{R: " + value.r + " G:" + value.g + " B:" + value.b + "}";
    }
    Color33.toString = toString;
    function getHashCode(value) {
        let hash = value.r || 0;
        hash = hash * 397 ^ (value.g || 0);
        hash = hash * 397 ^ (value.b || 0);
        return hash;
    }
    Color33.getHashCode = getHashCode;
    function toArray(value, array, index = 0) {
        array[index] = value.r;
        array[index + 1] = value.g;
        array[index + 2] = value.b;
    }
    Color33.toArray = toArray;
    function toColor4(value, alpha = 1) {
        return Color42.create(value.r, value.g, value.b, alpha);
    }
    Color33.toColor4 = toColor4;
    function asArray(value) {
        const result = new Array();
        toArray(value, result, 0);
        return result;
    }
    Color33.asArray = asArray;
    function toLuminance(value) {
        return value.r * 0.3 + value.g * 0.59 + value.b * 0.11;
    }
    Color33.toLuminance = toLuminance;
    function multiply(value, otherColor) {
        return create(value.r * otherColor.r, value.g * otherColor.g, value.b * otherColor.b);
    }
    Color33.multiply = multiply;
    function multiplyToRef(value, otherColor, result) {
        result.r = value.r * otherColor.r;
        result.g = value.g * otherColor.g;
        result.b = value.b * otherColor.b;
    }
    Color33.multiplyToRef = multiplyToRef;
    function equals(value, otherColor) {
        return otherColor && value.r === otherColor.r && value.g === otherColor.g && value.b === otherColor.b;
    }
    Color33.equals = equals;
    function equalsFloats(value, r, g, b) {
        return value.r === r && value.g === g && value.b === b;
    }
    Color33.equalsFloats = equalsFloats;
    function scale(value, scale2) {
        return create(value.r * scale2, value.g * scale2, value.b * scale2);
    }
    Color33.scale = scale;
    function scaleToRef(value, scale2, result) {
        result.r = value.r * scale2;
        result.g = value.g * scale2;
        result.b = value.b * scale2;
    }
    Color33.scaleToRef = scaleToRef;
    function scaleAndAddToRef(value, scale2, result) {
        result.r += value.r * scale2;
        result.g += value.g * scale2;
        result.b += value.b * scale2;
    }
    Color33.scaleAndAddToRef = scaleAndAddToRef;
    function clampToRef(value, min = 0, max = 1, result) {
        result.r = Scalar.clamp(value.r, min, max);
        result.g = Scalar.clamp(value.g, min, max);
        result.b = Scalar.clamp(value.b, min, max);
    }
    Color33.clampToRef = clampToRef;
    function clamp(value, min = 0, max = 1) {
        const result = Color33.Black();
        clampToRef(value, min, max, result);
        return result;
    }
    Color33.clamp = clamp;
    function add(value, otherColor) {
        return create(value.r + otherColor.r, value.g + otherColor.g, value.b + otherColor.b);
    }
    Color33.add = add;
    function addToRef(value, otherColor, result) {
        result.r = value.r + otherColor.r;
        result.g = value.g + otherColor.g;
        result.b = value.b + otherColor.b;
    }
    Color33.addToRef = addToRef;
    function subtract(value, otherColor) {
        return create(value.r - otherColor.r, value.g - otherColor.g, value.b - otherColor.b);
    }
    Color33.subtract = subtract;
    function subtractToRef(value, otherColor, result) {
        result.r = value.r - otherColor.r;
        result.g = value.g - otherColor.g;
        result.b = value.b - otherColor.b;
    }
    Color33.subtractToRef = subtractToRef;
    function clone(value) {
        return create(value.r, value.g, value.b);
    }
    Color33.clone = clone;
    function copyFrom(source, dest) {
        dest.r = source.r;
        dest.g = source.g;
        dest.b = source.b;
    }
    Color33.copyFrom = copyFrom;
    function set(dest, r, g, b) {
        dest.r = r;
        dest.g = g;
        dest.b = b;
    }
    Color33.set = set;
    function toHexString(value) {
        const intR = value.r * 255 | 0;
        const intG = value.g * 255 | 0;
        const intB = value.b * 255 | 0;
        return "#" + Scalar.toHex(intR) + Scalar.toHex(intG) + Scalar.toHex(intB);
    }
    Color33.toHexString = toHexString;
    function toLinearSpace(value) {
        const convertedColor = create();
        toLinearSpaceToRef(value, convertedColor);
        return convertedColor;
    }
    Color33.toLinearSpace = toLinearSpace;
    function toLinearSpaceToRef(value, convertedColor) {
        convertedColor.r = Math.pow(value.r, ToLinearSpace);
        convertedColor.g = Math.pow(value.g, ToLinearSpace);
        convertedColor.b = Math.pow(value.b, ToLinearSpace);
    }
    Color33.toLinearSpaceToRef = toLinearSpaceToRef;
    function toGammaSpace(value) {
        const convertedColor = create();
        toGammaSpaceToRef(value, convertedColor);
        return convertedColor;
    }
    Color33.toGammaSpace = toGammaSpace;
    function toGammaSpaceToRef(value, convertedColor) {
        convertedColor.r = Math.pow(value.r, ToGammaSpace);
        convertedColor.g = Math.pow(value.g, ToGammaSpace);
        convertedColor.b = Math.pow(value.b, ToGammaSpace);
    }
    Color33.toGammaSpaceToRef = toGammaSpaceToRef;
})(Color32 || (Color32 = {}));

// src/components.ts
var Cube = engine.defineComponent("cube-id", {});

// src/factory.ts
function createCube(x, y, z, spawner = true) {
    const meshEntity = engine.addEntity();
    Cube.create(meshEntity);
    Transform2.create(meshEntity, { position: { x, y, z } });
    MeshRenderer3.setBox(meshEntity);
    MeshCollider3.setBox(meshEntity);
    if (spawner) {
        PointerEvents2.create(meshEntity, {
            pointerEvents: [
                {
                    eventType: PointerEventType.PET_DOWN,
                    eventInfo: {
                        button: InputAction.IA_PRIMARY,
                        hoverText: "Press E to spawn",
                        maxDistance: 100,
                        showFeedback: true
                    }
                }
            ]
        });
    }
    return meshEntity;
}

// src/systems.ts
var BounceScaling = engine.defineComponent("BounceScaling", { t: Schemas.Number });
function circularSystem(dt) {
    const entitiesWithMeshRenderer = engine.getEntitiesWith(MeshRenderer3, Transform2);
    for (const [entity, _meshRenderer, _transform] of entitiesWithMeshRenderer) {
        const mutableTransform = Transform2.getMutable(entity);
        mutableTransform.rotation = Quaternion.multiply(
            mutableTransform.rotation,
            Quaternion.fromAngleAxis(dt * 10, Vector32.Up())
        );
    }
}
function spawnerSystem() {
    const clickedCubes = engine.getEntitiesWith(PointerEvents2);
    for (const [entity] of clickedCubes) {
        if (inputSystem.isTriggered(InputAction.IA_PRIMARY, PointerEventType.PET_DOWN, entity)) {
            createCube(1 + Math.random() * 8, Math.random() * 8, 1 + Math.random() * 8, false);
            BounceScaling.createOrReplace(entity);
        }
    }
}
function bounceScalingSystem(dt) {
    const clickedCubes = engine.getEntitiesWith(BounceScaling, Transform2);
    for (const [entity] of clickedCubes) {
        const m = BounceScaling.getMutable(entity);
        m.t += dt;
        if (m.t > 5) {
            Transform2.getMutable(entity).scale = Vector32.One();
            BounceScaling.deleteFrom(entity);
        } else {
            const factor = 0.9 + 0.2 * Math.exp(-1.5 * m.t) * Math.sin(10 * m.t);
            Transform2.getMutable(entity).scale = Vector32.scale(Vector32.One(), factor);
        }
    }
}

// node_modules/@dcl/sdk/index.js
var import_EngineApi2 = require("~system/EngineApi");

// node_modules/@dcl/sdk/internal/transports/rendererTransport.js
function createRendererTransport(engineApi) {
    async function sendToRenderer(message) {
        const response = await engineApi.crdtSendToRenderer({
            data: new Uint8Array(message)
        });
        if (response && response.data && response.data.length) {
            if (rendererTransport2.onmessage) {
                for (const byteArray of response.data) {
                    rendererTransport2.onmessage(byteArray);
                }
            }
        }
    }
    const rendererTransport2 = {
        async send(message) {
            try {
                await sendToRenderer(message);
            } catch (error) {
                console.error(error);
                debugger;
            }
        },
        filter(message) {
            if (message.componentId > MAX_STATIC_COMPONENT) {
                return false;
            }
            return !!message;
        }
    };
    return rendererTransport2;
}

// node_modules/@dcl/sdk/internal/Observable.js
var ObserverEventState = class {
    constructor(mask, skipNextObservers = false, target, currentTarget) {
        this.initalize(mask, skipNextObservers, target, currentTarget);
    }
    initalize(mask, skipNextObservers = false, target, currentTarget) {
        this.mask = mask;
        this.skipNextObservers = skipNextObservers;
        this.target = target;
        this.currentTarget = currentTarget;
        return this;
    }
};
var Observer = class {
    constructor(callback, mask, scope = null) {
        this.callback = callback;
        this.mask = mask;
        this.scope = scope;
        this.unregisterOnNextCall = false;
        this._willBeUnregistered = false;
    }
};
var Observable = class {
    constructor(onObserverAdded) {
        this._observers = new Array();
        this._onObserverAdded = null;
        this._eventState = new ObserverEventState(0);
        if (onObserverAdded) {
            this._onObserverAdded = onObserverAdded;
        }
    }
    add(callback, mask = -1, insertFirst = false, scope = null, unregisterOnFirstCall = false) {
        if (!callback) {
            return null;
        }
        const observer = new Observer(callback, mask, scope);
        observer.unregisterOnNextCall = unregisterOnFirstCall;
        if (insertFirst) {
            this._observers.unshift(observer);
        } else {
            this._observers.push(observer);
        }
        if (this._onObserverAdded) {
            this._onObserverAdded(observer);
        }
        return observer;
    }
    addOnce(callback) {
        return this.add(callback, void 0, void 0, void 0, true);
    }
    remove(observer) {
        if (!observer) {
            return false;
        }
        const index = this._observers.indexOf(observer);
        if (index !== -1) {
            this._deferUnregister(observer);
            return true;
        }
        return false;
    }
    removeCallback(callback, scope) {
        for (let index = 0; index < this._observers.length; index++) {
            if (this._observers[index].callback === callback && (!scope || scope === this._observers[index].scope)) {
                this._deferUnregister(this._observers[index]);
                return true;
            }
        }
        return false;
    }
    notifyObservers(eventData, mask = -1, target, currentTarget) {
        if (!this._observers.length) {
            return true;
        }
        const state = this._eventState;
        state.mask = mask;
        state.target = target;
        state.currentTarget = currentTarget;
        state.skipNextObservers = false;
        state.lastReturnValue = eventData;
        for (const obs of this._observers) {
            if (obs._willBeUnregistered) {
                continue;
            }
            if (obs.mask & mask) {
                if (obs.scope) {
                    state.lastReturnValue = obs.callback.apply(obs.scope, [eventData, state]);
                } else {
                    state.lastReturnValue = obs.callback(eventData, state);
                }
                if (obs.unregisterOnNextCall) {
                    this._deferUnregister(obs);
                }
            }
            if (state.skipNextObservers) {
                return false;
            }
        }
        return true;
    }
    notifyObserversWithPromise(eventData, mask = -1, target, currentTarget) {
        let p = Promise.resolve(eventData);
        if (!this._observers.length) {
            return p;
        }
        const state = this._eventState;
        state.mask = mask;
        state.target = target;
        state.currentTarget = currentTarget;
        state.skipNextObservers = false;
        this._observers.forEach((obs) => {
            if (state.skipNextObservers) {
                return;
            }
            if (obs._willBeUnregistered) {
                return;
            }
            if (obs.mask & mask) {
                if (obs.scope) {
                    p = p.then((lastReturnedValue) => {
                        state.lastReturnValue = lastReturnedValue;
                        return obs.callback.apply(obs.scope, [eventData, state]);
                    });
                } else {
                    p = p.then((lastReturnedValue) => {
                        state.lastReturnValue = lastReturnedValue;
                        return obs.callback(eventData, state);
                    });
                }
                if (obs.unregisterOnNextCall) {
                    this._deferUnregister(obs);
                }
            }
        });
        return p.then(() => {
            return eventData;
        });
    }
    notifyObserver(observer, eventData, mask = -1) {
        const state = this._eventState;
        state.mask = mask;
        state.skipNextObservers = false;
        observer.callback(eventData, state);
    }
    hasObservers() {
        return this._observers.length > 0;
    }
    clear() {
        this._observers = new Array();
        this._onObserverAdded = null;
    }
    clone() {
        const result = new Observable();
        result._observers = this._observers.slice(0);
        return result;
    }
    hasSpecificMask(mask = -1) {
        for (const obs of this._observers) {
            if (obs.mask & mask || obs.mask === mask) {
                return true;
            }
        }
        return false;
    }
    _deferUnregister(observer) {
        observer.unregisterOnNextCall = false;
        observer._willBeUnregistered = true;
        Promise.resolve().then.bind(Promise.resolve())(async () => this._remove(observer)).catch(console.error);
    }
    _remove(observer) {
        if (!observer) {
            return false;
        }
        const index = this._observers.indexOf(observer);
        if (index !== -1) {
            this._observers.splice(index, 1);
            return true;
        }
        return false;
    }
};

// node_modules/@dcl/sdk/observables.js
var import_EngineApi = require("~system/EngineApi");
var subscribeFunction = import_EngineApi.subscribe;
function createSubscriber(eventName) {
    return () => {
        subscribeFunction({ eventId: eventName }).catch(console.error);
    };
}
var onEnterSceneObservable = new Observable(createSubscriber("onEnterScene"));
var onLeaveSceneObservable = new Observable(createSubscriber("onLeaveScene"));
var onSceneReadyObservable = new Observable(createSubscriber("sceneStart"));
var onPlayerExpressionObservable = new Observable(createSubscriber("playerExpression"));
var onVideoEvent = new Observable(createSubscriber("videoEvent"));
var onProfileChanged = new Observable(createSubscriber("profileChanged"));
var onPlayerConnectedObservable = new Observable(createSubscriber("playerConnected"));
var onPlayerDisconnectedObservable = new Observable(createSubscriber("playerDisconnected"));
var onRealmChangedObservable = new Observable(createSubscriber("onRealmChanged"));
var onPlayerClickedObservable = new Observable(createSubscriber("playerClicked"));
var onCommsMessage = new Observable(createSubscriber("comms"));
async function pollEvents(sendBatch2) {
    const { events } = await sendBatch2({ actions: [] });
    for (const e of events) {
        if (e.generic) {
            const data = JSON.parse(e.generic.eventData);
            switch (e.generic.eventId) {
                case "onEnterScene": {
                    onEnterSceneObservable.notifyObservers(data);
                    break;
                }
                case "onLeaveScene": {
                    onLeaveSceneObservable.notifyObservers(data);
                    break;
                }
                case "sceneStart": {
                    onSceneReadyObservable.notifyObservers(data);
                    break;
                }
                case "playerExpression": {
                    onPlayerExpressionObservable.notifyObservers(data);
                    break;
                }
                case "videoEvent": {
                    const videoData = data;
                    onVideoEvent.notifyObservers(videoData);
                    break;
                }
                case "profileChanged": {
                    onProfileChanged.notifyObservers(data);
                    break;
                }
                case "playerConnected": {
                    onPlayerConnectedObservable.notifyObservers(data);
                    break;
                }
                case "playerDisconnected": {
                    onPlayerDisconnectedObservable.notifyObservers(data);
                    break;
                }
                case "onRealmChanged": {
                    onRealmChangedObservable.notifyObservers(data);
                    break;
                }
                case "playerClicked": {
                    onPlayerClickedObservable.notifyObservers(data);
                    break;
                }
                case "comms": {
                    onCommsMessage.notifyObservers(data);
                    break;
                }
            }
        }
    }
}

// node_modules/@dcl/sdk/index.js
var rendererTransport = createRendererTransport({ crdtSendToRenderer: import_EngineApi2.crdtSendToRenderer });
engine.addTransport(rendererTransport);
async function onUpdate(deltaTime) {
    await engine.update(deltaTime);
    await pollEvents(import_EngineApi2.sendBatch);
}
async function onStart() {
    await engine.seal();
    const response = await (0, import_EngineApi2.crdtGetState)({ data: new Uint8Array() });
    if (!!rendererTransport.onmessage) {
        if (response && response.data && response.data.length) {
            for (const byteArray of response.data) {
                rendererTransport.onmessage(byteArray);
            }
        }
    }
}

// src/index.ts
engine.addSystem(circularSystem);
engine.addSystem(spawnerSystem);
engine.addSystem(bounceScalingSystem);
executeTask(async function() {
    const cube = createCube(-2, 1, -2);
    Material3.setPbrMaterial(cube, { albedoColor: Color42.create(1, 0, 0.42) });
    for (let x = 0.5; x < 16; x += 1) {
        for (let y = 0.5; y < 16; y += 1) {
            createCube(x, 0, y, false);
        }
    }
});
var hoverState = 0;
engine.addSystem(function CircleHoverSystem(dt) {
    hoverState += Math.PI * dt * 0.5;
    const entitiesWithBoxShapes = engine.getEntitiesWith(MeshRenderer3, Transform2);
    for (const [entity] of entitiesWithBoxShapes) {
        const transform = Transform2.getMutable(entity);
        transform.position.y = Math.cos(
            hoverState + Math.sqrt(Math.pow(transform.position.x - 8, 2) + Math.pow(transform.position.z - 8, 2)) / Math.PI
        ) * 2 + 2;
    }
});