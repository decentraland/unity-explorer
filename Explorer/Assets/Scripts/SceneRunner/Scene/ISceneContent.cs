using CommunicationData.URLHelpers;

namespace SceneRunner.Scene
{
    public interface ISceneContent
    {
        URLDomain ContentBaseUrl { get; }

        bool TryGetContentUrl(string contentPath, out URLAddress result, out string fileHash);

        bool TryGetHash(string name, out string hash);
    }
}
