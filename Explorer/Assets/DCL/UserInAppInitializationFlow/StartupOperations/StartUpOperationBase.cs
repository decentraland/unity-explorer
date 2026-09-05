using DCL.Diagnostics;
using DCL.RealmNavigation.LoadingOperation;

namespace DCL.UserInAppInitializationFlow
{
    public abstract class StartUpOperationBase : LoadingOperationBase<IStartupOperation.Params>, IStartupOperation
    {
        protected StartUpOperationBase(string reportCategory = ReportCategory.STARTUP) : base(reportCategory) { }
    }
}
