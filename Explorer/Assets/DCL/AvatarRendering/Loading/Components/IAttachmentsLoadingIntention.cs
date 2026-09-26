using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAttachmentsLoadingIntention<in TResultElement> : ILoadingIntention
    {
        int TotalAmount { get; }

        void SetTotal(int total);

        void AppendToResult(TResultElement resultElement);

        bool NeedsBuilderAPISigning { get; }

        public IReadOnlyList<(string, string)> Params { get; }
        public string UserID { get; }
    }
}
