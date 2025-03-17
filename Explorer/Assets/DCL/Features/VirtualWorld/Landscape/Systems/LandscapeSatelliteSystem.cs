using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    /// <summary>
    ///     This system is the one that creates the ground textures for the satellite view, also manages their visibility status based on the settings data
    /// </summary>
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeSatelliteSystem : BaseUnityLoopSystem
    {
        private readonly SatelliteFloor floor;
        private readonly MapRendererTextureContainer textureContainer;

        private bool isViewRendered;

        private LandscapeSatelliteSystem(World world,
            MapRendererTextureContainer textureContainer,
            SatelliteFloor floor) : base(world)
        {
            this.textureContainer = textureContainer;
            this.floor = floor;
        }

        protected override void Update(float t)
        {
            if (textureContainer.IsComplete() && !isViewRendered)
            {
                floor.Create(textureContainer);
                isViewRendered = true;
            }
        }
    }
}
