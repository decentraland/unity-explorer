using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    [CreateAssetMenu(fileName = "CameraReelGalleryMessagesSettings", menuName = "DCL/CameraReelGallery/ActionMessages")]
    public class CameraReelGalleryMessagesConfiguration : ScriptableObject
    {
        [field: SerializeField] public string ShareToXMessage { get; private set; } = "Happening right now in @decentraland.\n\nCome hang out \ud83d\udc4b \n\n";
        [field: SerializeField] public string PhotoSuccessfullyDeletedMessage { get; private set; } = "Photo successfully deleted";
        [field: SerializeField] public string PhotoSuccessfullyUpdatedMessage { get; private set; } = "Photo successfully updated";
        [field: SerializeField] public string PhotoSuccessfullyDownloadedMessage { get; private set; } = "Photo successfully downloaded";
        [field: SerializeField] public string LinkCopiedMessage { get; private set; } = "Link copied!";
    }
}
