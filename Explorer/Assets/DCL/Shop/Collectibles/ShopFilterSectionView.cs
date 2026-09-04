using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopFilterSectionView : MonoBehaviour
    {
        [field: SerializeField] public Button HeaderButton { get; private set; } = null!;
        [field: SerializeField] public RectTransform? Chevron { get; private set; }
        [field: SerializeField] public GameObject Content { get; private set; } = null!;
        [field: SerializeField] public TMP_Text? Summary { get; private set; }
        [field: SerializeField] public bool OpenByDefault { get; private set; } = true;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            HeaderButton.onClick.AddListener(() => SetOpen(!IsOpen));
            SetOpen(OpenByDefault);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            Content.SetActive(open);

            if (Chevron != null)
                Chevron.localRotation = Quaternion.Euler(0f, 0f, open ? 180f : 0f);
        }

        public void SetSummary(string? text)
        {
            if (Summary == null)
                return;

            Summary.text = text ?? string.Empty;
            Summary.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
