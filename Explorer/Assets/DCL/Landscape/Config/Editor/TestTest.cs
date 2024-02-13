using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    public struct TestJob : IJob
    {
        private NativeArray<int> asdasd;
        private int num;

        public TestJob(ref NativeArray<int> asdasd)
        {
            this.asdasd = asdasd;
            num = 0;
        }

        public void Execute()
        {
            for (var i = 0; i < asdasd.Length; i++)
                asdasd[i] = i;
        }
    }

    public class TestTest
    {
        [Test]
        public void TestConvertExistingDataToNativeArray()
        {
            unsafe
            {
                var myArray2D = new int[2, 2] { { 0, 0 }, { 0, 0 } };

                // Get the dimensions of the 2D array
                int width = myArray2D.GetLength(0);
                int height = myArray2D.GetLength(1);

                // Pin the managed array to get a pointer to its memory
                var handle = GCHandle.Alloc(myArray2D, GCHandleType.Pinned);
                IntPtr arrayPtr = handle.AddrOfPinnedObject();

                // Calculate the total length
                int totalLength = width * height;

                // Create a NativeArray that references the existing memory
                NativeArray<int> nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>((void*)arrayPtr, totalLength, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif

                var job = new TestJob(ref nativeArray);

                JobHandle jobHandle = job.Schedule();
                jobHandle.Complete();

                for (var y = 0; y < myArray2D.GetLength(1); y++)
                {
                    for (var x = 0; x < myArray2D.GetLength(0); x++) { Debug.Log(myArray2D[y, x]); }
                }

                handle.Free();
            }
        }
    }
}
