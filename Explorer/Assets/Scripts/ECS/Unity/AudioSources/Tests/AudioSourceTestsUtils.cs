using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.AudioSources.Tests
{
    public static class AudioSourceTestsUtils
    {
        public static PBAudioSource CreatePBAudioSource() =>
            new()
            {
                AudioClipUrl = $"file://{Application.dataPath + "/../TestResources/Audio/Test.mp3"}",
                Loop = false,
                Pitch = 0.5f,
                Volume = 0.5f,
                Playing = true,
            };
    }
}
