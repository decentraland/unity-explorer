using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading.Components;
using DCL.CharacterPreview;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();
        private readonly HashSet<URN> shortenedEmotes = new ();
        private readonly IEquippedEmotes equippedEmotes;

        public AuthenticationScreenCharacterPreviewController(CharacterPreviewView view,  IEquippedEmotes equippedEmotes, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, true, characterPreviewEventBus)
        {
            this.equippedEmotes = equippedEmotes;
        }

        public override void Initialize(Avatar avatar)
        {
            shortenedWearables.Clear();
            shortenedEmotes.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            foreach (URN urn in avatar.Emotes)
            {
                if (urn.IsNullOrEmpty()) continue;
                URN shortenedUrn = urn.Shorten();

                shortenedEmotes.Add(shortenedUrn);
            }

            previewAvatarModel.Wearables = shortenedWearables;
            previewAvatarModel.Emotes = shortenedEmotes;

            base.Initialize(avatar);
        }

        private int i;
        protected void OnPointerDown(PointerEventData pointerEventData)
        {
            Debug.Log("VVV pointer selected");
            OnEmoteSlotSelected(i);
            i++;
            base.OnPointerDown(pointerEventData);
        }
        public int PlayJumpInEmote()
        {
            PlayEmote("wave");
            return 3;
        }

        private void OnEmoteSlotSelected(int slot)
        {
            IEmote? emote = equippedEmotes.EmoteInSlot(slot);
            if (emote == null) return;
            PlayEmote(emote.GetUrn().Shorten());
        }
    }
}
