using DCL.Diagnostics;
using System;

namespace DCL.AvatarRendering.Loading
{
    /// <summary>
    ///     Validates the Builder collection IDs supplied through the "self-preview-builder-collections" application argument.
    /// </summary>
    public static class BuilderCollectionIds
    {
        /// <summary>
        ///     Tells whether the given ID may be substituted into <see cref="LoadingConstants.BUILDER_DTO_URL_COL_ID_PLACEHOLDER" />,
        ///     logging a warning for the IDs that are turned down.
        /// </summary>
        /// <remarks>
        ///     Every accepted ID ends up in a URL that is requested with the user's Web3 signature attached. The template prefix is
        ///     absolute, so the host cannot be swapped, but an ID carrying "?", "#" or "../" resolves to a different builder-api
        ///     endpoint - "#", for instance, truncates the trailing path segment - and spends that signature on it. Builder
        ///     collection IDs are GUIDs, and demanding that shape rules out the whole family of such inputs.
        /// </remarks>
        public static bool IsValid(string collectionId, ReportData reportData)
        {
            if (Guid.TryParse(collectionId, out _))
                return true;

            ReportHub.LogWarning(reportData, $"Skipping Builder collection id '{collectionId}': expected a GUID.");
            return false;
        }
    }
}
