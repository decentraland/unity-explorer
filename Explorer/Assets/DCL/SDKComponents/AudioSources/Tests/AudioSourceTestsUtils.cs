using DCL.ECSComponents;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace DCL.SDKComponents.AudioSources.Tests
{
    public static class AudioSourceTestsUtils
    {
        private const string AUDIO_SOURCE_PATH = "DCL/SDKComponents/AudioSources/Tests/cuckoo-test-clip.mp3";
        public static AudioClip TestAudioClip => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/{AUDIO_SOURCE_PATH}");

        public static PBAudioSource CreatePBAudioSource()
        {
            Assert.IsNotNull(TestAudioClip);

            return new PBAudioSource
            {
                AudioClipUrl = $"file://{Application.dataPath}/{AUDIO_SOURCE_PATH}",
                Loop = false,
                Pitch = 0.5f,
                Volume = 0.5f,
                Playing = true,
            };
        }
    }
}
