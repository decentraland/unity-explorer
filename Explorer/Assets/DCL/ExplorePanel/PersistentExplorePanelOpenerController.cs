using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using MVC;
using System.Threading;

namespace DCL.ExplorePanel
{
    public class PersistentExplorePanelOpenerController : ControllerBase<PersistentExploreOpenerView, EmptyParameter>
    {
        private readonly IMVCManager mvcManager;

        public PersistentExplorePanelOpenerController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected override void OnViewShow()
        {
            viewInstance.OpenExploreButton.onClick.RemoveAllListeners();

            viewInstance.OpenExploreButton.onClick.AddListener(() =>
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.ButtonPressedAudio);
                mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap))).Forget();
            });
        }

    }
}
