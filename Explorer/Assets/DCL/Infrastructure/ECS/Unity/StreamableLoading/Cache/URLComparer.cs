using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    public class URLComparer<TLoadingIntention> : IEqualityComparer<TLoadingIntention> where TLoadingIntention: ILoadingIntention
    {
        public static readonly URLComparer<TLoadingIntention> INSTANCE = new ();

        public int GetHashCode(TLoadingIntention intention) =>
            intention.CommonArguments.URL.GetHashCode();

        public bool Equals(TLoadingIntention x, TLoadingIntention y) =>
            Equals(x.CommonArguments.URL, y.CommonArguments.URL);
    }
}
