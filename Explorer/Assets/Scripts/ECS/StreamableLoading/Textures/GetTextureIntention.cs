using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public struct GetTextureIntention : ILoadingIntention, IEquatable<GetTextureIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public bool IsReadable;
        public TextureWrapMode WrapMode;
        public FilterMode FilterMode;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetTextureIntention other) =>
            IsReadable == other.IsReadable && WrapMode == other.WrapMode && FilterMode == other.FilterMode && this.AreUrlEquals(other);

        public override bool Equals(object obj) =>
            obj is GetTextureIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IsReadable, (int)WrapMode, (int)FilterMode, CommonArguments.URL);

        public override string ToString() =>
            $"Get Texture: {CommonArguments.URL}";
    }
}
