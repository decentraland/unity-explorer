using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTableView : MonoBehaviour
    {
        public MinimumSpecsRowView[] Rows
            => GetComponentsInChildren<MinimumSpecsRowView>();
    }
}