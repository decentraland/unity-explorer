using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class DCLInputText
    {
        public readonly TextField TextField = new ();
        public readonly TextFieldPlaceholder Placeholder = new ();

        public void Initialize(string textFieldName, string styleClass)
        {
            TextField.name = textFieldName;
            TextField.AddToClassList(styleClass);
            TextField.pickingMode = PickingMode.Position;
            Placeholder.SetupTextField(TextField);
        }

        public void Dispose()
        {
            Placeholder.Dispose();
        }
    }
}
