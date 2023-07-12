using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadTextureSystem))]
    public partial class TextureDeferredLoadingSystem : DeferredLoadingSystem<Texture2D, GetTextureIntention>
    {
        public TextureDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
            : base(world, concurrentLoadingBudgetProvider) { }
    }
}
