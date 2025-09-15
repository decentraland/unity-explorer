using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    [CreateAssetMenu(fileName = "CameraReelGalleryMessagesSettings", menuName = "DCL/CameraReelGallery/ActionMessages")]
    public class CameraReelGalleryMessagesConfiguration : ScriptableObject
    {
        [field: SerializeField] public string ShareToXMessage { get; private set; } = "Check out what I'm doing in @decentraland right now and join me!";
        [field: SerializeField] public string PhotoSuccessfullyDeletedMessage { get; private set; } = "Photo successfully deleted";
        [field: SerializeField] public string PhotoSuccessfullyUpdatedMessage { get; private set; } = "Photo successfully updated";
        [field: SerializeField] public string PhotoSuccessfullyDownloadedMessage { get; private set; } = "Photo successfully downloaded";
        [field: SerializeField] public string LinkCopiedMessage { get; private set; } = "Link copied!";
    }
}
