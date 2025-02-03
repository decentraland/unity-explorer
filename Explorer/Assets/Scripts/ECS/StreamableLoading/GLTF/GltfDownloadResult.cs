using GLTFast.Loading;

namespace ECS.StreamableLoading.GLTF
{
    public class GltfDownloadResult : IDownload
    {
        public GltfDownloadResult(byte[] data, string text, string? error, bool success)
        {
            Data = data;
            Text = text;
            Error = error;
            Success = success;
        }

        public bool Success { get; set; }
        public string? Error { get; set; }
        public byte[] Data { get; set; }
        public string Text { get; set; }
        public bool? IsBinary => GltfValidator.IsGltfBinaryFormat(Data);

        public void Dispose()
        {
            // Ignore
        }
    }
}
