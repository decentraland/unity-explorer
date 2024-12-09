using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.PassportBridge;
using DCL.Passport;
using MVC;

namespace DCL.InWorldCamera.PassportBridgeOpener
{
    public class PassportBridgeOpener : IPassportBridge
    {
        public void OpenPassport(IMVCManager mvcManager, string userAddress)
        {
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userAddress))).Forget();
        }
    }
}
