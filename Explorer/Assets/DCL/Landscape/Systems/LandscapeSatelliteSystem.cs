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
        private readonly SatelliteView view;
        private readonly MapRendererTextureContainer textureContainer;

        private bool isViewRendered;

        private LandscapeSatelliteSystem(World world,
            MapRendererTextureContainer textureContainer,
            SatelliteView view) : base(world)
        {
            this.textureContainer = textureContainer;
            this.view = view;
        }

        protected override void Update(float t)
        {
            if (textureContainer.IsComplete() && !isViewRendered)
            {
                view.Create(textureContainer);
                isViewRendered = true;
            }
        }
    }
}
