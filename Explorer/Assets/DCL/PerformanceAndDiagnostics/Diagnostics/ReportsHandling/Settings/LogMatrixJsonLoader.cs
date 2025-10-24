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
                    ReportHub.LogWarning(ReportCategory.ENGINE, $"Log matrix override file not found: {filePath}");
                    return null;
                }

                var jsonContent = File.ReadAllText(filePath);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, $"Log matrix override file is empty: {filePath}");
                    return null;
                }

                var dto = JsonUtility.FromJson<CategorySeverityMatrixDto>(jsonContent);
                if (dto == null)
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, $"Failed to deserialize log matrix override file: {filePath}");
                    return null;
                }

                ReportHub.LogProductionInfo($"Successfully loaded log matrix override from: {filePath}");
                return dto;
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.ENGINE);
                ReportHub.LogWarning(ReportCategory.ENGINE, $"Failed to load log matrix override file: {filePath}");
                return null;
            }
        }

        public static string ResolveFilePath(string filePath)
        {
            // If it's already an absolute path, return as-is
            if (Path.IsPathRooted(filePath))
                return filePath;

            // For built applications, try relative to the application's data directory
            // This is where the executable is located in Unity builds
            var dataDir = Path.GetDirectoryName(Application.dataPath);
            if (!string.IsNullOrEmpty(dataDir))
            {
                var dataPath = Path.Combine(dataDir, filePath);
                if (File.Exists(dataPath))
                    return dataPath;
            }

            // Try relative to current working directory
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            if (File.Exists(currentDirPath))
                return currentDirPath;

            // In editor, try relative to project root (Assets/..)
            #if UNITY_EDITOR
            var projectRootPath = Path.Combine(Application.dataPath, "..", filePath);
            if (File.Exists(projectRootPath))
                return projectRootPath;
            #endif

            // Try relative to persistent data path as fallback
            var persistentPath = Path.Combine(Application.persistentDataPath, filePath);
            if (File.Exists(persistentPath))
                return persistentPath;

            // Return original path if none found (will be handled by File.Exists check)
            return filePath;
        }
    }
}
