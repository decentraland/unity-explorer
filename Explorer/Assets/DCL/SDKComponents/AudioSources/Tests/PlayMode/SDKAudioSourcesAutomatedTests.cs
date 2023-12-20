using UnityEngine.TestTools;
using System.Collections;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources.Tests.PlayMode
{
    public class SDKAudioSourcesAutomatedTests
    {
        private string testScene = "Assets/Scenes/StaticSceneLoader.unity";

        [UnityTest]
        public IEnumerator VV()
        {
            // Load the scene asynchronously
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(testScene, LoadSceneMode.Single);

            // Wait until the scene is loaded
            while (!asyncLoad.isDone)
                yield return null;

            yield return new WaitForSeconds(10000);

            // Pass the test if this point is reached
            Assert.IsTrue(true);
        }
    }
}
