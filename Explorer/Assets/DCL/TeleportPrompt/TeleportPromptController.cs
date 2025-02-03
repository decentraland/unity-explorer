﻿using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Diagnostics;
using DCL.Input;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using DCL.Chat.MessageBus;
using DCL.UI;
using UnityEngine;
using Utility;

namespace DCL.TeleportPrompt
{
    public partial class TeleportPromptController : ControllerBase<TeleportPromptView, TeleportPromptController.Params>
    {
        private const string ORIGIN = "teleport prompt";

        private readonly ICursor cursor;
        private readonly IWebRequestController webRequestController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IChatMessagesBus chatMessagesBus;
        private ImageController placeImageController;
        private Action<TeleportPromptResultType> resultCallback;
        private CancellationTokenSource cts;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public TeleportPromptController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            IWebRequestController webRequestController,
            IPlacesAPIService placesAPIService,
            IChatMessagesBus chatMessagesBus
        ) : base(viewFactory)
        {
            this.cursor = cursor;
            this.webRequestController = webRequestController;
            this.placesAPIService = placesAPIService;
            this.chatMessagesBus = chatMessagesBus;
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

                chatMessagesBus.Send(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {inputData.Coords.x},{inputData.Coords.y}", ORIGIN);
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
            catch (Exception e)
            {
                SetEmptyPlaceInfo(parcel);

                if (e is not OperationCanceledException)
                    ReportHub.LogException(e, ReportCategory.UI);
            }
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
            viewInstance.placeCreator.text = "created by <b>Unknown</b>";
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
