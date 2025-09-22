using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public interface IDriveInfoProvider
    {
        List<Utility.PlatformUtils.DriveData> GetDrivesInfo();
        string GetPersistentDataPath();
    }

    public class PlatformDriveInfoProvider : IDriveInfoProvider
    {
        public List<Utility.PlatformUtils.DriveData> GetDrivesInfo()
        {
            // Get the original list from your native utility
            var sourceDrives = Utility.PlatformUtils.GetAllDrivesInfo();

            // Manually create and populate the new list
            var resultList = new List<Utility.PlatformUtils.DriveData>(sourceDrives.Count);

            foreach (var sourceDrive in sourceDrives)
            {
                resultList.Add(new Utility.PlatformUtils.DriveData
                {
                    Name = sourceDrive.Name, AvailableFreeSpace = sourceDrive.AvailableFreeSpace
                });
            }

            return resultList;
        }

        public string GetPersistentDataPath()
        {
            return Application.persistentDataPath;
        }
    }
}