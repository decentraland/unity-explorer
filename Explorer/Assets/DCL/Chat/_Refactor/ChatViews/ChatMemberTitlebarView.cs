using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMemberTitlebarView : MonoBehaviour
{
    public event Action OnCloseRequested;
    public event Action OnBackRequested;
    public Button ButtonClose => closeButton;
    public Button ButtonBack => backButton;
    
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text membersCountText;

    private void Awake()
    {
        closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
        backButton.onClick.AddListener(() => OnBackRequested?.Invoke());
    }
    
    public void SetMemberCount(string count) => membersCountText.text = count;

    public void Activate(bool activate) => gameObject.SetActive(activate);
}
