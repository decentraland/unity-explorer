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

        public void Configure(string id, string wearableName, Sprite rarityBackgroundSprite, Color rarityColor, Sprite categoryIconSprite)
        {
            currentId = id;
            pxName.text = wearableName;
            thumbnail.sprite = LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite;
            rarityBackground.sprite = rarityBackgroundSprite;
            flap.color = rarityColor;
            categoryIcon.sprite = categoryIconSprite;
        }

        public void LoadThumbnail(IThumbnailProvider thumbnailProvider, IWearable wearable, CancellationToken panelCt)
        {
            // Panel-scoped token only: cancelling on rebind poisons ECSThumbnailProvider's shared in-flight slot for any other waiter.
            LoadThumbnailAsync(thumbnailProvider, wearable, panelCt).Forget();
        }

        private async UniTaskVoid LoadThumbnailAsync(IThumbnailProvider thumbnailProvider, IWearable wearable, CancellationToken ct)
        {
            string boundId = currentId;

            try
            {
                Sprite sprite = await thumbnailProvider.GetAsync(wearable, ct);
                if (ct.IsCancellationRequested) return;

                // The cell may have been recycled to another wearable while the load was in flight.
                if (!string.Equals(currentId, boundId, StringComparison.OrdinalIgnoreCase)) return;

                thumbnail.sprite = sprite;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.THUMBNAILS); }
        }

        private void Awake()
        {
            removeButton.onClick.AddListener(() => RemoveRequested?.Invoke(currentId));
        }
    }
}
