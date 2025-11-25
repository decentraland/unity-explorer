namespace DCL.AvatarRendering.Export
{
    /// <summary>
    /// Add this component to an entity to trigger avatar export debugging/export
    /// </summary>
    public struct ExportAvatarIntention
    {
        public ExportMode Mode;
        public string OutputPath; // Optional: where to save the export
        public bool RemoveAfterExport; // Whether to remove this component after processing
        
        public ExportAvatarIntention(ExportMode mode, string outputPath = "", bool removeAfterExport = true)
        {
            Mode = mode;
            OutputPath = outputPath;
            RemoveAfterExport = removeAfterExport;
        }
    }
    
    public enum ExportMode
    {
        DebugLog,           // Just log debug info
        ExportVRM,          // Export to VRM format
        ExportGLB,          // Export to GLB format
        DebugAndExport      // Debug log + export
    }
}
