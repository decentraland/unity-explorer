using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetTrimmedEmotesByParamIntention : IAttachmentsLoadingIntention<ITrimmedEmote>, IEquatable<GetTrimmedEmotesByParamIntention>
    {
        public List<ITrimmedEmote> Results { get; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }
        public bool NeedsBuilderAPISigning { get; }
        public IReadOnlyList<(string, string)> Params { get; }
        public string UserID { get; }

        public GetTrimmedEmotesByParamIntention(IReadOnlyList<(string, string)> requestParams, string userID, List<ITrimmedEmote> results, int totalAmount, bool needsBuilderAPISigning = false)
        {
            Params = requestParams;
            UserID = userID;
            Results = results;
            TotalAmount = totalAmount;
            NeedsBuilderAPISigning = needsBuilderAPISigning;
            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, cancellationTokenSource: new CancellationTokenSource());
        }

        public bool Equals(GetTrimmedEmotesByParamIntention other) =>
            CommonArguments.URL.Equals(other.CommonArguments.URL);

        public override bool Equals(object? obj) =>
            obj is GetTrimmedEmotesByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.GetHashCode();

        public int TotalAmount { get; private set; }

        public void SetTotal(int total)
        {
            TotalAmount = total;
        }

        public void AppendToResult(ITrimmedEmote resultElement)
        {
            Results.Add(resultElement);
        }
    }
}
