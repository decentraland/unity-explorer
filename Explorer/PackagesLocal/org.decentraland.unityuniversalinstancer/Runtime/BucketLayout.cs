// Bucket addressing shared by the cull job, the renderer entry and the
// submitter. A bucket is one (draw mode, fade state, LOD level) triple; every
// instance the cull job keeps lands in one to four of them.

namespace GPUInstancerPro
{
    internal static class BucketLayout
    {
        public const int LEVELS = 4;
        public const int FADE_STATES = 2;
        public const int MODES = 3;
        public const int COUNT = MODES * FADE_STATES * LEVELS;

        // Draw modes: which passes a bucket is submitted to.
        public const int MODE_BOTH = 0;        // colour draw that also casts shadows
        public const int MODE_COLOR_ONLY = 1;  // colour draw, ShadowCastingMode.Off
        public const int MODE_SHADOW_ONLY = 2; // ShadowCastingMode.ShadowsOnly

        public const int FADE_STEADY = 0;
        public const int FADE_CROSSFADING = 1;

        // Indirect-args entries: one per (LOD slot, mode, fade state).
        public const int ARG_ENTRIES_PER_SLOT = MODES * FADE_STATES;
        public const int ARG_UINTS_PER_ENTRY = 5;

        public static int Index(int mode, int fadeState, int level) =>
            (((mode * FADE_STATES) + fadeState) * LEVELS) + level;

        public static int Level(int bucket) => bucket % LEVELS;

        public static int FadeState(int bucket) => (bucket / LEVELS) % FADE_STATES;

        public static int Mode(int bucket) => bucket / (LEVELS * FADE_STATES);

        public static int ArgEntry(int slot, int mode, int fadeState) =>
            (slot * ARG_ENTRIES_PER_SLOT) + (mode * FADE_STATES) + fadeState;
    }
}
