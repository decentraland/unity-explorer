using UnityEngine;

namespace DCL.WebRequests
{
    public struct GetAudioClipArguments
    {
        public readonly AudioType AudioType;

        public GetAudioClipArguments(AudioType audioType)
        {
            this.AudioType = audioType;
        }
    }
}
