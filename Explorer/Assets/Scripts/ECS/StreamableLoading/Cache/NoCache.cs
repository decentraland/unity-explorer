using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    /// <summary>
    ///     Null implementation to provide no caching capabilities explicitly
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TLoadingIntention"></typeparam>
    public class NoCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention> where TLoadingIntention: struct, ILoadingIntention
    {
        public static readonly NoCache<TAsset, TLoadingIntention> INSTANCE = new ();

        bool IEqualityComparer<TLoadingIntention>.Equals(TLoadingIntention x, TLoadingIntention y) =>
            EqualityComparer<TLoadingIntention>.Default.Equals(x, y);

        int IEqualityComparer<TLoadingIntention>.GetHashCode(TLoadingIntention obj) =>
            EqualityComparer<TLoadingIntention>.Default.GetHashCode(obj);

        public bool TryGet(in TLoadingIntention key, out TAsset asset)
        {
            asset = default(TAsset);
            return false;
        }

        public void Add(in TLoadingIntention key, TAsset asset) { }

        public void Dereference(in TLoadingIntention key, TAsset asset) { }
    }
}
