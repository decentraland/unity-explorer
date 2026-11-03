using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    public struct GetFontIntention : ILoadingIntention, IEquatable<GetFontIntention>
    {
        public string Src;

        public ConvertedFontBundle? Bundle;

        private int? hashCode;

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        // Requests for the same bundle must resolve to one FontData, because they share the font assets of that bundle
        public bool Equals(GetFontIntention other) =>
            Bundle != null || other.Bundle != null
                ? Bundle?.Listed == other.Bundle?.Listed && StringComparer.OrdinalIgnoreCase.Equals(Bundle?.Hash, other.Bundle?.Hash)
                : this.AreUrlEquals(other);

        public override bool Equals(object? obj) =>
            obj is GetFontIntention other && Equals(other);

        public override int GetHashCode()
        {
            if (hashCode != null)
                return hashCode.Value;

            hashCode = Bundle is { } bundle ? StringComparer.OrdinalIgnoreCase.GetHashCode(bundle.Hash) : CommonArguments.URL.GetHashCode();
            return hashCode.Value;
        }

        public override string ToString() =>
            $"Get Font Intention: {Src} {CommonArguments.URL}";
    }
}
