using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.CharacterPreview.Components;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.UsersMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS.LifeCycle.Components;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Users
{
    internal partial class UsersMarkersHotAreaController : MapLayerControllerBase, IMapLayerController
    {
        private readonly IObjectPool<HotUserMarkerObject> objectsPool;
        private readonly IObjectPool<IHotUserMarker> wrapsPool;
        private readonly RemoteUsersRequestController remoteUsersRequestController;
        private TrackPlayersPositionSystem trackSystem;
        private RemovedTrackedPlayersPositionSystem untrackSystem;

        private readonly Dictionary<string, IHotUserMarker> markers = new ();
        private readonly List<string> remoteUsers = new ();
        private readonly List<string> closebyUsers = new ();
        private bool isEnabled;
        private bool isInitialised;

        public UsersMarkersHotAreaController(
            IObjectPool<HotUserMarkerObject> objectsPool,
            IObjectPool<IHotUserMarker> wrapsPool,
            Transform parent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrls)
            : base(parent, coordsUtils, cullingController)
        {
            this.objectsPool = objectsPool;
            this.wrapsPool = wrapsPool;
            remoteUsersRequestController = new RemoteUsersRequestController(webRequestController, decentralandUrls);
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();
            wrapsPool.Clear();
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            trackSystem = TrackPlayersPositionSystem.InjectToWorld(ref builder);
            trackSystem.SetQueryMethod(SetPlayerMarkerQuery);
            untrackSystem = RemovedTrackedPlayersPositionSystem.InjectToWorld(ref builder);
            untrackSystem.SetQueryMethod(RemoveMarkerQuery);
            trackSystem.Activate();
            untrackSystem.Activate();
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(CharacterPreviewComponent), typeof(DeleteEntityIntention))]
        private void SetPlayerMarker(in CharacterTransform transformComponent, in AvatarShapeComponent avatarShape)
        {
            if (!isEnabled)
                return;

            if (remoteUsers.Contains(avatarShape.ID))
                remoteUsers.Remove(avatarShape.ID);

            if (markers.TryGetValue(avatarShape.ID, out var marker))
            {
                marker.UpdateMarkerPosition(avatarShape.ID, transformComponent.Transform.position);
                mapCullingController.SetTrackedObjectPositionDirty(marker);
            }
            else
            {
                closebyUsers.Add(avatarShape.ID);
                var wrap = wrapsPool.Get();
                markers.Add(avatarShape.ID, wrap);
                mapCullingController.StartTracking(wrap, wrap);
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void RemoveMarker(in AvatarShapeComponent avatarShape)
        {
            closebyUsers.Remove(avatarShape.ID);
            if (markers.TryGetValue(avatarShape.ID, out var marker))
            {
                mapCullingController.StopTracking(marker);
                wrapsPool.Release(marker);
                markers.Remove(avatarShape.ID);
            }
        }

        private async UniTask ProcessRemoteUsers(CancellationToken ct)
        {
            List<RemotePlayersDTOs.RemotePlayerData> remotePlayersData = await remoteUsersRequestController.RequestUsers(ct);

            //Reset the markers bound to remote users by releasing them
            foreach (string remoteUser in remoteUsers)
            {
                if(closebyUsers.Contains(remoteUser)) continue;

                if (!markers.TryGetValue(remoteUser, out var marker)) continue;

                mapCullingController.StopTracking(marker);
                wrapsPool.Release(marker);
            }
            remoteUsers.Clear();

            foreach (RemotePlayersDTOs.RemotePlayerData remotePlayerData in remotePlayersData)
            {
                remoteUsers.Add(remotePlayerData.avatarId);

                if (closebyUsers.Contains(remotePlayerData.avatarId))
                    continue;

                var wrap = wrapsPool.Get();
                markers.Add(remotePlayerData.avatarId, wrap);
                wrap.UpdateMarkerPosition(remotePlayerData.avatarId, remotePlayerData.position);
                mapCullingController.StartTracking(wrap, wrap);
            }
        }

        public async UniTask Enable(CancellationToken cancellationToken)
        {
            isEnabled = true;

            if (!isInitialised)
            {
                isInitialised = true;
                await ProcessRemoteUsers(cancellationToken);
            }
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            isEnabled = false;

            foreach (IHotUserMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                wrapsPool.Release(marker);
            }

            wrapsPool.Clear();
            markers.Clear();
            return UniTask.CompletedTask;
        }
    }

    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrackPlayersPositionSystem : ControllerECSBridgeSystem
    {
        internal TrackPlayersPositionSystem(World world) : base(world) { }
    }

    [UpdateAfter(typeof(AvatarGroup))]
    [UpdateAfter(typeof(TrackPlayersPositionSystem))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RemovedTrackedPlayersPositionSystem : ControllerECSBridgeSystem
    {
        internal RemovedTrackedPlayersPositionSystem(World world) : base(world) { }
    }
}
