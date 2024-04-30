using Cysharp.Threading.Tasks;
using DCL.Profiling;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Representation of the created web request dedicated to download an audio clip
    /// </summary>
    public readonly struct GetAudioClipWebRequest : ITypedWebRequest
    {
        private readonly string url;

        public UnityWebRequest UnityWebRequest { get; }

        private GetAudioClipWebRequest(UnityWebRequest unityWebRequest, string url)
        {
            this.url = url;
            UnityWebRequest = unityWebRequest;
        }

        public struct CreateAudioClipOp : IWebRequestOp<GetAudioClipWebRequest>
        {
            public AudioClip Clip { get; private set; }

            /// <summary>
            ///     Creates the audio clip
            /// </summary>
            public UniTask ExecuteAsync(GetAudioClipWebRequest webRequest, CancellationToken ct)
            {
                UnityWebRequest unityWebRequest = webRequest.UnityWebRequest;

                // files bigger than 1MB will be treated as streaming
                if (unityWebRequest.downloadedBytes > 1000000)
                    ((DownloadHandlerAudioClip)unityWebRequest.downloadHandler).streamAudio = true;

                Clip = DownloadHandlerAudioClip.GetContent(unityWebRequest);

                unityWebRequest.Dispose();

                Clip.SetDebugName(webRequest.url);
                ProfilingCounters.AudioClipsAmount.Value++;

                return UniTask.CompletedTask;
            }
        }

        internal static GetAudioClipWebRequest Initialize(in CommonArguments commonArguments, GetAudioClipArguments audioClipArguments)
        {
            UnityWebRequest wr = UnityWebRequestMultimedia.GetAudioClip(commonArguments.URL, audioClipArguments.AudioType);
            return new GetAudioClipWebRequest(wr, commonArguments.URL);
        }
    }
}
