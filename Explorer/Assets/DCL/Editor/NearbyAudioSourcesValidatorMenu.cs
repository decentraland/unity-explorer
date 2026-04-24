using DCL.VoiceChat;
using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DCL.Editor
{
    /// <summary>
    /// Diagnostic menu that compares <see cref="LivekitAudioSource"/> child GameObjects under the
    /// "VoiceChatSources_Nearby" parent against the authoritative active-audio-sources dictionary
    /// tracked by <see cref="NearbyVoiceChatManager"/>. Reports wallet IDs of extras/missing entries.
    /// </summary>
    public static class NearbyAudioSourcesValidatorMenu
    {
        private const string PARENT_NAME = "VoiceChatSources_Nearby";
        private const string SOURCE_NAME_PREFIX = "LivekitSource_";
        private const string LOG_TAG = "[NearbyAudioSourcesValidator]";

        [MenuItem("Decentraland/Voice Chat/Validate Nearby Audio Sources")]
        public static void Validate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{LOG_TAG} Enter Play Mode before running this check.");
                return;
            }

            ConcurrentDictionary<string, LivekitAudioSource>? expected = NearbyVoiceChatManager.EditorActiveAudioSources;
            if (expected == null)
            {
                Debug.LogWarning($"{LOG_TAG} NearbyVoiceChatManager is not active — nothing to validate.");
                return;
            }

            GameObject parent = GameObject.Find(PARENT_NAME);
            if (parent == null)
            {
                Debug.LogWarning($"{LOG_TAG} Parent GameObject '{PARENT_NAME}' not found in the scene.");
                return;
            }

            var expectedIdentities = new HashSet<string>(expected.Keys);

            Transform parentTransform = parent.transform;
            int sceneCount = parentTransform.childCount;
            var sceneIdentities = new List<string>(sceneCount);

            for (int i = 0; i < sceneCount; i++)
            {
                string childName = parentTransform.GetChild(i).name;
                string identity = childName.StartsWith(SOURCE_NAME_PREFIX)
                    ? childName.Substring(SOURCE_NAME_PREFIX.Length)
                    : childName;
                sceneIdentities.Add(identity);
            }

            var sceneSet = new HashSet<string>(sceneIdentities);

            var extras = new List<string>();
            foreach (string id in sceneIdentities)
                if (!expectedIdentities.Contains(id))
                    extras.Add(id);

            var missing = new List<string>();
            foreach (string id in expectedIdentities)
                if (!sceneSet.Contains(id))
                    missing.Add(id);

            int expectedCount = expectedIdentities.Count;

            var sb = new StringBuilder(256);
            sb.Append(LOG_TAG).Append(" Scene sources: ").Append(sceneCount)
              .Append(", expected: ").Append(expectedCount);

            if (sceneCount == expectedCount && extras.Count == 0 && missing.Count == 0)
            {
                sb.Append(" — OK");
                Debug.Log(sb.ToString());
                return;
            }

            sb.Append(" — MISMATCH");

            if (extras.Count > 0)
            {
                sb.Append("\nExtras (in scene, not expected) [").Append(extras.Count).Append("]:");
                foreach (string id in extras)
                    sb.Append("\n  ").Append(id);
            }

            if (missing.Count > 0)
            {
                sb.Append("\nMissing (expected, not in scene) [").Append(missing.Count).Append("]:");
                foreach (string id in missing)
                    sb.Append("\n  ").Append(id);
            }

            Debug.LogWarning(sb.ToString());
        }
    }
}
