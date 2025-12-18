namespace DCL.AvatarRendering.Export
{
    public struct AvatarExportEvents
    {
        public readonly bool Succeeded;

        public AvatarExportEvents(bool succeeded)
        {
            Succeeded = succeeded;
        }
    }
}
