using DCL.Diagnostics;
using DCL.RealmNavigation.LoadingOperation;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public abstract class StartUpOperationBase : LoadingOperationBase<IStartupOperation.Params>
    {
        protected StartUpOperationBase(string reportCategory = ReportCategory.SCENE_LOADING) : base(reportCategory) { }
    }
}
