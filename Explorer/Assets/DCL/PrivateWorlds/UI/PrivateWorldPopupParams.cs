using System;

namespace DCL.PrivateWorlds.UI
{
    /// <summary>
    /// The mode/state of the private world popup.
    /// </summary>
    public enum PrivateWorldPopupMode
    {
        /// <summary>
        /// World requires a password to enter.
        /// </summary>
        PasswordRequired,

        /// <summary>
        /// User is not allowed to access the world (not on whitelist/community).
        /// </summary>
        AccessDenied
    }

    /// <summary>
    /// The result of the popup interaction.
    /// </summary>
    public enum PrivateWorldPopupResult
    {
        /// <summary>
        /// User submitted a password.
        /// </summary>
        PasswordSubmitted,

        /// <summary>
        /// User cancelled the popup.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Password was incorrect (returned from validation).
        /// </summary>
        PasswordIncorrect
    }

    /// <summary>
    /// Parameters for showing the private world popup.
    /// </summary>
    public class PrivateWorldPopupParams
    {
        /// <summary>
        /// The mode/state of the popup.
        /// </summary>
        public PrivateWorldPopupMode Mode { get; set; }

        /// <summary>
        /// The name of the world the user is trying to access.
        /// </summary>
        public string WorldName { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the world owner (for "contact owner" message).
        /// </summary>
        public string OwnerName { get; set; } = string.Empty;

        /// <summary>
        /// The wallet address of the world owner.
        /// </summary>
        public string OwnerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Optional error message to display (e.g., "Incorrect password").
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Result of the popup interaction (set by controller).
        /// </summary>
        public PrivateWorldPopupResult Result { get; set; } = PrivateWorldPopupResult.Cancelled;

        /// <summary>
        /// Password entered by user (if applicable).
        /// </summary>
        public string? EnteredPassword { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PrivateWorldPopupParams() { }

        /// <summary>
        /// Constructor with common parameters.
        /// </summary>
        public PrivateWorldPopupParams(string worldName, PrivateWorldPopupMode mode, string? ownerAddress = null)
        {
            WorldName = worldName;
            Mode = mode;
            OwnerAddress = ownerAddress ?? string.Empty;
        }

        /// <summary>
        /// Creates params for a password-required popup.
        /// </summary>
        public static PrivateWorldPopupParams ForPasswordRequired(string worldName, string ownerName, string ownerAddress, string? errorMessage = null) =>
            new ()
            {
                Mode = PrivateWorldPopupMode.PasswordRequired,
                WorldName = worldName,
                OwnerName = ownerName,
                OwnerAddress = ownerAddress,
                ErrorMessage = errorMessage
            };

        /// <summary>
        /// Creates params for an access-denied popup.
        /// </summary>
        public static PrivateWorldPopupParams ForAccessDenied(string worldName, string ownerName, string ownerAddress) =>
            new ()
            {
                Mode = PrivateWorldPopupMode.AccessDenied,
                WorldName = worldName,
                OwnerName = ownerName,
                OwnerAddress = ownerAddress
            };
    }
}
