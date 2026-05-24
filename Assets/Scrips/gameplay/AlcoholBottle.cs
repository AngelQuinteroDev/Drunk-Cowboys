using UnityEngine;

public class AlcoholBottle : MonoBehaviour
{
    [SerializeField] private float customDrunkAmount = 0f;

    private void OnTriggerEnter(Collider other)
    {
        DrunkSystem drunk = other.GetComponentInParent<DrunkSystem>();
        if (drunk == null) return;

        if (customDrunkAmount > 0f)
            drunk.AddDrunkLevel(customDrunkAmount);
        else
            drunk.CollectBottle();

        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
            pc.TriggerDrinking();

        Destroy(gameObject);
    }
}