namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Atlas slice indices, mouth-pose mapping, and pause-character classification used by
    ///     the avatar facial expression pipeline.
    /// </summary>
    /// <remarks>
    ///     Atlases are 1024×1024, 4×4 grid of 256px cells (top-to-bottom, left-to-right).
    ///
    ///     Eyebrows: 0 Idle, 1 Up, 2 Down, 3 Angry, 4 Sad, 5 Surprised, 6-15 Unused.
    ///     Eyes:     0 Idle, 1 HalfClosed, 2 Closed, 3 WideOpen, 4-7 Look*, 8-15 Unused.
    ///     Mouth:    0 Idle, 1 a/e/i, 2 b/m/p, 3 f/v, 4 d/th, 5 u, 6 c/g/h/k/n/s/t/x/y/z,
    ///               7 o, 8 l, 9 r, 10 ch/j/sh, 11 w/q, 12 Sad, 13 Happy, 14 Smile, 15 Worried.
    /// </remarks>
    public static class AvatarFacialExpressionConstants
    {
        public const int NO_EYEBROWS_OVERRIDE = -1;
        public const int NO_EYE_OVERRIDE = -1;
        public const int NO_MOUTH_POSE = -1;

        public const int EYE_HALF_CLOSED = 1;
        public const int EYE_CLOSED = 2;

        public const float UPPERCASE_DURATION_MULTIPLIER = 2f;

        /// <summary>Mouth-pose-rich text looped while an avatar is actively speaking in voice chat.</summary>
        public const string VOICE_CHAT_LOOP_TEXT = "el murcielago hindu comia feliz cardillo y kiwi";

        /// <summary>One blink: HalfClosed → Closed → HalfClosed → restore expression.</summary>
        public static readonly int[] BLINK_SEQUENCE = { EYE_HALF_CLOSED, EYE_CLOSED, EYE_HALF_CLOSED };

        public static bool IsVowel(char c)
        {
            c = char.ToLowerInvariant(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

        /// <summary>
        ///     Pause characters hold the expression mouth (not idle) instead of an articulated pose.
        /// </summary>
        public static bool IsPauseChar(char c) =>
            c == ',' || c == '.' || c == ';' || c == ':' || c == '!' || c == '?' || c == ' ' || c == '\n';

        /// <summary>
        ///     Maps a character at <paramref name="index"/> to a mouth pose slice. Digraphs
        ///     (th, ch, sh) detected by peeking. Pause chars and any unmapped char return
        ///     <see cref="NO_MOUTH_POSE"/> so the caller falls back to the expression mouth.
        /// </summary>
        public static int MapCharToMouthPose(string text, int index)
        {
            char c = char.ToLowerInvariant(text[index]);

            if (IsPauseChar(c))
                return NO_MOUTH_POSE;

            char next = index + 1 < text.Length ? char.ToLowerInvariant(text[index + 1]) : '\0';

            switch (c)
            {
                case 'a': case 'e': case 'i': return 1;
                case 'b': case 'm': case 'p': return 2;
                case 'f': case 'v':           return 3;
                case 't':                     return next == 'h' ? 4 : 6;
                case 'u':                     return 5;
                case 'd':                     return 4;
                case 'g': case 'h':
                case 'k': case 'n': case 'x':
                case 'y': case 'z':           return 6;
                case 'c':                     return next == 'h' ? 10 : 6;
                case 's':                     return next == 'h' ? 10 : 6;
                case 'o':                     return 7;
                case 'l':                     return 8;
                case 'r':                     return 9;
                case 'j':                     return 10;
                case 'w': case 'q':           return 11;
                default:                      return NO_MOUTH_POSE;
            }
        }
    }
}