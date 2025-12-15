namespace DCL.AvatarRendering.Export
{
	public struct AvatarExportEvents
	{
		public bool Succeeded;
		
		public AvatarExportEvents(bool succeeded) => Succeeded = succeeded;
	}
}