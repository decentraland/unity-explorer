using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    public struct GetFontIntention : ILoadingIntention, IEquatable<GetFontIntention>
    {
        public FontSourceKind Kind;

        public string Src;

        private int? hashCode;

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetFontIntention other) =>
            Kind == other.Kind && this.AreUrlEquals(other);

        public override bool Equals(object? obj) =>
            obj is GetFontIntention other && Equals(other);

        public override int GetHashCode()
        {
            if (hashCode != null)
                return hashCode.Value;

            hashCode = HashCode.Combine(Kind, CommonArguments.URL);
            return hashCode.Value;
        }

        public override string ToString() =>
            $"Get Font Intention: {Src} ({Kind}) {CommonArguments.URL}";
    }
}
