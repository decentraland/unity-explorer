using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack;
using DCL.Ipfs;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using UnityEngine;

namespace Runtime.Wearables
{
    public class SmartWearableAuthorizationPopupController :  ControllerBase<SmartWearableAuthorizationPopupView, SmartWearableAuthorizationPopupController.Params>
    {
        private readonly SmartWearableCache smartWearableCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;

        public SmartWearableAuthorizationPopupController(
            [NotNull] ViewFactoryMethod viewFactory,
            SmartWearableCache smartWearableCache,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons) : base(viewFactory)
        {
            this.smartWearableCache = smartWearableCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.AuthorizeButton.onClick.AddListener(OnAuthorizeButtonClick);
            viewInstance.DenyButton.onClick.AddListener(OnDenyButtonClick);
        }

        private void OnAuthorizeButtonClick()
        {
            inputData.CompletionSource.TrySetResult(true);
        }

        private void OnDenyButtonClick()
        {
            inputData.CompletionSource.TrySetResult(false);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            await viewInstance!.WaitChoiceAsync();

        protected override void OnViewShow()
        {
            base.OnViewShow();

            var wearable = inputData.Wearable;
            var thumbnail = ((IAvatarAttachment)wearable).ThumbnailAssetResult?.Asset.Sprite;
            var rarityBackground = rarityBackgrounds.GetTypeImage(wearable.GetRarity());
            var rarityColor = rarityColors.GetColor(wearable.GetRarity());
            var categoryIcon = categoryIcons.GetTypeImage(wearable.GetCategory());
            viewInstance.Setup(wearable.GetName(), thumbnail, rarityBackground, rarityColor, categoryIcon);

            UpdatePermissionsAsync(wearable).Forget();
        }

        private async UniTask UpdatePermissionsAsync(IWearable wearable)
        {
            (_, SceneMetadata sceneMetadata) = await smartWearableCache.GetCachedSceneInfoAsync(wearable, CancellationToken.None);
            await UniTask.SwitchToMainThread();
            viewInstance.SetPermissions(sceneMetadata.requiredPermissions);
        }

        public static async UniTask<bool> RequestAuthorizationAsync(IMVCManager mvcManager, IWearable wearable, CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<bool>();

            var commandParams = new Params(wearable, completionSource);
            mvcManager.ShowAndForget(IssueCommand(commandParams), ct);
            if (ct.IsCancellationRequested) return false;

            return await commandParams.GetResultAsync(ct);
        }

        public struct Params
        {
            public readonly IWearable Wearable;

            public readonly UniTaskCompletionSource<bool> CompletionSource;

            public Params(IWearable wearable, UniTaskCompletionSource<bool> completionSource)
            {
                Wearable = wearable;
                CompletionSource = completionSource;
            }

            public async UniTask<bool> GetResultAsync(CancellationToken ct) =>
                await CompletionSource.Task.AttachExternalCancellation(ct);
        }
    }
}
