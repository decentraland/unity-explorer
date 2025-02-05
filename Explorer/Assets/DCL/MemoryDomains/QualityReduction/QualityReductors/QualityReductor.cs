using Arch.Core;
using ECS.StreamableLoading.DeferredLoading.Components;

namespace ECS.StreamableLoading.DeferredLoading.QualityReductors
{
    public abstract class QualityReductor
    {
        private bool IsQualityReduced { get; set; }

        public void RequestQualityReduction(World world)
        {
            if (!IsQualityReduced)
            {
                world.Create(QualityChangeRequest.Reduced(GetDomain()));
                IsQualityReduced = true;
            }
        }

        public void RequestQualityIncrease(World world)
        {
            if (IsQualityReduced)
            {
                world.Create(QualityChangeRequest.Increased(GetDomain()));
                IsQualityReduced = false;
            }
        }

        protected abstract QualityReductionRequestDomain GetDomain();
    }
}