using UnityEngine;

namespace ECS.Unity.AudioSources
{
    public static class AudioUtils
    {
        public static AudioType ToAudioType(this string url)
        {
            if (!string.IsNullOrEmpty(url))
                return url[^3..].ToLower() switch
                       {
                           "mp3" => AudioType.MPEG,
                           "wav" => AudioType.WAV,
                           "ogg" => AudioType.OGGVORBIS,
                           _ => AudioType.UNKNOWN,
                       };

            Debug.LogError($"Cannot detect AudioType. UrlName doesn't contain file extension!. Setting to {AudioType.UNKNOWN.ToString()}");
            return AudioType.UNKNOWN;
        }
    }
}
