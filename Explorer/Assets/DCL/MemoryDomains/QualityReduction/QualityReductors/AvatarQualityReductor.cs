using Arch.Core;
using ECS.StreamableLoading.DeferredLoading.Components;

namespace ECS.StreamableLoading.DeferredLoading.QualityReductors
{
    public class AvatarQualityReductor : QualityReductor
    {
        protected override QualityReductionRequestDomain GetDomain()
        {
            return QualityReductionRequestDomain.AVATAR;
        }
    }
}