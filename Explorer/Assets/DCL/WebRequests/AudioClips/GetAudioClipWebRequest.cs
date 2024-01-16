using DCL.Profiling;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests.AudioClips
{
    /// <summary>
    ///     Representation of the created web request dedicated to download an audio clip
    /// </summary>
    public readonly struct GetAudioClipWebRequest: ITypedWebRequest
    {
        private readonly string url;

        public UnityWebRequest UnityWebRequest { get; }

        private GetAudioClipWebRequest(UnityWebRequest unityWebRequest, string url)
        {
            this.url = url;
            UnityWebRequest = unityWebRequest;
        }

        /// <summary>
        ///     Creates the audio clip and finalizes the request
        /// </summary>
        public AudioClip CreateAudioClip()
        {
            // files bigger than 1MB will be treated as streaming
            if (UnityWebRequest.downloadedBytes > 1000000)
                ((DownloadHandlerAudioClip)UnityWebRequest.downloadHandler).streamAudio = true;

            AudioClip clip = DownloadHandlerAudioClip.GetContent(UnityWebRequest);

            UnityWebRequest.Dispose();

            clip.SetDebugName(url);
            ProfilingCounters.AudioClipsAmount.Value++;

            return clip;
        }


        internal static GetAudioClipWebRequest Initialize(in CommonArguments commonArguments, GetAudioClipArguments audioClipArguments)
        {
            UnityWebRequest wr = UnityWebRequestMultimedia.GetAudioClip(commonArguments.URL, audioClipArguments.AudioType);
            return new GetAudioClipWebRequest(wr, commonArguments.URL);
        }
    }
}
