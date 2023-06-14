using AssetManagement;
using System.Threading;
using Utility;

namespace ECS.StreamableLoading.Common.Components
{
    public interface IAssetIntention
    {
        CancellationTokenSource CancellationTokenSource { get; }
    }

    public interface ILoadingIntention : IAssetIntention
    {
        CommonLoadingArguments CommonArguments { get; set; }
        CancellationTokenSource IAssetIntention.CancellationTokenSource => CommonArguments.cancellationTokenSource;
    }

    public static class LoadingIntentionExtensions
    {
        public static void SetURL<T>(this ref T loadingIntention, string url) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.URL = url;
            loadingIntention.CommonArguments = ca;
        }

        public static void RemoveCurrentSource<T>(this ref T loadingIntention) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.PermittedSources.RemoveFlag(ca.CurrentSource);
            loadingIntention.CommonArguments = ca;
        }

        public static void SetAttempts<T>(this ref T loadingIntention, int attempts) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.Attempts = attempts;
            loadingIntention.CommonArguments = ca;
        }

        public static void SetSources<T>(this ref T loadingIntention, AssetSource permittedSources, AssetSource currentSource) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.PermittedSources = permittedSources;
            ca.CurrentSource = currentSource;
            loadingIntention.CommonArguments = ca;
        }
    }
}
