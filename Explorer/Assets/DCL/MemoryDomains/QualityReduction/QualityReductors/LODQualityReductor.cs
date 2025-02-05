using ECS.StreamableLoading.DeferredLoading.Components;

namespace ECS.StreamableLoading.DeferredLoading.QualityReductors
{
    public class LODQualityReductor : QualityReductor
    {
        protected override QualityReductionRequestDomain GetDomain()
        {
            return QualityReductionRequestDomain.LOD;
        }
    }
}