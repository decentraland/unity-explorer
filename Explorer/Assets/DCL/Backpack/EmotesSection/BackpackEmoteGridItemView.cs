using DCL.AvatarRendering.Thumbnails.Utils;
using TMPro;
using UnityEngine;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteGridItemView : BackpackItemView
    {
        [field: SerializeField]
        public TMP_Text EquippedSlotLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text ThumbnailName { get; private set; }

        public void ShowMetadata(string name)
        {
            ThumbnailName.text = name;
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
