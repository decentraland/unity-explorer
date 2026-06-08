using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public interface IDriveInfoProvider
    {
        /// <summary>
        ///     Returns storage information for the drive/volume that hosts the persistent data path.
        ///     Only the relevant volume is queried, avoiding the cost of enumerating every mounted
        ///     drive (which can be very slow when network drives are present).
        /// </summary>
        Utility.PlatformUtils.DriveData? GetPersistentDataDriveInfo();
    }

    public class PlatformDriveInfoProvider : IDriveInfoProvider
    {
        public Utility.PlatformUtils.DriveData? GetPersistentDataDriveInfo() =>
            Utility.PlatformUtils.GetDriveInfoForPath(Application.persistentDataPath);
    }
}
