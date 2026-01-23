using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class PlaceDetailPanelController : ControllerBase<PlaceDetailPanelView, PlaceDetailPanelParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource panelCts = new ();
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IProfileRepository profileRepository;

        public PlaceDetailPanelController(
            ViewFactoryMethod viewFactory,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IProfileRepository profileRepository) : base(viewFactory)
        {
            this.thumbnailLoader = thumbnailLoader;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.profileRepository = profileRepository;
        }

        public override void Dispose()
        {
            panelCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {

        }

        protected override void OnBeforeViewShow()
        {
            panelCts = panelCts.SafeRestart();
            SetupAsync(panelCts.Token).Forget();
            return;

            async UniTaskVoid SetupAsync(CancellationToken ct)
            {
                Profile.CompactInfo? creatorProfile = null;

                if (!string.IsNullOrEmpty(inputData.PlaceData.owner))
                    creatorProfile = await profileRepository.GetCompactAsync(inputData.PlaceData.owner, ct);

                viewInstance!.ConfigurePlaceData(inputData.PlaceData, thumbnailLoader, profileRepositoryWrapper, creatorProfile, panelCts.Token);
            }
        }

        protected override void OnViewClose()
        {
            panelCts.SafeCancelAndDispose();
        }
    }
}
