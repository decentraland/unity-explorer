namespace DCL.Diagnostics
{
    public static class LogMatrixConstants
    {
        public const string LOG_MATRIX_FILE_NOT_FOUND = "Log matrix override file not found: {0}";
        public const string LOG_MATRIX_FILE_EMPTY = "Log matrix override file is empty: {0}";
        public const string LOG_MATRIX_DESERIALIZE_FAILED = "Failed to deserialize log matrix override file: {0}";
        public const string LOG_MATRIX_LOAD_SUCCESS = "Successfully loaded log matrix override from: {0}";
        public const string LOG_MATRIX_LOAD_FAILED = "Failed to load log matrix override file: {0}";
        public const string LOG_MATRIX_INVALID_SEVERITY = "Invalid severity '{0}' for category '{1}' in log matrix override";
        
        public const string LOG_MATRIX_ENABLED = "Enabled {0}.{1} logging";
        public const string LOG_MATRIX_DISABLED = "Disabled {0}.{1} logging";
        public const string LOG_MATRIX_INVALID_ACTION = "🔴 Invalid action. Use: enable or disable";
        public const string LOG_MATRIX_INVALID_SEVERITY_CMD = "🔴 Invalid severity: {0}. Use: Log, Warning, Error, Exception, Assert";
        public const string LOG_MATRIX_ENABLED_CMD = "🟢 Enabled {0}.{1} logging";
        public const string LOG_MATRIX_DISABLED_CMD = "🔴 Disabled {0}.{1} logging";
    }
}
