namespace DCL.CharacterMotion.Components
{
    public struct GlideState
    {
        public GlideStateValue Value;

        public bool WantsToGlide;

        public int CooldownStartedTick;
    }

    public enum GlideStateValue
    {
        PROP_CLOSED,
        OPENING_PROP,
        GLIDING,
        CLOSING_PROP
    }
}
