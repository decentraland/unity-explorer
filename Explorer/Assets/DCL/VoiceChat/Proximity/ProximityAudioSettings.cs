namespace DCL.VoiceChat
{
    /// <summary>
    /// Late-bound holder for the <see cref="VoiceChatConfiguration"/> ScriptableObject.
    /// Created before InjectToWorld, populated in InitializeAsync after the SO is loaded.
    /// The SO itself is the single source of truth for all proximity audio parameters.
    /// </summary>
    public class ProximityConfigHolder
    {
        public VoiceChatConfiguration? Config;
    }
}
