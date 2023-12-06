using DCL.Character;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public class ParcelServiceContainer
    {
        public RetrieveSceneFromVolatileWorld RetrieveSceneFromVolatileWorld { get; private set; }
        public RetrieveSceneFromFixedRealm RetrieveSceneFromFixedRealm { get; private set; }
        public TeleportController TeleportController { get; private set; }

        public static ParcelServiceContainer Create(IRealmData realmData, ICharacterObject characterObject, IDebugContainerBuilder debugContainerBuilder)
        {
            var teleportController = new TeleportController(characterObject);

            BuildDebugWidget(teleportController, debugContainerBuilder);

            return new ParcelServiceContainer
            {
                RetrieveSceneFromFixedRealm = new RetrieveSceneFromFixedRealm(),
                RetrieveSceneFromVolatileWorld = new RetrieveSceneFromVolatileWorld(realmData),
                TeleportController = teleportController,
            };
        }

        private static void BuildDebugWidget(ITeleportController teleportController, IDebugContainerBuilder debugContainerBuilder)
        {
            var binding = new ElementBinding<Vector2Int>(Vector2Int.zero);

            debugContainerBuilder.AddWidget("Teleport")
                                 .AddControl(new DebugVector2IntFieldDef(binding), null)
                                 .AddControl(
                                      new DebugButtonDef("To Parcel", () => teleportController.TeleportToParcel(binding.Value)),
                                      new DebugButtonDef("To Spawn Point", () => teleportController.TeleportToSceneSpawnPointAsync(binding.Value, CancellationToken.None)));
        }
    }
}
