using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatProfilePictureView : MonoBehaviour
    {
        [SerializeField] private ImageView thumbnailImageView;
        [SerializeField] private Image thumbnailBackground;
        [SerializeField] private GameObject loadingSpinner;

        public void Setup(Sprite sprite, bool isLoading)
        {
            loadingSpinner.SetActive(isLoading);
            thumbnailImageView.gameObject.SetActive(!isLoading);
            if (!isLoading)
            {
                if (sprite != null)
                    thumbnailImageView.SetImage(sprite);
            }
        }

        public void SetBackgroundColor(Color color)
        {
            thumbnailBackground.color = color;
        }
    }
}