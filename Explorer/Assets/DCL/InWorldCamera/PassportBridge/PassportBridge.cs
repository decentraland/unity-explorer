using Cysharp.Threading.Tasks;
using DCL.Passport;
using MVC;

namespace DCL.InWorldCamera.PassportBridge
{
    public static class PassportBridge
    {
        public static void OpenPassport(IMVCManager mvcManager, string userAddress)
        {
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userAddress))).Forget();
        }
    }
}
