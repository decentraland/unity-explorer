using DCL.ECSComponents;
using System.Collections.Generic;

namespace DCL.SDKComponents.AudioEffects
{
    /// <summary>
    ///     Per-scene aggregated view of <see cref="PBAudioSourceEffect"/> sources targeting each avatar.
    ///     Owned by the scene-world plugin; cleared on world dispose. Updated incrementally — one
    ///     <see cref="Upsert"/> / <see cref="Remove"/> call per changed source per tick.
    /// </summary>
    public interface ISceneAudioEffectsRegistry
    {
        /// <summary>Drops every chain. Called on world dispose.</summary>
        void Clear();

        /// <summary>
        ///     Registers <paramref name="pbEffect"/> as targeting <paramref name="targetAvatarId"/>.
        ///     No-op if <paramref name="pbEffect"/> is already registered (target changes are ignored).
        /// </summary>
        void Upsert(string targetAvatarId, PBAudioSourceEffect pbEffect);

        /// <summary>Drops <paramref name="pbEffect"/> from its chain. No-op if not registered.</summary>
        void Remove(PBAudioSourceEffect pbEffect);

        /// <summary>Case-insensitive point lookup.</summary>
        bool TryGetEffects(string ethAddress, out List<PBAudioSourceEffect> effects);
    }
}
