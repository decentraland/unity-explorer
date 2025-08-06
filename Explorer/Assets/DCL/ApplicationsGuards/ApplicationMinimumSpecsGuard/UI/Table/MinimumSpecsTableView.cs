using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTableView : MonoBehaviour
    {
        // Always presented
        [field: SerializeField] public MinimumSpecsRowView LastRow { get; private set; }

        // If we need more rows in between
        [field: SerializeField] public MinimumSpecsRowView RowTemplate { get; private set; }

        // [field: SerializeField] public MinimumSpecsRowView LastRow { get; private set; }
        // public MinimumSpecsRowView[] Rows => GetComponentsInChildren<MinimumSpecsRowView>();
    }
}
