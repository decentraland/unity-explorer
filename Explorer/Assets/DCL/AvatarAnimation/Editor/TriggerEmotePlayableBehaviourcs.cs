
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using Global.Dynamic;
using UnityEngine;
using UnityEngine.Playables;

namespace DCL.AvatarAnimation
{
    public class TriggerEmotePlayableBehaviour : PlayableBehaviour
    {
        public string URN;

        private Entity cachedEntity = Entity.Null;
        private AvatarBase cachedAvatar;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if(string.IsNullOrEmpty(URN))
                return;

            // Gets the asset associated to the track
            AvatarBase avatar = (AvatarBase)playerData;

            // Prevents the emote from playing while there is an emote already playing
            if(cachedEntity != Entity.Null && GlobalWorld.ECSWorldInstance.TryGetRef<CharacterEmoteComponent>(cachedEntity, out bool emoteExists).CurrentEmoteReference != null)
                return;

 //           if(avatar.IsAnimatorInTag(AnimationHashes.EMOTE) || avatar.IsAnimatorInTag(AnimationHashes.EMOTE_LOOP))
 //               return;

            // If the asset was changed in the editor, update the cached data
            if (avatar != cachedAvatar)
            {
                cachedAvatar = avatar;
                cachedEntity = FindEntityFromAvatarBase(cachedAvatar);
            }

            // It adds the emote intent (which will be consumed and removed by the CharacterEmoteSystem) if it was not already added
            if (cachedEntity != Entity.Null && !GlobalWorld.ECSWorldInstance.Has<CharacterEmoteIntent>(cachedEntity))
            {
                Debug.Log("<color=yellow>EMOTE ADDED [" + URN + "] to entity {" + cachedEntity + "} with AvatarBase {"  + cachedAvatar.name +  "}</color>");
                GlobalWorld.ECSWorldInstance.Add<CharacterEmoteIntent>(cachedEntity);
                ref CharacterEmoteIntent emoteIntent = ref GlobalWorld.ECSWorldInstance.TryGetRef<CharacterEmoteIntent>(cachedEntity, out bool exists);
                emoteIntent.EmoteId = URN;
                emoteIntent.TriggerSource = TriggerSource.SELF;
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if(cachedAvatar == null || cachedEntity == Entity.Null)
                return;

            ref CharacterEmoteComponent emote = ref GlobalWorld.ECSWorldInstance.TryGetRef<CharacterEmoteComponent>(cachedEntity, out bool exists);

            // Only stop what is playing
            if(emote.CurrentEmoteReference == null)
                return;

            Debug.Log("<color=red>EMOTE STOPPED [" + URN + "] in entity {" + cachedEntity + "} with AvatarBase {"  + cachedAvatar.name +  "}</color>");
            emote.StopEmote = true;
        }

        private static Entity FindEntityFromAvatarBase(AvatarBase avatar)
        {
            Entity foundEntity = Entity.Null;
            Query allAvatars = GlobalWorld.ECSWorldInstance.Query(new QueryDescription().WithAll<AvatarBase>().WithNone<CharacterEmoteIntent>());

            foreach (ref var chunk in allAvatars)
            {
                AvatarBase[] avatars = chunk.GetArray<AvatarBase>();
                /*AvatarBase av = chunk.GetFirst<AvatarBase>();

                if (av == avatar)
                    {
                        foundEntity = chunk.Entity(0);
                        break;
                    }*/

                foreach (int entityIndex in chunk)
                    if (entityIndex > -1 && avatars[entityIndex] == avatar)
                    {
                        foundEntity = chunk.Entity(entityIndex);
                        break;
                    }

                if (foundEntity != Entity.Null)
                    break;
            }

            return foundEntity;
        }
    }
}
