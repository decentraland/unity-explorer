namespace ECS.StreamableLoading.DeferredLoading
{
    public struct QualityReductionRequest
    {
        public bool Reduce;

        public QualityReductionRequest(bool reduce)
        {
            Reduce = reduce;
        }
    }
}