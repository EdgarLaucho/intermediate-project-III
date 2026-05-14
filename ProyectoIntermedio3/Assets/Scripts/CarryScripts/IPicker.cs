using UnityEngine;

public class IPicker : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        IPickable item = other.GetComponent<IPickable>();

        if (item != null)
        {
            item.PickUp();
        }
    }
}