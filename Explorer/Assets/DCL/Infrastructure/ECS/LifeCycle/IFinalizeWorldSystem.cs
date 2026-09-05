using Arch.Core;

namespace ECS.LifeCycle
{
    /// <summary>
    ///     Executes clean-up logic right before World destruction
    /// </summary>
    public interface IFinalizeWorldSystem
    {
        /// <summary>
        ///     Executes certain clean-up logic on SDK Components
        /// </summary>
        /// <param name="query">All = typeof(CRDTEntity)</param>
        void FinalizeComponents(in Query query);
    }
}
