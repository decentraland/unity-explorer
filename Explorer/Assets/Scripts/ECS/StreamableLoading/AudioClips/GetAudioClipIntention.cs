using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AudioClips
{
    public struct GetAudioClipIntention : ILoadingIntention, IEquatable<GetAudioClipIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public AudioType AudioType;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetAudioClipIntention other) =>
            CommonArguments.URL == other.CommonArguments.URL;

        public override bool Equals(object? obj) =>
            obj is GetAudioClipIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(AudioType, CommonArguments.URL);

        public override string ToString() =>
            $"Get AudioClip Intention: {CommonArguments.URL}";
    }
}
