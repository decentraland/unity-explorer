using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AudioClips
{
    public struct GetAudioClipIntention : ILoadingIntention, IEquatable<GetAudioClipIntention>
    {
        public AudioType AudioType;
        private int? hashCode;

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetAudioClipIntention other) =>
            this.AreUrlEquals(other);

        public override bool Equals(object? obj) =>
            obj is GetAudioClipIntention other && Equals(other);

        public override int GetHashCode()
        {
            if (hashCode != null)
                return hashCode.Value;

            hashCode = CommonArguments.URL.GetHashCode();
            return hashCode.Value;
        }

        public override string ToString() =>
            $"Get AudioClip Intention: {CommonArguments.URL}";
    }
}
