namespace DCL.AvatarRendering.Export
{
    public readonly struct AvatarExportEvents
    {
        public readonly bool Succeeded;

        public AvatarExportEvents(bool succeeded)
        {
            Succeeded = succeeded;
        }
    }
}
