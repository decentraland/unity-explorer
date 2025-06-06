using DCL.InWorldCamera.CameraReelGallery;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Photos
{
    public class PhotosView : MonoBehaviour
    {
        [field: SerializeField] public CameraReelGalleryView GalleryView { get; private set; }

        public void SetActive(bool active) => gameObject.SetActive(active);
    }
}
