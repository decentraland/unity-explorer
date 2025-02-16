using UnityEngine.Serialization;

namespace ECS.StreamableLoading.DeferredLoading.Components
{
    public enum QualityReductionRequestType
    {
        REDUCE,
        INCREASE
    }

    public enum QualityReductionRequestDomain
    {
        AVATAR,
        LOD
    }

    public struct QualityChangeRequest
    {
        private QualityReductionRequestType ReduceType;
        public QualityReductionRequestDomain Domain;

        public static QualityChangeRequest Reduced(QualityReductionRequestDomain domain)
        {
            return new QualityChangeRequest
            {
                ReduceType = QualityReductionRequestType.REDUCE, Domain = domain
            };
        }

        public static QualityChangeRequest Increased(QualityReductionRequestDomain domain)
        {
            return new QualityChangeRequest
            {
                ReduceType = QualityReductionRequestType.INCREASE, Domain = domain
            };
        }

        public bool IsReduce()
        {
            return ReduceType == QualityReductionRequestType.REDUCE;
        }
    }
}