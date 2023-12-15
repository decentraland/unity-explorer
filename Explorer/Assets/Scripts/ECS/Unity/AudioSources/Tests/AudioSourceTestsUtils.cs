using DCL.ECSComponents;
using UnityEditor;
using UnityEngine;

namespace ECS.Unity.AudioSources.Tests
{
    public static class AudioSourceTestsUtils
    {
        public static AudioClip TestAudioClip => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Scripts/ECS/Unity/AudioSources/Tests/cuckoo-test-clip.mp3");

        public static PBAudioSource CreatePBAudioSource() =>
            new()
            {
                AudioClipUrl = $"file://{Application.dataPath + "/../TestResources/Audio/cuckoo-test-clip.mp3"}",
                Loop = false,
                Pitch = 0.5f,
                Volume = 0.5f,
                Playing = true,
            };
    }
}
