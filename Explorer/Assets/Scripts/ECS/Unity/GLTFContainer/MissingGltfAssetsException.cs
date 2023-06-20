using System;

namespace ECS.Unity.GLTFContainer
{
    /// <summary>
    ///     Indicates that a given source does not contain GLTF assets (and thus can't be used)
    /// </summary>
    public class MissingGltfAssetsException : Exception
    {
        private readonly string sourceName;

        public MissingGltfAssetsException(string sourceName)
        {
            this.sourceName = sourceName;
        }

        public override string ToString() =>
            $"{sourceName} doesn't contain assets with gltf extensions";
    }
}
