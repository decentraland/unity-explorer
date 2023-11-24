using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using Ipfs;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ParcelsService
{
    public class TeleportController : ITeleportController
    {
        private readonly ICharacterObject characterObject;

        private IRetrieveScene retrieveScene;

        public TeleportController(ICharacterObject characterObject)
        {
            this.characterObject = characterObject;
        }

        public void InvalidateRealm()
        {
            retrieveScene = null;
        }

        public void OnRealmLoaded(IRetrieveScene retrieveScene, World world)
        {
            this.retrieveScene = retrieveScene;
            this.retrieveScene.World = world;
        }

        public async UniTask TeleportToSceneSpawnPoint(Vector2Int parcel, CancellationToken ct)
        {
            // If type of retrieval is not set yet
            if (retrieveScene == null)
            {
                TeleportToParcel(parcel);
                return;
            }

            IpfsTypes.SceneEntityDefinition sceneDef = await retrieveScene.ByParcel(parcel, ct);

            Vector3 targetPosition;

            if (sceneDef != null)
            {
                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(sceneDef.metadata.scene.DecodedBase);

                List<IpfsTypes.SceneMetadata.SpawnPoint> spawnPoints = sceneDef.metadata.spawnPoints;

                if (spawnPoints.Count > 0)
                {
                    // TODO transfer obscure logic of how to pick the desired spawn point from the array
                    // For now just pick default/first

                    IpfsTypes.SceneMetadata.SpawnPoint spawnPoint = spawnPoints[0];

                    for (var i = 0; i < spawnPoints.Count; i++)
                    {
                        IpfsTypes.SceneMetadata.SpawnPoint sp = spawnPoints[i];
                        if (!sp.@default) continue;

                        spawnPoint = sp;
                        break;
                    }

                    Vector3 offset = GetOffsetFromSpawnPoint(spawnPoint);

                    // TODO validate offset position is within bounds of one of scene parcels

                    targetPosition += offset;
                }
            }
            else
                targetPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel);

            // Prevent conflict with Interpolation System
            // TODO move this to the system

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            characterObject.Move(targetPosition);
        }

        private static Vector3 GetOffsetFromSpawnPoint(IpfsTypes.SceneMetadata.SpawnPoint spawnPoint)
        {
            if (spawnPoint.SP != null)
            {
                IpfsTypes.SceneMetadata.SpawnPoint.SinglePosition val = spawnPoint.SP.Value;
                return new Vector3(val.x, val.y, val.z);
            }

            if (spawnPoint.MP != null)
            {
                static float GetMidPoint(float[] coordArray)
                {
                    var sum = 0f;

                    for (var i = 0; i < coordArray.Length; i++)
                        sum += (int)coordArray[i];

                    return sum / coordArray.Length;
                }

                IpfsTypes.SceneMetadata.SpawnPoint.MultiPosition val = spawnPoint.MP.Value;
                return new Vector3(GetMidPoint(val.x), GetMidPoint(val.y), GetMidPoint(val.z));
            }

            // Center
            return new Vector3(ParcelMathHelper.PARCEL_SIZE / 2f, 0, ParcelMathHelper.PARCEL_SIZE / 2f);
        }

        public void TeleportToParcel(Vector2Int parcel)
        {
            Vector3 characterPos = ParcelMathHelper.GetPositionByParcelPosition(parcel);
            characterObject.Move(characterPos);
        }
    }
}
