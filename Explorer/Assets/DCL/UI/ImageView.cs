using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ImageView : MonoBehaviour
    {
        [field: SerializeField]
        internal GameObject LoadingObject { get; private set; }

        [field: SerializeField]
        internal Image Image { get; private set; }

        public bool IsLoading
        {
            set => LoadingObject.SetActive(value);
        }

        public bool ImageEnabled
        {
            set => Image.enabled = value;
        }

        public void SetImage(Sprite sprite)
        {
            Image.sprite = sprite;
            LoadingObject.SetActive(false);
            Image.enabled = true;
        }

        public void SetColor(Color color) =>
            Image.color = color;
    }
}
