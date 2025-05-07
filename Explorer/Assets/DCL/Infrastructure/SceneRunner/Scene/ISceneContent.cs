using CommunicationData.URLHelpers;

namespace SceneRunner.Scene
{
    public interface ISceneContent
    {
        URLDomain ContentBaseUrl { get; }

        bool TryGetContentUrl(string contentPath, out URLAddress result);

        bool TryGetHash(string name, out string hash);
    }
}
