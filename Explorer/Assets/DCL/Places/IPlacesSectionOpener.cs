namespace DCL.Places
{
    /// <summary>
    ///     Exists only so the section modal can be tested without a <see cref="PlacesController" />.
    /// </summary>
    public interface IPlacesSectionOpener
    {
        void OpenSection(PlacesSection section);
    }
}
