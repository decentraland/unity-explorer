using UnityEngine;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens
{
    public class LoadingBackgroundView : MonoBehaviour
    {
        [field: SerializeField]
        public Image Image { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup RootCanvasGroup { get; private set; } = null!;
    }
}
