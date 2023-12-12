using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Billboard.Demo.World;
using DCL.Billboard.Extensions;
using DCL.ECSComponents;
using UnityEngine;

namespace DCL.Billboard.Demo
{
    public class BillboardPlaygroundBoot : MonoBehaviour
    {
        [SerializeField] private bool useX;
        [SerializeField] private bool useY;
        [SerializeField] private bool useZ;

        private void Start()
        {
            StartAsync();
        }

        private async void StartAsync()
        {
            var world = Arch.Core.World.Create();
            var demoWorld = new BillboardDemoWorld(world, Vector3.one, randomCounts: 0, predefinedBillboards: BillboardMode.BmNone);
            demoWorld.SetUp();

            while (this)
            {
                var query = new QueryDescription().WithAll<PBBillboard>();
                world.Query(in query, (ref PBBillboard b) => b.Apply(useX, useY, useZ));
                demoWorld.Update();
                await UniTask.Yield();
            }
        }
    }
}
