using AssetManagement;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     General non-parameterized intention that is nested in the loading system for other intentions
    /// </summary>
    public struct SubIntention : ILoadingIntention, IEquatable<SubIntention>
    {
        public SubIntention(CommonLoadingArguments commonArguments)
        {
            commonArguments.PermittedSources = AssetSource.NONE;
            CommonArguments = commonArguments;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public override string ToString() =>
            CommonArguments.URL.OriginalString;

        public bool Equals(SubIntention y) =>
            CommonArguments.URL.Equals(y.CommonArguments.URL);

        public override int GetHashCode() =>
            CommonArguments.URL.GetHashCode();
    }
}
