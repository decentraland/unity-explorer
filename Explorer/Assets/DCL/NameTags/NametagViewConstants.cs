using DG.Tweening;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Nametags
{
    public static class NametagViewConstants
    {
        public const int DEFAULT_OPACITY_MAX_DISTANCE = 20;
        internal const long CHAT_BUBBLE_DELAY = 5000L;
        internal const int MAX_MESSAGE_WIDTH = 300;

        internal const string WALLET_ID_OPENING_STYLE = "<color=#FFFFFF66>";
        internal const string WALLET_ID_CLOSING_STYLE = "</color>";
        internal const string RECIPIENT_NAME_START_STRING = "<color=#FFFFFF>for</color> ";
    }
}
