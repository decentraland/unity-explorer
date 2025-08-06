using System.Collections.Generic;
using System.Linq;
using DCL.Diagnostics;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTablePresenter
    {
        private const string FAIL_ICON_SPRITE_TAG = "<sprite name=\"274c\">";

        private readonly MinimumSpecsTableView tableView;
        private readonly List<MinimumSpecsRowView> spawnedRows = new ();

        public MinimumSpecsTablePresenter(MinimumSpecsTableView view)
        {
            tableView = view;
        }

        public void Populate(IEnumerable<SpecResult> results)
        {
            ClearSpawnedRows();

            var unmetResults = results.Where(r => !r.IsMet).ToList();

            if (unmetResults.Count == 0)
            {
                tableView.LastRow.gameObject.SetActive(false);
                return;
            }

            PopulateRow(tableView.LastRow, unmetResults[0]);
            tableView.LastRow.gameObject.SetActive(true);

            for (var i = 1; i < unmetResults.Count; i++)
            {
                MinimumSpecsRowView newRow = Object.Instantiate(tableView.RowTemplate, tableView.RowTemplate.transform.parent);
                PopulateRow(newRow, unmetResults[i]);
                newRow.gameObject.SetActive(true);
                spawnedRows.Add(newRow);
            }
        }

        private void PopulateRow(MinimumSpecsRowView row, SpecResult result)
        {
            var formattedActualText = $"{FAIL_ICON_SPRITE_TAG} {result.Actual}";

            row.SetTitle(GetCategoryDisplayName(result.Category));
            row.SetRequiredText(result.Required);
            row.SetActualText(formattedActualText);
        }

        private void ClearSpawnedRows()
        {
            foreach (MinimumSpecsRowView row in spawnedRows)
            {
                if (row != null)
                    Object.Destroy(row.gameObject);
            }

            spawnedRows.Clear();
        }

        private string GetCategoryDisplayName(SpecCategory category)
        {
            return category switch
                   {
                       SpecCategory.OS => "Operating System",
                       SpecCategory.CPU => "Processor",
                       SpecCategory.GPU => "Graphics Card",
                       SpecCategory.VRAM => "Graphics Memory",
                       SpecCategory.RAM => "System Memory",
                       SpecCategory.Storage => "Storage Space",
                       SpecCategory.ComputeShaders => "Compute Shaders",
                       _ => category.ToString(),
                   };
        }
    }
}
