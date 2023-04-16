using NUnit.Framework;

public class SceneRuntimeShould
{
    // A Test behaves as an ordinary method
    [Test]
    public void MainTest()
    {
        var sceneRuntime = new SceneRuntime(Helpers.LoadSceneSourceCode("BasicRequire"));

        sceneRuntime.StartScene();
        sceneRuntime.Update();
    }
}
