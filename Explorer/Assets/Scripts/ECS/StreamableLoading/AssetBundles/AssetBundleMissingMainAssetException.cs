using System;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Indicates that a given source does not contain root assets of the specified type (and thus can't be used further)
    /// </summary>
    public class AssetBundleMissingMainAssetException : Exception
    {
        private readonly string sourceName;
        private readonly Type type;

        public AssetBundleMissingMainAssetException(string sourceName, Type type) : base($"{sourceName} doesn't contain assets with {type} extensions")
        {
            this.sourceName = sourceName;
            this.type = type;
        }

        public override string ToString() =>
            $"{sourceName} doesn't contain assets with {type} extensions";
    }
}
