using System;

namespace DCL.PrivateWorlds.UI
{
    /// <summary>
    /// The mode/state of the private world popup.
    /// </summary>
    public enum PrivateWorldPopupMode
    {
        PasswordRequired,
        AccessDenied
    }

    /// <summary>
    /// The result of the popup interaction.
    /// </summary>
    public enum PrivateWorldPopupResult
    {
        PasswordSubmitted,
        Cancelled
    }

    /// <summary>
    /// Parameters for showing the private world popup.
    /// </summary>
    public class PrivateWorldPopupParams
    {
        public PrivateWorldPopupMode Mode { get; }
        public string WorldName { get;}
        public string OwnerAddress { get; }
        public string? ErrorMessage { get; set; }
        public PrivateWorldPopupResult Result { get; set; } = PrivateWorldPopupResult.Cancelled;
        public string? EnteredPassword { get; set; }
        public PrivateWorldPopupParams() { }
        public PrivateWorldPopupParams(string worldName, PrivateWorldPopupMode mode, string? ownerAddress = null)
        {
            WorldName = worldName;
            Mode = mode;
            OwnerAddress = ownerAddress ?? string.Empty;
        }
    }
}
