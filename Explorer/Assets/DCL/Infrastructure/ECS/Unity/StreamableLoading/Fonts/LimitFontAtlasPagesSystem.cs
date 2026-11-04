using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;

namespace ECS.StreamableLoading.Fonts
{
    /// <summary>
    ///     Caps the atlas pages of the cached scene fonts every frame. The cache is shared by all scene worlds, so each of them
    ///     checks every font: the check reads two fields per font asset.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SDK_FONTS)]
    public partial class LimitFontAtlasPagesSystem : BaseUnityLoopSystem
    {
        private readonly FontsCache cache;

        internal LimitFontAtlasPagesSystem(World world, FontsCache cache) : base(world)
        {
            this.cache = cache;
        }

        protected override void Update(float t)
        {
            cache.LimitAtlasPages();
        }
    }
}
