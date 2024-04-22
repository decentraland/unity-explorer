using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using DCL.ECSComponents;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerSDKDataComponent : IDirtyMarker
    {
        public ISceneFacade SceneFacade;
        public CRDTEntity CRDTEntity;
        public string Address;
        public bool IsGuest;
        public string Name;
        public URN BodyShapeURN;
        public Color SkinColor;
        public Color EyesColor;
        public Color HairColor;

        public IReadOnlyList<URN> EmoteUrns;
        public IReadOnlyCollection<URN> WearableUrns;
        public URN PreviousEmote;
        public URN PlayingEmote;
        public bool LoopingEmote;
        public Entity SceneWorldEntity;
        public bool IsDirty { get; set; }
    }
}
