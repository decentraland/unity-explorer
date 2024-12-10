using System;
using System.Collections.Generic;

namespace DCL.InWorldCamera.CameraReelStorageService.Schemas
{
    [Serializable]
    public class CameraReelResponses
    {
        public List<CameraReelResponse> images = new ();
        public int currentImages;
        public int maxImages;
    }

    [Serializable]
    public class CameraReelResponsesCompact
    {
        public List<CameraReelResponseCompact> images = new ();
        public int currentImages;
        public int maxImages;
    }

    [Serializable]
    public class CameraReelResponseCompact
    {
        public string id;
        public string url;
        public string thumbnailUrl;
        public bool isPublic;
        public string dateTime;
    }

    [Serializable]
    public class CameraReelResponse
    {
        public string id;
        public string url;
        public string thumbnailUrl;
        public bool isPublic;

        public ScreenshotMetadata metadata;
    }

    [Serializable]
    public class CameraReelUploadResponse
    {
        public int currentImages;
        public int maxImages;

        public CameraReelResponse image;
    }

    [Serializable]
    public class CameraReelStorageResponse
    {
        public int currentImages;
        public int maxImages;
    }

    [Serializable]
    public class CameraReelErrorResponse
    {
        public string message;
        public string reason;
    }
}
