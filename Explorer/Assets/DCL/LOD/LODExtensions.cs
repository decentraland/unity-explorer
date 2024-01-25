namespace DCL.LOD
{
    public static class LODExtensions
    {
        public static void TryRelease(this in LODAsset? asset, ILODAssetsPool lodAssetsPool)
        {
            if (asset == null) return;

            LODAsset value = asset.Value;
            lodAssetsPool.Release(value.LodKey, value);
        }
    }
}
