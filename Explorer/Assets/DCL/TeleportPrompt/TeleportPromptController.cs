using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Input;
using DCL.ParcelsService;
using DCL.PlacesAPIService;
using DCL.SceneLoadingScreens;
using DCL.UI;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Reporting;
using MVC;
using System;
using System.Threading;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using Utility;

namespace DCL.TeleportPrompt
{
    public partial class TeleportPromptController : ControllerBase<TeleportPromptView, TeleportPromptController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ICursor cursor;
        private readonly IRealmNavigator realmNavigator;
        private readonly IMVCManager mvcManager;
        private readonly IWebRequestController webRequestController;
        private readonly IPlacesAPIService placesAPIService;
        private ImageController placeImageController;
        private Action<TeleportPromptResultType> resultCallback;
        private CancellationTokenSource cts;

        public TeleportPromptController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            IRealmNavigator realmNavigator,
            IMVCManager mvcManager,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService) : base(viewFactory)
        {
            this.cursor = cursor;
            this.realmNavigator = realmNavigator;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            this.placesAPIService = placesAPIService;
        }

        protected override void OnViewInstantiated()
        {
            placeImageController = new ImageController(viewInstance.placeImage, webRequestController);
            viewInstance.cancelButton.onClick.AddListener(Dismiss);
            viewInstance.continueButton.onClick.AddListener(Approve);
        }

        protected override void OnViewShow()
        {
            cursor.Unlock();

            RequestTeleport(inputData.Coords, result =>
            {
                if (result != TeleportPromptResultType.Approved)
                    return;

                cts = cts.SafeRestart();
                realmNavigator.TryInitializeTeleportToParcelAsync(inputData.Coords, cts.Token).Forget();
                ;
            });
        }

        protected override void OnViewClose() =>
            cts.SafeCancelAndDispose();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.cancelButton.OnClickAsync(ct),
                viewInstance.continueButton.OnClickAsync(ct));

        private void RequestTeleport(Vector2Int coords, Action<TeleportPromptResultType> result)
        {
            resultCallback = result;

            cts = cts.SafeRestart();
            GetPlaceInfoAsync(coords, cts.Token).Forget();
        }

        private void Dismiss() =>
            resultCallback?.Invoke(TeleportPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(TeleportPromptResultType.Approved);

        private async UniTaskVoid GetPlaceInfoAsync(Vector2Int parcel, CancellationToken ct)
        {
            try
            {
                placeImageController.SetImage(viewInstance.defaultImage);
                SetPopupAsLoading(true);
                await UniTask.Delay(300, cancellationToken: ct);
                PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(parcel, ct);

                if (placeInfo == null)
                    SetEmptyPlaceInfo(parcel);
                else
                    SetPlaceInfo(placeInfo);
            }
            catch (Exception e) when (e is not OperationCanceledException) { SetEmptyPlaceInfo(parcel); }
        }

        private void SetPlaceInfo(PlacesData.PlaceInfo placeInfo)
        {
            SetPopupAsLoading(false);
            placeImageController.RequestImage(placeInfo.image);
            viewInstance.placeName.text = placeInfo.title;
            viewInstance.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
            viewInstance.location.text = placeInfo.base_position;
        }

        private void SetEmptyPlaceInfo(Vector2Int parcel)
        {
            SetPopupAsLoading(false);
            viewInstance.placeName.text = "Empty parcel";
            viewInstance.placeCreator.text = $"created by <b>Unknown</b>";
            viewInstance.location.text = parcel.ToString();
        }

        private void SetPopupAsLoading(bool isLoading)
        {
            viewInstance.loadingSpinner.SetActive(isLoading);
            viewInstance.loadingPlaceContainer.SetActive(isLoading);
            viewInstance.placeInfoContainer.SetActive(!isLoading);
            viewInstance.cancelButton.interactable = !isLoading;
            viewInstance.continueButton.interactable = !isLoading;
        }
    }
}
