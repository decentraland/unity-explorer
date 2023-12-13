using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipsCache : IStreamableCache<AudioClip, GetAudioClipIntention>
    {
        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AudioClip>> IrrecoverableFailures { get; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool TryGet(in GetAudioClipIntention key, out AudioClip asset) =>
            throw new NotImplementedException();

        public void Add(in GetAudioClipIntention key, AudioClip asset)
        {
            throw new NotImplementedException();
        }

        public void Dereference(in GetAudioClipIntention key, AudioClip asset) { }

        public bool Equals(GetAudioClipIntention x, GetAudioClipIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetAudioClipIntention obj) =>
            obj.GetHashCode();
    }
}
