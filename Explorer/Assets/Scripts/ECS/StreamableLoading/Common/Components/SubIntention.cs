using AssetManagement;
using System.Threading;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     General non-parameterized intention that is nested in the loading system for other intentions
    /// </summary>
    public struct SubIntention : ILoadingIntention
    {
        public SubIntention(CommonLoadingArguments commonArguments)
        {
            commonArguments.PermittedSources = AssetSource.NONE;
            CommonArguments = commonArguments;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public override string ToString() =>
            CommonArguments.URL;
    }
}
