using GLTFast.Loading;
using System;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    /// Provides a mechanism to inspect the progress and result of a GLTF asset download or access request
    ///
    /// Note: This was changed from struct to class to avoid boxing both in the client and the GLTF plugin usage
    /// </summary>
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
            Data = Array.Empty<byte>();
            if (!string.IsNullOrEmpty(Text))
                Text = string.Empty;
        }
    }
}
