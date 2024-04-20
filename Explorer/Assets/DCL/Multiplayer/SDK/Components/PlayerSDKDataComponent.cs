using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using DCL.ECSComponents;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerSDKDataComponent : IDirtyMarker
    {
        public bool IsDirty { get; set; }

        public ISceneFacade SceneFacade;
        public CRDTEntity CRDTEntity;
        public string Address;
        public bool IsGuest;
        public string Name;
        public URN BodyShapeURN;
        public Color SkinColor;
        public Color EyesColor;
        public Color HairColor;
        public Entity SceneWorldEntity;

        // repeated string wearable_urns = 1;
        // repeated string emotes_urns = 2;
    }
}
