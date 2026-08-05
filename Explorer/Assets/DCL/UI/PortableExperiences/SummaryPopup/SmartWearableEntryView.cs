using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class SmartWearableEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text pxName = null!;

        [field: SerializeField]
        internal Button removeButton = null!;

        [field: SerializeField]
        internal Image rarityBackground = null!;

        [field: SerializeField]
        internal Image flap = null!;

        [field: SerializeField]
        internal Image categoryIcon = null!;

        [field: SerializeField]
        internal Image thumbnail = null!;

        public Action<string>? RemoveRequested;

        private string currentId = string.Empty;

        private CancellationTokenSource? thumbnailCts;

        public void Configure(string id, string wearableName, Sprite rarityBackgroundSprite, Color rarityColor, Sprite categoryIconSprite)
        {
            // A recycled cell may still have the previous binding's thumbnail load in flight.
            thumbnailCts.SafeCancelAndDispose();
            thumbnail.sprite = LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite;

            currentId = id;
            pxName.text = wearableName;
            rarityBackground.sprite = rarityBackgroundSprite;
            flap.color = rarityColor;
            categoryIcon.sprite = categoryIconSprite;
        }

        public void LoadThumbnail(IThumbnailProvider thumbnailProvider, IWearable wearable, CancellationToken panelCt)
        {
            thumbnailCts = thumbnailCts.SafeRestartLinked(panelCt);
            LoadThumbnailAsync(thumbnailProvider, wearable, thumbnailCts.Token).Forget();
        }

        private async UniTaskVoid LoadThumbnailAsync(IThumbnailProvider thumbnailProvider, IWearable wearable, CancellationToken ct)
        {
            try
            {
                Sprite sprite = await thumbnailProvider.GetAsync(wearable, ct);
                if (ct.IsCancellationRequested) return;

                thumbnail.sprite = sprite;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.THUMBNAILS); }
        }

        private void Awake()
        {
            removeButton.onClick.AddListener(() => RemoveRequested?.Invoke(currentId));
        }

        private void OnDestroy()
        {
            thumbnailCts.SafeCancelAndDispose();
        }
    }
}
