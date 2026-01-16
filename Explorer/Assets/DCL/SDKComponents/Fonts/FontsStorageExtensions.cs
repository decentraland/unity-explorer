namespace DCL.SDKComponents.Fonts
{
    public static class FontsStorageExtensions
    {
        public static IFontsStorage AsCached(this IFontsStorage origin) =>
            new CachedFontsStorage(origin);
    }
}
