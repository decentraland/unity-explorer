namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Late-bound holder for the <see cref="VoiceChatConfiguration"/> ScriptableObject.
    /// Created before InjectToWorld, populated in InitializeAsync after the SO is loaded.
    /// </summary>
    public class ProximityConfigHolder
    {
        public VoiceChatConfiguration? Config;
    }
}
