using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionController : ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CommunityCreationEditionController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {

        }

        protected override void OnBeforeViewShow() =>
            viewInstance.SetAsClaimedName(inputData.HasClaimedName);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.backgroundCloseButton.OnClickAsync(ct), viewInstance!.cancelButton.OnClickAsync(ct));
    }
}
