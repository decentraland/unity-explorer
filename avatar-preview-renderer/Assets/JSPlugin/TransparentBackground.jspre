document.getElementsByTagName("canvas")[0].style.background = "transparent";

const origConfigure = GPUCanvasContext.prototype.configure;
GPUCanvasContext.prototype.configure = function(desc) {
    // Always force transparency
    const cfg = Object.assign({}, desc, { alphaMode: 'premultiplied' });
    return origConfigure.call(this, cfg);
};