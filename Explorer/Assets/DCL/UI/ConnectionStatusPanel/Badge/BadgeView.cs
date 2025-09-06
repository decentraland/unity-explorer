using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ConnectionStatusPanel.Badge
{
    public class BadgeView : MonoBehaviour
    {
        [SerializeField] private RectTransform self = null!;
        [SerializeField] private TMP_Text tmpText = null!;
        [SerializeField] private List<Image> backgroundPieces = new ();

        public void UpdateText(string text, float? width = null)
        {
            tmpText.SetText(text);

            if (width.HasValue && Mathf.Approximately(width.Value, 0) == false)
            {
                var delta = self.sizeDelta;
                delta.x = width.Value;
                self.sizeDelta = delta;
            }
        }

        public void ApplyColor(Color textColor, Color backgroundColor)
        {
            tmpText.color = textColor;
            foreach (Image backgroundPiece in backgroundPieces) backgroundPiece.color = backgroundColor;
        }
    }
}
