using UnityEngine;

namespace DCL.Diagnostics
{
    public class CategoryExclusionMatrix : ICategorySeverityMatrix
    {
        private readonly ICategorySeverityMatrix baseMatrix;
        private readonly string excludedCategory;

        public CategoryExclusionMatrix(ICategorySeverityMatrix baseMatrix, string excludedCategory)
        {
            this.baseMatrix = baseMatrix;
            this.excludedCategory = excludedCategory;
        }

        public bool IsEnabled(string category, LogType severity) =>
            category != excludedCategory && baseMatrix.IsEnabled(category, severity);
    }
}
