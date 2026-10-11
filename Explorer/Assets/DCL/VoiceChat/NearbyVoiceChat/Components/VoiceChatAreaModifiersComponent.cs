using Arch.Core;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby
{
    public readonly struct VoiceChatAreaModifier
    {
        public readonly World SceneWorld;
        public readonly Entity AreaEntity;
        public readonly float VolumeScale;
        public readonly float MaxDistance;
        public readonly bool Mute;
        public readonly bool Isolate;

        public VoiceChatAreaModifier(World sceneWorld, Entity areaEntity, float volumeScale, float maxDistance, bool mute, bool isolate)
        {
            SceneWorld = sceneWorld;
            AreaEntity = areaEntity;
            VolumeScale = volumeScale;
            MaxDistance = maxDistance;
            Mute = mute;
            Isolate = isolate;
        }

        public bool IsSameArea(in VoiceChatAreaModifier other) =>
            ReferenceEquals(SceneWorld, other.SceneWorld) && AreaEntity == other.AreaEntity;
    }

    /// <summary>
    ///     Voice modifiers of every <c>VoiceChatModifierArea</c> the avatar currently stands in, resolved most-restrictive-wins.
    /// </summary>
    public struct VoiceChatAreaModifiersComponent
    {
        public readonly List<VoiceChatAreaModifier> Modifiers;

        public VoiceChatAreaModifiersComponent(List<VoiceChatAreaModifier> modifiers)
        {
            Modifiers = modifiers;
        }

        public readonly float VolumeScale
        {
            get
            {
                var volume = 1f;

                foreach (VoiceChatAreaModifier modifier in Modifiers)
                    if (modifier.VolumeScale < volume)
                        volume = modifier.VolumeScale;

                return volume;
            }
        }

        public readonly float MaxDistance
        {
            get
            {
                float distance = float.PositiveInfinity;

                foreach (VoiceChatAreaModifier modifier in Modifiers)
                    if (modifier.MaxDistance < distance)
                        distance = modifier.MaxDistance;

                return distance;
            }
        }

        public readonly bool Muted
        {
            get
            {
                foreach (VoiceChatAreaModifier modifier in Modifiers)
                    if (modifier.Mute)
                        return true;

                return false;
            }
        }

        /// <summary>
        ///     True when both sides share an isolating area, or neither stands in one.
        /// </summary>
        public static bool CanHear(List<VoiceChatAreaModifier>? speaker, List<VoiceChatAreaModifier>? listener)
        {
            bool speakerIsolated = false;

            if (speaker != null)
                foreach (VoiceChatAreaModifier speakerModifier in speaker)
                {
                    if (!speakerModifier.Isolate) continue;

                    speakerIsolated = true;

                    if (listener == null) continue;

                    foreach (VoiceChatAreaModifier listenerModifier in listener)
                        if (listenerModifier.Isolate && listenerModifier.IsSameArea(speakerModifier))
                            return true;
                }

            if (speakerIsolated) return false;

            if (listener != null)
                foreach (VoiceChatAreaModifier listenerModifier in listener)
                    if (listenerModifier.Isolate)
                        return false;

            return true;
        }
    }
}
