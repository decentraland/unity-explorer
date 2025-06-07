// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace KtxUnity
{

    /// <summary>
    /// Wraps a managed byte[] in a NativeArray&lt;byte&gt;without copying memory.
    /// </summary>
    public class ManagedNativeArray : IDisposable
    {

        NativeArray<byte> m_NativeArray;
        GCHandle m_BufferHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_SafetyHandle;
#endif
        bool m_Pinned;

        /// <summary>
        /// Wraps a managed byte[] in a NativeArray&lt;byte&gt;without copying memory.
        /// </summary>
        /// <param name="original">The original byte[] to convert into a NativeArray&lt;byte&gt;</param>
        public unsafe ManagedNativeArray(byte[] original)
        {
            if (original != null)
            {
                m_BufferHandle = GCHandle.Alloc(original, GCHandleType.Pinned);
                fixed (void* bufferAddress = &original[0])
                {
                    m_NativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(bufferAddress, original.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_SafetyHandle = AtomicSafetyHandle.Create();
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(array: ref m_NativeArray, m_SafetyHandle);
#endif
                }

                m_Pinned = true;
            }
            else
            {
                m_NativeArray = new NativeArray<byte>();
            }
        }

        /// <summary>
        /// Points to the managed NativeArray&lt;byte&gt;.
        /// </summary>
        public NativeArray<byte> nativeArray => m_NativeArray;


        /// <summary>
        /// Disposes the managed NativeArray&lt;byte&gt;.
        /// </summary>
        public void Dispose()
        {
            if (m_Pinned)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(m_SafetyHandle);
#endif
                m_BufferHandle.Free();
            }
        }
    }
}
