using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DCL.SDKComponents.AudioEffects
{
    /// <summary>
    ///     Case-insensitive eth-address → effect list map. Keeps a reverse index
    ///     (pb → target) so <see cref="Remove"/> can find the owning chain in O(1)
    ///     and <see cref="Upsert"/> can early-out on already-registered effects.
    ///     Target changes on an already-registered pb are ignored — the effect keeps its original target.
    /// </summary>
    public sealed class SceneAudioEffectsRegistry : ISceneAudioEffectsRegistry
    {
        private static readonly StringComparer TARGET_COMPARER = StringComparer.OrdinalIgnoreCase;

        private readonly Dictionary<string, List<PBAudioSourceEffect>> avatarAudioEffects = new (TARGET_COMPARER);
        private readonly Dictionary<PBAudioSourceEffect, string> targetByEffect = new (ReferenceComparer.INSTANCE);

        public void Clear()
        {
            avatarAudioEffects.Clear();
            targetByEffect.Clear();
        }

        public void Remove(PBAudioSourceEffect pbEffect)
        {
            if (targetByEffect.Remove(pbEffect, out string targetAvatar))
                RemoveFromEffects(targetAvatar, pbEffect);
        }

        public void Upsert(string targetAvatarId, PBAudioSourceEffect pbEffect)
        {
            if (targetByEffect.ContainsKey(pbEffect))
                return;

            if (!avatarAudioEffects.TryGetValue(targetAvatarId, out List<PBAudioSourceEffect> effects))
            {
                effects = new List<PBAudioSourceEffect>();
                avatarAudioEffects[targetAvatarId] = effects;
            }

            effects.Add(pbEffect);
            targetByEffect[pbEffect] = targetAvatarId;
        }

        public bool TryGetEffects(string ethAddress, out List<PBAudioSourceEffect> effects) =>
            avatarAudioEffects.TryGetValue(ethAddress, out effects);

        private void RemoveFromEffects(string target, PBAudioSourceEffect pb)
        {
            if (!avatarAudioEffects.TryGetValue(target, out List<PBAudioSourceEffect> effects))
                return;

            for (var i = 0; i < effects.Count; i++)
            {
                if (!ReferenceEquals(effects[i], pb)) continue;

                effects.RemoveAt(i);
                break;
            }

            if (effects.Count == 0)
                avatarAudioEffects.Remove(target);
        }

        private sealed class ReferenceComparer : IEqualityComparer<PBAudioSourceEffect>
        {
            public static readonly ReferenceComparer INSTANCE = new ();

            public bool Equals(PBAudioSourceEffect x, PBAudioSourceEffect y) =>
                ReferenceEquals(x, y);

            public int GetHashCode(PBAudioSourceEffect obj) =>
                RuntimeHelpers.GetHashCode(obj);
        }
    }
}
