using GLTFast.Loading;

namespace ECS.StreamableLoading.GLTF
{
    public struct GltfDownloadResult : IDownload
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public byte[] Data { get; set; }
        public string Text { get; set; }
        public bool? IsBinary => GltfValidator.IsGltfBinaryFormat(Data);

        public void Dispose()
        {
            Data = null!;
        }
    }
}
