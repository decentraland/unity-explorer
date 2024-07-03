using TMPro;
using UnityEngine;

namespace DCL.Navmap
{
    public class MapPinTooltipView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Title { get; private set; }
        [field: SerializeField] public TMP_Text Description { get; private set; }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (!gameObject.activeInHierarchy)
                return;

            gameObject.SetActive(false);
        }
    }
}
