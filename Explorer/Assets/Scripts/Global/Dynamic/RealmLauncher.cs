using System;
using TMPro;
using UnityEngine;

namespace Global.Dynamic
{
    public class RealmLauncher : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown realmPicker;

        public Action<string> OnRealmSelected;

        public void Initialize(string[] realms)
        {
            foreach (string realm in realms) { realmPicker.options.Add(new TMP_Dropdown.OptionData(realm)); }

            realmPicker.onValueChanged.AddListener(selectedValue =>
                OnRealmSelected(realmPicker.options[selectedValue].text));
        }
    }
}
