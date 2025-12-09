namespace DCL.AvatarRendering.Export
{
    /// <summary>
    /// Add this component to an entity to trigger avatar export debugging/export
    /// </summary>
    public struct ExportAvatarIntention
    {
        public string OutputPath; // Optional: where to save the export
        
        public ExportAvatarIntention(string outputPath = "")
        {
            OutputPath = outputPath;
        }
    }
}
