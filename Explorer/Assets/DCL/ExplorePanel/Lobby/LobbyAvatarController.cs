using Arch.Core;
using CommunicationData.URLHelpers;
using Avatar = DCL.Profiles.Avatar;
using DCL.CharacterPreview;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Displays the signed-in avatar with the shared character preview renderer.</summary>
    public sealed class LobbyAvatarController : CharacterPreviewControllerBase
    {
        private readonly List<URN> wearables = new ();

        public LobbyAvatarController(CharacterPreviewView view, ICharacterPreviewFactory factory,
            World world, CharacterPreviewEventBus eventBus) : base(view, factory, world, false, eventBus)
        {
            zoomEnabled = false;
            panEnabled = false;
            rotateEnabled = false;
        }

        public override void Initialize(Avatar avatar, Vector3 position)
        {
            wearables.Clear();
            foreach (URN wearable in avatar.Wearables) wearables.Add(wearable.Shorten());
            previewAvatarModel.Wearables = wearables;
            base.Initialize(avatar, position);
        }
    }
}
