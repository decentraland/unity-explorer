using CommunicationData.URLHelpers;

namespace SceneRunner.Scene
{
    public interface ISceneContent
    {
        bool TryGetContentUrl(string contentPath, out URLAddress result);

        bool TryGetHash(string name, out string hash);
    }
}
