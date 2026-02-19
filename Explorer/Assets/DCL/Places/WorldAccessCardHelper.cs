using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PrivateWorlds;
using System;
using System.Threading;

namespace DCL.Places
{
    /// <summary>
    /// Shared helper for checking world access and updating place card UI.
    /// Used by PlacesResultsView and PlacesSectionView to avoid duplication.
    /// </summary>
    public static class WorldAccessCardHelper
    {
        public static async UniTaskVoid CheckAndUpdateCardAsync(
            IWorldPermissionsService worldPermissionsService,
            string worldName,
            PlaceCardView cardView,
            CancellationToken ct)
        {
            try
            {
                WorldAccessCheckContext context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);
                cardView.SetWorldAccessState(context.Result, context.AccessInfo?.AccessType);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"Failed to check world access for '{worldName}': {e.Message}");
            }
        }
    }
}
