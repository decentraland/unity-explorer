using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Loading;
using UnityEngine;
using UnityEngine.Networking;

namespace Loading
{
    /// <summary>
    /// Default <see cref="IDownloadProvider"/> implementation
    /// </summary>
    public class BinaryDownloadProvider : IDownloadProvider
    {
        private readonly Dictionary<string, string> _content;

        public BinaryDownloadProvider(Dictionary<string, string> content)
        {
            _content = content;
        }

        /// <summary>
        /// Sends a URI request and waits for its completion.
        /// </summary>
        /// <param name="url">URI to request</param>
        /// <returns>Object representing the request</returns>
        public async Task<IDownload> RequestAsync(Uri url)
        {
            var req = new AwaitableDownload(url);
            await req.WaitAsync();
            return req;
        }

        /// <summary>
        /// Sends a URI request to load a texture
        /// </summary>
        /// <param name="url">URI to request</param>
        /// <param name="nonReadable">If true, resulting texture is not CPU readable (uses less memory)</param>
        /// <returns>Object representing the request</returns>
        public async Task<ITextureDownload> RequestTextureAsync(Uri url, bool nonReadable, bool forceLinear)
        {
            var fileName = Path.GetFileName(url.LocalPath);
            var file = _content[fileName];
            Debug.Log($"Requesting texture: {fileName} from: {file}");

            var req = new AwaitableTextureDownload(new Uri(file), nonReadable);
            await req.WaitAsync();
            return req;
        }
    }


    /// <summary>
    /// Default <see cref="IDownload"/> implementation that loads URIs via <see cref="UnityWebRequest"/>
    /// </summary>
    public class AwaitableDownload : IDownload
    {
        const string k_MimeTypeGltfBinary = "model/gltf-binary";
        const string k_MimeTypeGltf = "model/gltf+json";

        /// <summary>
        /// <see cref="UnityWebRequest"/> that is used for the download
        /// </summary>
        protected UnityWebRequest m_Request;

        /// <summary>
        /// The download's <see cref="UnityWebRequestAsyncOperation"/>
        /// </summary>
        protected UnityWebRequestAsyncOperation m_AsyncOperation;

        /// <summary>
        /// Empty constructor
        /// </summary>
        protected AwaitableDownload()
        {
        }

        /// <summary>
        /// Creates a download of a URI
        /// </summary>
        /// <param name="url">URI to request</param>
        public AwaitableDownload(Uri url)
        {
            Init(url);
        }

        void Init(Uri url)
        {
            m_Request = UnityWebRequest.Get(url);
            m_AsyncOperation = m_Request.SendWebRequest();
        }

        /// <summary>
        /// Waits until the URI request is completed.
        /// </summary>
        /// <returns>A task that represents the completion of the download</returns>
        public async Task WaitAsync()
        {
            while (!m_AsyncOperation.isDone)
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// True if the download finished and was successful
        /// </summary>
        public bool Success =>
            m_Request != null && m_Request.isDone && m_Request.result == UnityWebRequest.Result.Success;

        /// <summary>
        /// If the download failed, error description
        /// </summary>
        public string Error => m_Request == null ? "Request disposed" : m_Request.error;

        /// <summary>
        /// Downloaded data as byte array
        /// </summary>
        public byte[] Data => m_Request?.downloadHandler.data;

        /// <summary>
        /// Downloaded data as string
        /// </summary>
        public string Text => m_Request?.downloadHandler.text;

        /// <summary>
        /// True if the requested download is a glTF-Binary file.
        /// False if it is a regular JSON-based glTF file.
        /// Null if the type could not be determined.
        /// </summary>
        public bool? IsBinary
        {
            get
            {
                if (Success)
                {
                    return Data.Length >= 4 &&
                           Data[0] == 0x67 && // 'g'
                           Data[1] == 0x6C && // 'l'
                           Data[2] == 0x54 && // 'T'
                           Data[3] == 0x46; // 'F'
                }

                return null;
            }
        }

        /// <summary>
        /// Releases previously allocated resources.
        /// </summary>
        public void Dispose()
        {
            m_Request.Dispose();
            m_Request = null;
        }
    }

    public class AwaitableTextureDownload : AwaitableDownload, ITextureDownload
    {
        /// <summary>
        /// Parameter-less constructor, required for inheritance.
        /// </summary>
        protected AwaitableTextureDownload()
        {
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="url">Texture URI to request</param>
        /// <param name="nonReadable">If true, resulting texture is not CPU readable (uses less memory)</param>
        public AwaitableTextureDownload(Uri url, bool nonReadable)
        {
            Init(url, nonReadable);
        }

        /// <summary>
        /// Generates the UnityWebRequest used for sending the request.
        /// </summary>
        /// <param name="url">Texture URI to request</param>
        /// <param name="nonReadable">If true, resulting texture is not CPU readable (uses less memory)</param>
        /// <returns>UnityWebRequest used for sending the request</returns>
        protected static UnityWebRequest CreateRequest(Uri url, bool nonReadable)
        {
            return UnityWebRequestTexture.GetTexture(url, nonReadable);
        }

        void Init(Uri url, bool nonReadable)
        {
            m_Request = CreateRequest(url, nonReadable);
            m_AsyncOperation = m_Request.SendWebRequest();
        }

        /// <inheritdoc />
        public IDisposableTexture GetTexture(bool linear)
        {
            return (m_Request?.downloadHandler as DownloadHandlerTexture)?.texture.ToDisposableTexture();
        }
    }

    internal static class DisposableTextureExtensions
    {
        internal static IDisposableTexture ToDisposableTexture(this Texture2D texture2D) =>
            new NonReusableTexture(texture2D);
    }
}