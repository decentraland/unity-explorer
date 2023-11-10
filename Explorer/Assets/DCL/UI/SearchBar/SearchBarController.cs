using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchBarController : IDisposable
{
    private SearchBarView view;

    public SearchBarController(SearchBarView view)
    {
        this.view = view;

        view.inputField.onValueChanged.AddListener(OnValueChanged);
        view.inputField.onSubmit.AddListener(s => SubmitSearch(s));
        view.clearSearchButton.onClick.AddListener(() => ClearSearch());
        //view.inputField.onSelect.AddListener((text)=>OnSelected?.Invoke(true));
    }

    private void OnValueChanged(string searchText)
    {
    }

    private void ClearSearch()
    {
    }

    private void SubmitSearch(string searchString)
    {
    }

    public void Dispose()
    {
        view.inputField.onSelect.RemoveAllListeners();
        view.inputField.onValueChanged.RemoveAllListeners();
        view.inputField.onSubmit.RemoveAllListeners();
        view.clearSearchButton.onClick.RemoveAllListeners();
    }
}
