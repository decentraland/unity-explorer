using TMPro;
using UnityEngine;

namespace DCL.Emoji
{
    public class EmojiTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private Vector2 sourceAnchor = new (0.5f, 1f);
        [SerializeField] private Vector2 offset = new (0f, 28f);

        private readonly Vector3[] sourceCorners = new Vector3[4];

        private RectTransform rectTransform;
        private EmojiButton currentSource;

        public void Show(EmojiButton source)
        {
            currentSource = source;
            tooltipText.text = source.EmojiName;
            tooltipRoot.gameObject.SetActive(true);
            PositionAt(source.RectTransform);
        }

        public void Hide(EmojiButton source)
        {
            if (currentSource != source)
                return;

            ForceHide();
        }

        public void ForceHide() =>
            tooltipRoot.gameObject.SetActive(false);

        private void Awake()
        {
            rectTransform = (RectTransform) transform;
            ForceHide();
        }

        private void OnDisable()
        {
            ForceHide();
        }

        private void PositionAt(RectTransform source)
        {
            source.GetWorldCorners(sourceCorners);
            Vector3 sourcePosition = GetSourceAnchorPosition();

            var tooltipParent = (RectTransform) rectTransform.parent;
            Vector2 localPosition = tooltipParent.InverseTransformPoint(sourcePosition);
            Vector2 tooltipPosition = localPosition + offset;
            rectTransform.localPosition = new Vector3(tooltipPosition.x, tooltipPosition.y, rectTransform.localPosition.z);
        }

        private Vector3 GetSourceAnchorPosition()
        {
            Vector3 bottom = Vector3.Lerp(sourceCorners[0], sourceCorners[3], sourceAnchor.x);
            Vector3 top = Vector3.Lerp(sourceCorners[1], sourceCorners[2], sourceAnchor.x);
            return Vector3.Lerp(bottom, top, sourceAnchor.y);
        }
    }
}
