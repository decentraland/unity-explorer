using DCL.ECSComponents;

namespace DCL.SDKComponents.AudioEffectZone.Components
{
    /// <summary>
    /// Marker component added to entities with <see cref="PBAudioEffectZone"/>.
    /// Tracks that the zone has been initialized and its trigger area created.
    /// </summary>
    public struct AudioEffectZoneComponent { }

    public struct SilenceZoneComponent
    {
        public string? ExcludeIds;
    }

    public struct DespatializationAudioZoneComponent { }

    public struct AmplificationZoneComponent
    {
        public float VolumeMultiplier;
        public float DistanceMultiplier;
    }

    public struct ReverbAudioZoneComponent
    {
        public PBAudioEffectZone.Types.ReverbPreset Preset;
    }

    public struct EchoZoneComponent { }
}
