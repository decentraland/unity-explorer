using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAttachmentsLoadingIntention<in TResultElement> : ILoadingIntention
    {
        int TotalAmount { get; }

        void SetTotal(int total);

        void AppendToResult(TResultElement resultElement);
    }
}
