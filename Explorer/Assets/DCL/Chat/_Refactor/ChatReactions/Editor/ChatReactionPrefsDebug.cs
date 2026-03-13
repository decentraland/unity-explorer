#if UNITY_EDITOR
using DCL.Prefs;
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    public static class ChatReactionPrefsDebug
    {
        [MenuItem("Decentraland/Cache/Clear Reaction Recents")]
        public static void ClearReactionRecents()
        {
            PlayerPrefs.DeleteKey(DCLPrefKeys.CHAT_REACTION_FAVORITES);
            PlayerPrefs.DeleteKey(DCLPrefKeys.CHAT_REACTION_SELECTED);
            PlayerPrefs.Save();
            Debug.Log("Cleared reaction recents and legacy selected key from PlayerPrefs");
        }
    }
}
#endif
