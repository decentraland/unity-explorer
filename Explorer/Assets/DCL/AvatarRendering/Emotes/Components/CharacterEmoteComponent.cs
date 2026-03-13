using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using System.Collections.Generic;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public IReadOnlyDictionary<string, int>? CurrentAnimationTagsByLayerName => currentAnimationTagsByLayerName;
        public bool StopEmote;
        public AvatarEmoteMask Mask;

        private Dictionary<string, int>? currentAnimationTagsByLayerName;

        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip
            ? CurrentEmoteReference.avatarClip.length * CurrentEmoteReference.animatorComp!.speed
            : 0f;

        /// <summary>
        ///     Whether an emote is being played.
        /// </summary>
        /// <remarks>
        ///     In Local Scene Development mode the method behaves slightly differently. Check the implementation for details.
        /// </remarks>
        public readonly bool IsPlayingEmote
        {
            get
            {
                // NOTE in Local Scene Development mode -- where legacy anims are allowed -- we will have different behavior

                // Legacy clips are handled with the legacy animation component
                if (CurrentEmoteReference && CurrentEmoteReference.legacy) return CurrentEmoteReference.animationComp!.isPlaying;

                // For mecanim animations, we check the actual animator tag
                // We do that because we can be in a different state even if triggers have been set (e.g., waiting for a jump to finish)
                if (currentAnimationTagsByLayerName?.TryGetValue(AnimatorEmoteLayers.BASE_LAYER, out int tag) == true)
                    return tag == AnimationHashes.EMOTE || tag == AnimationHashes.EMOTE_LOOP;

                return false;
            }
        }

        public readonly bool IsPlayingMaskedEmote
        {
            get
            {
                foreach (string layerName in AnimatorEmoteLayers.NON_BASE_LAYERS)
                    if (currentAnimationTagsByLayerName?.TryGetValue(layerName, out int tag) == true && (tag == AnimationHashes.EMOTE || tag == AnimationHashes.EMOTE_LOOP))
                        return true;

                return false;
            }
        }

        public readonly bool IsPlayingLegacyEmote => CurrentEmoteReference && CurrentEmoteReference.legacy;

        public void Reset()
        {
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
            Mask = AvatarEmoteMask.AemFullBody;
        }

        public void SetAnimationTag(string layerName, int stateHash) {
            currentAnimationTagsByLayerName ??= new Dictionary<string, int>(AnimatorEmoteLayers.ALL_LAYERS.Length);
            currentAnimationTagsByLayerName[layerName] = stateHash;
        }
    }
}
