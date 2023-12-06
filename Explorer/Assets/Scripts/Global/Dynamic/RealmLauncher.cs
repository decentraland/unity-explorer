using System;
using TMPro;
using UnityEngine;

namespace Global.Dynamic
{
    public class RealmLauncher : MonoBehaviour
    {
        public Action<string> OnRealmSelected;
        [SerializeField] private TMP_Dropdown realmPicker;

        public void Initialize(string[] realms)
        {
            foreach (string realm in realms) { realmPicker.options.Add(new TMP_Dropdown.OptionData(realm)); }

            realmPicker.onValueChanged.AddListener(selectedValue =>
                OnRealmSelected(realmPicker.options[selectedValue].text));
        }
    }
}
