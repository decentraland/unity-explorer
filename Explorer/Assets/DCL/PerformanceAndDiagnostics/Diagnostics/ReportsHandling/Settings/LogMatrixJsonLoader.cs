using System;
using System.IO;
using UnityEngine;

namespace DCL.Diagnostics
{
    public static class LogMatrixJsonLoader
    {
        public static CategorySeverityMatrixDto? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, LogMatrixConstants.LOG_MATRIX_FILE_NOT_FOUND, filePath);
                    return null;
                }

                var jsonContent = File.ReadAllText(filePath);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, LogMatrixConstants.LOG_MATRIX_FILE_EMPTY, filePath);
                    return null;
                }

                var dto = JsonUtility.FromJson<CategorySeverityMatrixDto>(jsonContent);
                if (dto == null)
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, LogMatrixConstants.LOG_MATRIX_DESERIALIZE_FAILED, filePath);
                    return null;
                }

                ReportHub.LogProductionInfo(LogMatrixConstants.LOG_MATRIX_LOAD_SUCCESS, filePath);
                return dto;
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.ENGINE);
                ReportHub.LogWarning(ReportCategory.ENGINE, LogMatrixConstants.LOG_MATRIX_LOAD_FAILED, filePath);
                return null;
            }
        }

        public static string ResolveFilePath(string filePath)
        {
            // If it's already an absolute path, return as-is
            if (Path.IsPathRooted(filePath))
                return filePath;

            // Pre-allocate paths to avoid multiple string concatenations
            string[] candidatePaths = GetCandidatePaths(filePath);
            
            // Check each candidate path in order of preference
            foreach (string candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                    return candidatePath;
            }

            // Return original path if none found (will be handled by File.Exists check)
            return filePath;
        }

        private static string[] GetCandidatePaths(string filePath)
        {
            var dataDir = Path.GetDirectoryName(Application.dataPath);
            var currentDir = Directory.GetCurrentDirectory();
            var persistentDir = Application.persistentDataPath;

            #if UNITY_EDITOR
            return new[]
            {
                Path.Combine(dataDir ?? "", filePath),           // Application directory
                Path.Combine(currentDir, filePath),              // Current working directory
                Path.Combine(Application.dataPath, "..", filePath), // Project root
                Path.Combine(persistentDir, filePath)            // Persistent data path
            };
            #else
            return new[]
            {
                Path.Combine(dataDir ?? "", filePath),           // Application directory
                Path.Combine(currentDir, filePath),              // Current working directory
                Path.Combine(persistentDir, filePath)            // Persistent data path
            };
            #endif
        }
    }
}
