using UnityEngine;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens
{
    public class TipBreadcrumb : MonoBehaviour
    {
        [field: SerializeField]
        public Button Button { get; private set; } = null!;

        [SerializeField]
        private Transform scalingTransform = null!;

        [SerializeField]
        private Image image = null!;

        [SerializeField]
        private Color selectedColor;

        [SerializeField]
        private Color unselectedColor;

        public void Select()
        {
            scalingTransform.localScale = Vector3.one * 1.4f;
            image.color = selectedColor;
        }

        public void Unselect()
        {
            scalingTransform.localScale = Vector3.one;
            image.color = unselectedColor;
        }
    }
}
