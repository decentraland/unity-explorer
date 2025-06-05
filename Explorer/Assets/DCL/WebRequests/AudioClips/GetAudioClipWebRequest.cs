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
    public class GetAudioClipWebRequest : TypedWebRequestBase<GetAudioClipArguments>
    {
        internal GetAudioClipWebRequest(RequestEnvelope envelope, GetAudioClipArguments args, IWebRequestController controller) : base(envelope, args, controller) { }

        public override bool Http2Supported => false;

        public override UnityWebRequest CreateUnityWebRequest() =>
            UnityWebRequestMultimedia.GetAudioClip(Envelope.CommonArguments.URL, Args.AudioType);

        public async UniTask<AudioClip?> CreateAudioClipAsync(CancellationToken ct)
        {
            using IWebRequest? wr = await this.SendAsync(ct);

            if (wr.nativeRequest is not UnityWebRequest unityWebRequest)
                throw new NotSupportedException($"{nameof(CreateAudioClipAsync)} supports {nameof(UnityWebRequest)} only");

            // files bigger than 1MB will be treated as streaming
            if (unityWebRequest.downloadedBytes > 1000000)
                ((DownloadHandlerAudioClip)unityWebRequest.downloadHandler).streamAudio = true;

            AudioClip clip = DownloadHandlerAudioClip.GetContent(unityWebRequest);

            clip.SetDebugName(wr.Url.OriginalString);
            ProfilingCounters.AudioClipsAmount.Value++;

            return clip;
        }
    }
}
