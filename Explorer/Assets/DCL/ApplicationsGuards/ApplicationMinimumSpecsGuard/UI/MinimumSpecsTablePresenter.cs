using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsTablePresenter
    {
        private readonly MinimumSpecsTableView tableView;
        private readonly List<MinimumSpecsRowView> spawnedRows = new ();

        public MinimumSpecsTablePresenter(MinimumSpecsTableView view)
        {
            tableView = view;
        }

        public void Populate(IEnumerable<SpecResult> results)
        {
            ClearSpawnedRows();

            var unmetResults = results
                              .Where(r => !r.IsMet)
                              .ToList();

            PopulateRow(tableView.LastRow, unmetResults[0]);

            for (var i = 1; i < unmetResults.Count; i++)
            {
                MinimumSpecsRowView newRow = Object.Instantiate(tableView.RowTemplate, tableView.RowTemplate.transform.parent);
                PopulateRow(newRow, unmetResults[i]);
                newRow.gameObject.SetActive(true);
                spawnedRows.Add(newRow);
            }
        }

        private static void PopulateRow(MinimumSpecsRowView row, SpecResult result)
        {
            row.SetTitle(GetCategoryDisplayName(result.Category));
            row.SetRequiredText(result.Required);
            row.SetActualText(result.Actual);
        }

        private void ClearSpawnedRows()
        {
            foreach (MinimumSpecsRowView row in spawnedRows)
                Object.Destroy(row.gameObject);

            spawnedRows.Clear();
        }

        private static string GetCategoryDisplayName(SpecCategory category) =>
            category.ToString();
    }
}
