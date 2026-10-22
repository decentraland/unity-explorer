using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    /// <summary>
    ///     A pooled LoopListView2 item showing the month of the thumbnail rows below it.
    /// </summary>
    public class ReelGalleryMonthHeaderView : MonoBehaviour
    {
        [field: SerializeField] internal TMP_Text monthText { get; private set; } = null!;

        public void SetMonth(DateTime month)
        {
            monthText.SetText(month.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

            // LoopListView2 measures the item right after binding, so the height must be final here.
            var rectTransform = (RectTransform)transform;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, monthText.preferredHeight);
        }
    }
}
