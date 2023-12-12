using UnityEngine;

namespace DCL.WebRequests.AudioClips
{
    public struct GetAudioClipArguments
    {
        public readonly AudioType AudioType;

        public GetAudioClipArguments(string url)
        {
            AudioType = GetAudioTypeFromUrlName(url);
        }

        private static AudioType GetAudioTypeFromUrlName(string url)
        {
            if (!string.IsNullOrEmpty(url))
                return url[^3..].ToLower() switch
                       {
                           "mp3" => AudioType.MPEG,
                           "wav" => AudioType.WAV,
                           "ogg" => AudioType.OGGVORBIS,
                           _ => AudioType.UNKNOWN,
                       };

            Debug.LogError("Cannot detect AudioType. UrlName doesn't contain file extension!");
            return AudioType.UNKNOWN;
        }
    }
}
