using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens
{
    public class TipView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TitleLabel { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text BodyLabel { get; private set; } = null!;

        [field: SerializeField]
        public Image Image { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup RootCanvasGroup { get; private set; } = null!;
    }
}
