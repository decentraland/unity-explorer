using DCL.ECSComponents;

namespace DCL.CharacterCamera
{
    public static class CameraExtensions
    {
        public static CameraType ToSDKCameraType(this CameraMode mode)
        {
            return mode switch
                   {
                       CameraMode.FirstPerson => CameraType.CtFirstPerson,
                       _ => CameraType.CtThirdPerson,
                   };
        }
    }
}
