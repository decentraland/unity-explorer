using MVC;

namespace DCL.InWorldCamera.PassportBridge
{
    public interface IPassportBridge
    {
        void OpenPassport(IMVCManager mvcManager, string userAddress);
    }
}
