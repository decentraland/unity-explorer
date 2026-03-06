#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Editor
{
    public static class ChatReactionPrefsDebug
    {
        [MenuItem("Decentraland/Cache/Clear Reaction Favorites")]
        public static void ClearReactionFavorites()
        {
            PlayerPrefs.DeleteKey("ChatReaction_Favorites");
            PlayerPrefs.Save();
            Debug.Log("Cleared ChatReaction_Favorites from PlayerPrefs");
        }
    }
}
#endif
