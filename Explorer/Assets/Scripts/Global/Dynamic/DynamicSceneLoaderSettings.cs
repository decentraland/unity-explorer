using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Global.Dynamic
{
    [CreateAssetMenu(fileName = "DynamicSceneLoaderSettings", menuName = "SO/DynamicSceneLoaderSettings")]
    public class DynamicSceneLoaderSettings : ScriptableObject
    {
        [field: SerializeField] public Vector2Int StartPosition { get; private set; }

        [Tooltip("If it's 0, it will load every parcel in the range")]
        [field: SerializeField] public List<int2> StaticLoadPositions { get; private set; }
        [field: SerializeField] public List<string> Realms { get; private set; }
        [field: SerializeField] public string AuthWebSocketUrl { get; private set; }
        [field: SerializeField] public string AuthSignatureUrl { get; private set; }
        [field: SerializeField] public List<string> Web3WhitelistMethods { get; private set; }
    }
}
