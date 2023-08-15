namespace SceneRunner.Scene
{
    public interface ISceneContent
    {
        bool TryGetContentUrl(string url, out string result);

        bool TryGetHash(string name, out string hash);
    }
}
