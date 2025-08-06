using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTableView : MonoBehaviour
    {
        [field: Tooltip("Row that closes the table grid. Always presented since we have at least one entry when showing this modal.")]
        [field: SerializeField] public MinimumSpecsRowView LastRow { get; private set; }

        [field: Tooltip("Template to spawn when user have more than one requirements not met.")]
        [field: SerializeField] public MinimumSpecsRowView RowTemplate { get; private set; }
    }
}
