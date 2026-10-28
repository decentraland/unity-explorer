using TMPro;
using UnityEngine;

namespace DCL.UI
{
    /// <summary>
    ///     Keeps this RectTransform centred on the first &lt;link&gt; span of a TMP text, so a frame drawn around a
    ///     key name stays on the key whatever the surrounding text is.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TextLinkFrame : MonoBehaviour
    {
        [SerializeField] private TMP_Text text = null!;

        private void OnEnable()
        {
            text.OnPreRenderText += Reposition;
        }

        private void OnDisable()
        {
            text.OnPreRenderText -= Reposition;
        }

        private void Reposition(TMP_TextInfo textInfo)
        {
            if (textInfo.linkCount == 0)
                return;

            TMP_LinkInfo link = textInfo.linkInfo[0];

            int lastIndex = link.linkTextfirstCharacterIndex + link.linkTextLength - 1;

            // Truncation (e.g. ellipsis) can leave the span past the laid-out characters
            if (link.linkTextLength == 0 || lastIndex >= textInfo.characterCount)
                return;

            TMP_CharacterInfo first = textInfo.characterInfo[link.linkTextfirstCharacterIndex];
            TMP_CharacterInfo last = textInfo.characterInfo[lastIndex];

            // Horizontal centre of the glyphs, vertical centre of the line so the frame does not jump with ascenders or descenders
            var centre = new Vector3((first.bottomLeft.x + last.topRight.x) * 0.5f, (first.ascender + first.descender) * 0.5f, 0f);

            transform.position = text.rectTransform.TransformPoint(centre);
        }
    }
}
