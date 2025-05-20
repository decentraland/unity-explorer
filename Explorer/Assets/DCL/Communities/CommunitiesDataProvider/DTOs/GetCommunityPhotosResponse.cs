
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityPhotosResponse
    {
        public CameraReelResponseCompact[] photos;
        public int totalPages;
    }
}
