using DG.Tweening;
using UnityEngine;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class MetadataSidePanelAnimator
    {
        private readonly RectTransform panelRectTransform;
        private readonly RectTransform infoButtonImage;
        private readonly float initOffset;

        private float rightOffset;
        private Tweener currentTween;

        public MetadataSidePanelAnimator(RectTransform panelRectTransform, RectTransform infoButtonImage)
        {
            this.infoButtonImage = infoButtonImage;
            this.panelRectTransform = panelRectTransform;

            initOffset = panelRectTransform.offsetMax.x;
            rightOffset = -initOffset;
        }

        public void ToggleSizeMode(bool toFullScreen, float duration)
        {
            currentTween.Kill();

            if (toFullScreen)
            {
                currentTween = DOVirtual.Float(rightOffset, 0, duration, UpdateSizeMode);
                infoButtonImage.DOScale(new Vector3(-1, 1, 1), duration);
            }
            else
            {
                currentTween = DOVirtual.Float(rightOffset, -initOffset, duration, UpdateSizeMode);
                infoButtonImage.DOScale(Vector3.one, duration);
            }
        }

        private void UpdateSizeMode(float value)
        {
            rightOffset = value;
            panelRectTransform.offsetMax = new Vector2(-rightOffset, panelRectTransform.offsetMax.y);
        }
    }
}
