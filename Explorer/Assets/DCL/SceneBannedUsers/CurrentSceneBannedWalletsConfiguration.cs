using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneBannedUsers
{
    [CreateAssetMenu(fileName = "CurrentSceneBannedWallets", menuName = "DCL/SceneBannedUsers")]
    public class CurrentSceneBannedWalletsConfiguration : ScriptableObject
    {
        [SerializeField] public List<string> bannedWallets;
    }
}
