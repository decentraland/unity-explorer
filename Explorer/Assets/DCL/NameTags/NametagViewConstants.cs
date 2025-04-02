using DG.Tweening;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Nametags
{
    public static class NametagViewConstants
    {
        //Default values in case scriptable object is missing or not assigned
        internal const float DEFAULT_HEIGHT = 0.3f;
        internal const float DEFAULT_MARGIN_OFFSET_HEIGHT = 0.15f;
        internal const float DEFAULT_MARGIN_OFFSET_WIDTH = 0.2f;
        internal const float DEFAULT_BUBBLE_MARGIN_OFFSET_WIDTH = 0.4f;
        internal const float DEFAULT_BUBBLE_MARGIN_OFFSET_HEIGHT = 0.6f;
        internal const float DEFAULT_BUBBLE_ANIMATION_IN_DURATION = 0.5f;
        internal const float DEFAULT_BUBBLE_ANIMATION_OUT_DURATION = 0.35f;
        internal const float DEFAULT_SINGLE_EMOJI_EXTRA_HEIGHT = 0.1f;
        internal const float DEFAULT_SINGLE_EMOJI_SIZE = 3.5f;
        internal const int DEFAULT_OPACITY_MAX_DISTANCE = 20;
        internal const int DEFAULT_ADDITIONAL_MS_PER_CHARACTER = 20;
        internal const int DEFAULT_BUBBLE_IDLE_TIME_MS = 5000;

        internal const float MESSAGE_CONTENT_FONT_SIZE = 1.3f;
        internal const float DISTANCE_THRESHOLD = 0.1f;
        internal const float MAX_BUBBLE_WIDTH = 2.5f;
        internal const string WALLET_ID_OPENING_STYLE = "<color=#FFFFFF66><font=\"LiberationSans SDF\">";
        internal const string WALLET_ID_CLOSING_STYLE = "</font></color>";
        internal const string RECIPIENT_NAME_START_STRING = "<color=#FFFFFF>for</color> ";
        internal static readonly Regex SINGLE_EMOJI_REGEX = new (@"^\s*\\U[0-9a-fA-F]{8}\s*$", RegexOptions.Compiled);
        internal static readonly int SURFACE_PROPERTY = Shader.PropertyToID("_Surface");
        internal static readonly int SRC_BLEND_PROPERTY = Shader.PropertyToID("_SrcBlend");
        internal static readonly int DST_BLEND_PROPERTY = Shader.PropertyToID("_DstBlend");
        internal static readonly int Z_WRITE_PROPERTY = Shader.PropertyToID("_ZWrite");
        internal static readonly Vector2 ZERO_VECTOR = Vector2.zero;
        internal static readonly Ease LINEAR_EASE = Ease.Linear;
        internal static readonly int WALLET_ID_LENGTH = 5 + WALLET_ID_OPENING_STYLE.Length + WALLET_ID_CLOSING_STYLE.Length;
        internal static readonly Color DEFAULT_OPAQUE_COLOR = new (1, 1, 1, 1);
        internal static readonly Color DEFAULT_TRANSPARENT_COLOR = new (1, 1, 1, 0);
        internal static readonly Color MENTIONED_BUBBLE_TAIL_COLOR = new (0.5568f, 0.2549f, 0.5490f, 1);
        internal static readonly Color NORMAL_BUBBLE_TAIL_COLOR = new (0.2627f, 0.2509f, 0.2862f, 1);


    }
}
