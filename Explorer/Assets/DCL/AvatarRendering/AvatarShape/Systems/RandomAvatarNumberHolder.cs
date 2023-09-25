using UnityEngine;

public class RandomAvatarNumberHolder : MonoBehaviour
{
    [SerializeField]
    public int amoutOfRandomAvatars = 1;

    public static RandomAvatarNumberHolder instance;

    private void Awake()
    {
        instance = this;
    }
}
