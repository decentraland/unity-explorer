// SPDX-License-Identifier: Apache-2.0

namespace UnitedAV
{
    public enum MediaPathType
    {
        AbsolutePathOrURL = 0,
        RelativeToProjectFolder = 1,
        RelativeToStreamingAssetsFolder = 2,
        RelativeToDataFolder = 3,
        RelativeToPersistentDataFolder = 4,
    }

    public enum ErrorCode
    {
        None = 0,
        LoadFailed = 100,
        DecodeFailed = 200,
        InvalidPermissions = 300,
        InvalidOperation = 400,
    }
}
