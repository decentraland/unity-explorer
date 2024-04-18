using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Raycast.Components
{
    public struct HighlightComponent
    {
        public bool IsEnabled;
        public bool IsAtDistance;
        public Material MaterialOnUse;
        public Dictionary<EntityReference, Material[]> OriginalMaterials;
        public EntityReference CurrentEntity;
        public EntityReference NextEntity;
    }
}
