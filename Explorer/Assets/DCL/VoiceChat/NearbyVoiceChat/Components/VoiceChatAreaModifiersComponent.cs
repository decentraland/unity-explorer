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

        public float VolumeScale { get; private set; }
        public float MaxDistance { get; private set; }
        public bool Muted { get; private set; }
        public bool Isolated { get; private set; }

        public VoiceChatAreaModifiersComponent(List<VoiceChatAreaModifier> modifiers) : this()
        {
            Modifiers = modifiers;
            Resolve();
        }

        /// <summary>
        ///     Recomputes the resolved values from <see cref="Modifiers" />; call after every change to it.
        /// </summary>
        public void Resolve()
        {
            VolumeScale = 1f;
            MaxDistance = float.PositiveInfinity;
            Muted = false;
            Isolated = false;

            foreach (VoiceChatAreaModifier modifier in Modifiers)
            {
                if (modifier.VolumeScale < VolumeScale) VolumeScale = modifier.VolumeScale;
                if (modifier.MaxDistance < MaxDistance) MaxDistance = modifier.MaxDistance;
                Muted |= modifier.Mute;
                Isolated |= modifier.Isolate;
            }
        }

        /// <summary>
        ///     True when both sides share an isolating area, or neither stands in one.
        /// </summary>
        public static bool CanHear(in VoiceChatAreaModifiersComponent speaker, in VoiceChatAreaModifiersComponent listener)
        {
            if (!speaker.Isolated) return !listener.Isolated;
            if (!listener.Isolated) return false;

            foreach (VoiceChatAreaModifier speakerModifier in speaker.Modifiers)
            {
                if (!speakerModifier.Isolate) continue;

                foreach (VoiceChatAreaModifier listenerModifier in listener.Modifiers)
                    if (listenerModifier.Isolate && listenerModifier.IsSameArea(speakerModifier))
                        return true;
            }

            return false;
        }
    }
}
