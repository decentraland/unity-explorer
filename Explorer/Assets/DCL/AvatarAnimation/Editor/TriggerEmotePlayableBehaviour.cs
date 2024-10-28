
using Arch.Core;
using DCL.AvatarRendering.Emotes;
using DCL.Profiles;
using Global.Dynamic;
using UnityEngine.Playables;
using Utility.Arch;

namespace DCL.AvatarAnimation
{
    /// <summary>
    /// A playable / clip for the Unity timeline that takes a URN (either local or remote) and tells an avatar to play it.
    /// </summary>
    public class TriggerEmotePlayableBehaviour : BaseAvatarPlayableBehaviour
    {
        public string URN;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            base.ProcessFrame(playable, info, playerData);

            if(string.IsNullOrEmpty(URN))
                return;

            if (cachedEntity != Entity.Null)
            {
                ref CharacterEmoteComponent emote = ref GlobalWorld.ECSWorldInstance.TryGetRef<CharacterEmoteComponent>(cachedEntity, out bool emoteExists);

                // Prevents the emote from playing while there is an emote already playing
                if (!emoteExists || emote.CurrentEmoteReference != null)
                    return;
            }

            // If there is not any emote pending to be loaded / played
            if (cachedEntity != Entity.Null && !GlobalWorld.ECSWorldInstance.Has<CharacterEmoteIntent>(cachedEntity))
            {
                // The emote is added to the profile so it will be automatically be downloaded if necessary
                Profile profile = GlobalWorld.ECSWorldInstance.Get<Profile>(cachedEntity);
                profile.Avatar.AddEmote(URN);
                profile.IsDirty = true;

                // It adds the emote intent (which will be consumed and removed by the CharacterEmoteSystem) if it was not already added
                CharacterEmoteIntent emoteIntent = new (){ EmoteId =  URN, TriggerSource = TriggerSource.SELF, Spatial = true};
                GlobalWorld.ECSWorldInstance.Add(cachedEntity, emoteIntent);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if(cachedAvatar == null || cachedEntity == Entity.Null)
                return;

            // Cancels loading any pending emote
            GlobalWorld.ECSWorldInstance.TryRemove<CharacterEmoteIntent>(cachedEntity);

            ref CharacterEmoteComponent emote = ref GlobalWorld.ECSWorldInstance.TryGetRef<CharacterEmoteComponent>(cachedEntity, out bool emoteExists);

            if (emoteExists)
            {
                // Only stop what is playing
                if(emote.CurrentEmoteReference == null)
                    return;

                // Tells the CharacterEmoteSystem to stop the emote
                emote.StopEmote = true;
            }
        }
    }
}
