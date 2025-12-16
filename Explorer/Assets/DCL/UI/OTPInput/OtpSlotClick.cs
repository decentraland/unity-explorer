using UnityEngine;
using UnityEngine.EventSystems;

public sealed class OtpSlotClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private OtpCodeInput otp;
    [SerializeField] private OtpCodeInput2 otp2;
    [SerializeField] private int index;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (otp) otp.FocusSlot(index);
        if (otp2) otp2.FocusSlot(index);
    }
}
