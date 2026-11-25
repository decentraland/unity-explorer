using DCL.AvatarRendering.Thumbnails.Utils;
using TMPro;
using UnityEngine;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteGridItemView : BackpackItemView
    {
        [field: SerializeField]
        public TMP_Text EquippedSlotLabel { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text ThumbnailName { get; private set; } = null!;

        public void ShowMetadata(string displayName)
        {
            ThumbnailName.text = displayName;
            SetThumbnail(null);
            LoadingView.FinishLoadingAnimation(FullBackpackItem);
        }

        public void SetThumbnail(Sprite? sprite)
        {
            if (sprite == LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite)
                sprite = null;

            WearableThumbnail.sprite = sprite;
            WearableThumbnail.enabled = sprite != null;
            ThumbnailName.gameObject.SetActive(sprite == null);
        }
    }
}
