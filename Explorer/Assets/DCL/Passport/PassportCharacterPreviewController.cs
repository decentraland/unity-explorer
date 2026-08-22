using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.CharacterPreview;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Passport
{
    public class PassportCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();

        public PassportCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, false, characterPreviewEventBus) { }

        public override void Initialize(Avatar avatar, Vector3 position)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            previewAvatarModel.Wearables = shortenedWearables;

            base.Initialize(avatar, position);
            PlayEmote("wave");
        }
    }
}
