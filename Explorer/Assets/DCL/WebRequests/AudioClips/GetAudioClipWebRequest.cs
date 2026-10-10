using Cysharp.Threading.Tasks;
using DCL.Profiling;
using System;
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

        public bool Idempotent => true;

        private GetAudioClipWebRequest(UnityWebRequest unityWebRequest, string url)
        {
            this.url = url;
            UnityWebRequest = unityWebRequest;
        }

        public struct CreateAudioClipOp : IWebRequestOp<GetAudioClipWebRequest, AudioClip>
        {
            /// <summary>
            ///     Creates the audio clip
            /// </summary>
            public UniTask<AudioClip?> ExecuteAsync(GetAudioClipWebRequest webRequest, CancellationToken ct)
            {
                UnityWebRequest unityWebRequest = webRequest.UnityWebRequest;

                // files bigger than 1MB will be treated as streaming
                if (unityWebRequest.downloadedBytes > 1000000)
                    ((DownloadHandlerAudioClip)unityWebRequest.downloadHandler).streamAudio = true;

                AudioClip clip = DownloadHandlerAudioClip.GetContent(unityWebRequest);

                unityWebRequest.Dispose();

                clip.SetDebugName(webRequest.url);
                ProfilingCounters.AudioClipsAmount.Value++;

                return UniTask.FromResult(clip)!;
            }
        }

        internal static GetAudioClipWebRequest Initialize(string url, ref GetAudioClipArguments audioClipArguments)
        {
            UnityWebRequest wr = UnityWebRequestMultimedia.GetAudioClip(url, audioClipArguments.AudioType);
            return new GetAudioClipWebRequest(wr, url);
        }
    }
}
