using System;
using System.IO;
using UnityEngine;

namespace DCL.Diagnostics
{
    public static class LogMatrixJsonLoader
    {
        public static CategorySeverityMatrixDto? LoadFromApplicationRoot(string fileName)
        {
            try
            {
                string filePath = GetApplicationRootPath(fileName);

                if (!File.Exists(filePath))
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, string.Format(LogMatrixConstants.LOG_MATRIX_FILE_NOT_FOUND, filePath));
                    return null;
                }

                var jsonContent = File.ReadAllText(filePath);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, string.Format(LogMatrixConstants.LOG_MATRIX_FILE_EMPTY, filePath));
                    return null;
                }

                var dto = JsonUtility.FromJson<CategorySeverityMatrixDto>(jsonContent);
                if (dto == null)
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, string.Format(LogMatrixConstants.LOG_MATRIX_DESERIALIZE_FAILED, filePath));
                    return null;
                }

                ReportHub.LogProductionInfo(string.Format(LogMatrixConstants.LOG_MATRIX_LOAD_SUCCESS, filePath));
                return dto;
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.ENGINE);
                return null;
            }
        }

        private static string GetApplicationRootPath(string fileName)
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "..", fileName);
#else
            string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string executableDirectory = Path.GetDirectoryName(executablePath) ?? "";
            return Path.Combine(executableDirectory, fileName);
#endif
        }
    }
}
