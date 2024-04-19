using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerSDKDataComponent
    {
        public ISceneFacade SceneFacade;
        public Entity SceneWorldEntity;
        public CRDTEntity CRDTEntity;
        public string Address;
        public bool IsGuest;
        public string Name;
        public URN BodyShapeURN;
        public Color SkinColor;
        public Color EyesColor;
        public Color HairColor;
        // repeated string wearable_urns = 1;
        // repeated string emotes_urns = 2;
    }
}
