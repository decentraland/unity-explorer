using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.PartialDownloading
{
    public struct GetPartialIntention : ILoadingIntention, IEquatable<GetPartialIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }
        private GetPartialIntention(
            bool lookForShaderAssets = false,
            CancellationTokenSource cancellationTokenSource = null)
        {
            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, cancellationTokenSource: cancellationTokenSource);
        }


        public bool Equals(GetPartialIntention other) =>
            throw new NotImplementedException();
    }
}
