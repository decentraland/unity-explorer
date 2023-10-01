using UnityEngine;

//DELETE CLASS, TEMPORARY HELPER TO DEMO LOCOMOTION
public class PlayerAnimatorController : MonoBehaviour
{
    private Animator playerAnimator;
    private CharacterController characterController;

    private bool isJumping;

    private void Start()
    {
        playerAnimator = GetComponent<Animator>();
        characterController = GetComponentInParent<CharacterController>();
    }

    private void Update()
    {
        if (isJumping && !characterController.isGrounded)
            return;

        if (!characterController.isGrounded && !isJumping)
        {
            playerAnimator.SetBool("isJumping", true);
            isJumping = true;
            return;
        }

        if (characterController.isGrounded && isJumping)
        {
            playerAnimator.SetBool("isJumping", false);
            isJumping = false;
            return;
        }

        //Max speed is 48
        playerAnimator.SetFloat("Speed", characterController.velocity.sqrMagnitude / 48);
    }
}
