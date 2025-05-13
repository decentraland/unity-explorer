using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardController : ControllerBase<CommunityCardView, CommunityCardParameter>
    {
        public CommunityCardController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
