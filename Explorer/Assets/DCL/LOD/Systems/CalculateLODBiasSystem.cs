using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class CalculateLODBiasSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;
        private float defaultFOV;
        private float defaultLodBias;
        private float defaultHeight;

        public CalculateLODBiasSystem(World world) : base(world)
        {

        }

        protected override void Update(float t)
        {
            float newHeight = Mathf.Tan(camera.GetCameraComponent(World).Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            QualitySettings.lodBias = defaultLodBias * (newHeight / defaultHeight);
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            defaultFOV = camera.GetCameraComponent(World).Camera.fieldOfView;
            defaultLodBias = QualitySettings.lodBias;
            defaultHeight = Mathf.Tan(defaultFOV * 0.5f * Mathf.Deg2Rad);
        }
    }
}
