using Arch.Core;
using DCL.Billboard.DemoTest.World;
using UnityEngine;

namespace ECS.Unity.Billboard.DemoTest
{
    public class BillboardDemoTestBoot : MonoBehaviour
    {
        [SerializeField] private float cubeStep = 3;
        [SerializeField] private Vector3 cubeSize = new (1.6f, 1, 0.5f);
        [SerializeField] private int randomCounts = 5;
        [SerializeField] private int countInRow = 10;

        private async void Start()
        {
            await new BillboardDemoWorld(World.Create(), cubeSize, countInRow, randomCounts, cubeStep)
               .Run(destroyCancellationToken);
        }
    }
}
