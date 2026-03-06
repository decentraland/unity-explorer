#if UNITY_EDITOR
using DCL.Prefs;
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    public static class ChatReactionPrefsDebug
    {
        [MenuItem("Decentraland/Cache/Clear Reaction Favorites")]
        public static void ClearReactionFavorites()
        {
            PlayerPrefs.DeleteKey(DCLPrefKeys.CHAT_REACTION_FAVORITES);
            PlayerPrefs.Save();
            Debug.Log("Cleared " + DCLPrefKeys.CHAT_REACTION_FAVORITES + " from PlayerPrefs");
        }
    }
}
#endif
