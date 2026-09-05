namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public readonly struct TextureArrayMapping
    {
        public readonly TextureArrayHandler Handler;

        /// <summary>
        ///     Texture name from the original material
        /// </summary>
        public readonly int OriginalTextureID;
        public readonly int DefaultFallbackResolution;

        public TextureArrayMapping(TextureArrayHandler handler, int originalTextureID, int defaultFallbackResolution)
        {
            Handler = handler;
            OriginalTextureID = originalTextureID;
            DefaultFallbackResolution = defaultFallbackResolution;
        }
    }
}
