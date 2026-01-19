using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Rendering.RenderSystem;
using ECS.Abstract;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class PushPerMaterialBufferToGPUSystem : BaseUnityLoopSystem
    {
        private MaterialManager materialManager;

        internal PushPerMaterialBufferToGPUSystem(World world, MaterialManager matMan) : base(world)
        {
            this.materialManager = matMan;
        }

        protected override void Update(float t)
        {
            materialManager.EndOfFramePushtoGPU();
        }
    }
}
