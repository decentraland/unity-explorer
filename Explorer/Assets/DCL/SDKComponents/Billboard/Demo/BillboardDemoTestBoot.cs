using Cysharp.Threading.Tasks;
using DCL.Billboard.Demo.World;
using DCL.DemoWorlds;
using UnityEngine;

namespace DCL.Billboard.Demo
{
    public class BillboardDemoTestBoot : MonoBehaviour
    {
        [SerializeField] private float cubeStep = 3;
        [SerializeField] private Vector3 cubeSize = new (1.6f, 1, 0.5f);
        [SerializeField] private int randomCounts = 5;
        [SerializeField] private int countInRow = 10;

        private void Start()
        {
            new BillboardDemoWorld(Arch.Core.World.Create(), cubeSize, countInRow: countInRow, randomCounts: randomCounts, spawnStep: cubeStep)
               .SetUpAndRunAsync(destroyCancellationToken)
               .Forget();
        }
    }
}
