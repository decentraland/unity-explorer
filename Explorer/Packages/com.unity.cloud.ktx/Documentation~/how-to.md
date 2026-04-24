
# How To

The API provides the loading classes [KtxTexture](xref:KtxUnity.KtxTexture) for [KTX&trade; 2.0][ktx] files and [BasisUniversalTexture](xref:KtxUnity.BasisUniversalTexture) for [Basis Universal][basisu] files, which both offer the following async loading methods:

- [LoadFromUrl](xref:KtxUnity.TextureBase.LoadFromUrl*) for loading URLs (including file URLs starting with `file://`)
- [LoadFromStreamingAssets](xref:KtxUnity.TextureBase.LoadFromStreamingAssets*) for loading relative paths in the StreamingAssets folder
- [LoadFromBytes](xref:KtxUnity.TextureBase.LoadFromBytes*) for loading from memory

## Loading Textures

```C#
using KtxUnity;
…
async void Start() {

    // Create KTX texture instance
    var texture = new KtxTexture();

    // Linear color sampling. Needed for non-color value textures (e.g. normal maps)
    bool linearColor = true;

    // Load file from Streaming Assets folder (relative path)
    var result = await texture.LoadFromStreamingAssets("trout.ktx", linearColor);

    // Alternative: Load from URL
    // var result = await texture.LoadFromUrl("https://myserver.com/trout.ktx", linearColor);

    // Alternative: Load from memory
    // var result = await texture.LoadFromBytes(nativeArray, linearColor);

    if (result != null) {
        // Use texture. For example, apply texture to a material
        targetMaterial.mainTexture = result.texture;

        // Optional: Support arbitrary texture orientation by flipping the texture if necessary
        var scale = targetMaterial.mainTextureScale;
        scale.x = result.orientation.IsXFlipped() ? -1 : 1;
        scale.y = result.orientation.IsYFlipped() ? -1 : 1;
        targetMaterial.mainTextureScale = scale;
    }
}
…
```

## Using as Sprite

If you want to use the texture in a UI / Sprite context, this is how you create a Sprite with correct orientation:

```C#
using KtxUnity;
…
async void Start() {

    // Create a basis universal texture instance
    var texture = new BasisUniversalTexture();

    // Load file from Streaming Assets folder
    var result = await texture.LoadFromStreamingAssets("dachstein.basis");

    if (result != null) {
        // Calculate correct size
        var pos = new Vector2(0,0);
        var size = new Vector2(result.texture.width, result.texture.height);

        // Flip Sprite, if required
        if(result.orientation.IsXFlipped()) {
            pos.x = size.x;
            size.x *= -1;
        }

        if(result.orientation.IsYFlipped()) {
            pos.y = size.y;
            size.y *= -1;
        }

        // Create a Sprite and assign it to the Image
        GetComponent<Image>().sprite = Sprite.Create(result.texture, new Rect(pos, size), Vector2.zero);

        // Preserve aspect ratio:
        // Flipping the sprite by making the size x or y negative (above) breaks Image's `Preserve Aspect` feature
        // You can/have to calculate the RectTransform size yourself. Example:

        // Calculate correct size and assign it to the RectTransform
        const float scale = 0.5f; // Set this to whatever size you need it - best make it a serialized class field
        var rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(result.texture.width*scale, result.texture.height*scale);
    }
}
…
```

> Note: You can still use the `Preserve Aspect` Image option, if you encode your KTX/Basis files with flipped Y axis (see [Creating Textures](./creating-textures.md) )

## Advanced

Developers who want to create advanced loading code should look into classes [KtxTexture](xref:KtxUnity.KtxTexture), [BasisUniversalTexture](xref:KtxUnity.BasisUniversalTexture) and [TextureBase](xref:KtxUnity.TextureBase) directly.

When loading many textures at once, using the low-level API to get finer control over the loading process can yield great performance gains. Have a look at [Open](xref:KtxUnity.TextureBase.Open*), [LoadTexture2D](xref:KtxUnity.TextureBase.LoadTexture2D*) and [Dispose](xref:KtxUnity.TextureBase.Dispose) for details.

## Trademarks

*Unity* is a registered trademark of [Unity Technologies][unity].

Khronos&reg; and the Khronos Group logo are registered trademarks of the [The Khronos Group Inc][khronos].

KTX&trade; and the KTX logo are trademarks of the [The Khronos Group Inc][khronos].

[basisu]: https://github.com/BinomialLLC/basis_universal
[khronos]: https://www.khronos.org
[ktx]: https://www.khronos.org/ktx/
[unity]: https://unity.com
