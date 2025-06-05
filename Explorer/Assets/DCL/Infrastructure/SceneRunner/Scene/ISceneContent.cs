using CommunicationData.URLHelpers;
using System;

namespace SceneRunner.Scene
{
    public interface ISceneContent
    {
        URLDomain ContentBaseUrl { get; }

        bool TryGetContentUrl(string contentPath, out Uri result);

        bool TryGetHash(string name, out string hash);
    }
}
