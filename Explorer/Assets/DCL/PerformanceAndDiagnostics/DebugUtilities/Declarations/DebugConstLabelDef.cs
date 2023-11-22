namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition of the constant label
    /// </summary>
    public class DebugConstLabelDef : IDebugElementDef
    {
        public readonly string Text;

        public DebugConstLabelDef(string s)
        {
            Text = s;
        }
    }
}
