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
        PropClosed,
        OpeningProp,
        Gliding,
        ClosingProp
    }
}
