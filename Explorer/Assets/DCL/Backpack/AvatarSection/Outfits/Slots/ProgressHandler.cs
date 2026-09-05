using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.AvatarSection.Outfits.Slots
{
    /// <summary>
    ///     Drives a looping fill animation on an Image using DOTween.
    ///     Auto-starts on OnEnable and stops/reset on OnDisable.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ProgressHandler : MonoBehaviour
    {
        [SerializeField] private Image? targetImage;

        [Header("Animation")]
        [SerializeField] private float halfCycleDuration = 0.8f;

        [SerializeField] private Ease ease = Ease.InOutCubic;
        [SerializeField] private bool resetOnDisable = true;

        private Sequence? seq;

        private void Awake()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (targetImage == null)
                return;

            // Optional: ensure the Image is configured for filled radial
            // (Silently proceed if not; devs can configure in the editor)
            // targetImage.type = Image.Type.Filled; targetImage.fillMethod = Image.FillMethod.Radial360;

            targetImage.fillAmount = 0f;
            targetImage.fillClockwise = true;

            seq = DOTween.Sequence()
                .Append(targetImage.DOFillAmount(1f, halfCycleDuration).SetEase(ease))
                .AppendCallback(() => targetImage.fillClockwise = false)
                .Append(targetImage.DOFillAmount(0f, halfCycleDuration).SetEase(ease))
                .AppendCallback(() => targetImage.fillClockwise = true)
                .SetLoops(-1, LoopType.Restart);
        }

        private void OnDisable()
        {
            if (seq != null)
            {
                seq.Kill();
                seq = null;
            }

            if (resetOnDisable && targetImage != null)
            {
                targetImage.fillClockwise = true;
                targetImage.fillAmount = 0f;
            }
        }
    }
}