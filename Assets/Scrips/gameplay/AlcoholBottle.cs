using UnityEngine;

public class AlcoholBottle : MonoBehaviour
{
    [Header("Drunk")]
    [SerializeField] private float customDrunkAmount = 0f;

    [Header("Floating Animation")]
    [SerializeField] private float rotationSpeed = 80f;

    [SerializeField] private float floatHeight = 0.25f;

    [SerializeField] private float floatSpeed = 2f;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        RotateBottle();

        FloatBottle();
    }

    private void RotateBottle()
    {
        transform.Rotate(
            Vector3.up *
            rotationSpeed *
            Time.deltaTime,
            Space.World
        );
    }

    private void FloatBottle()
    {
        Vector3 pos = _startPosition;

        pos.y +=
            Mathf.Sin(
                Time.time * floatSpeed
            ) * floatHeight;

        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        DrunkSystem drunk =
            other.GetComponentInParent<DrunkSystem>();

        if (drunk == null)
            return;

        if (customDrunkAmount > 0f)
        {
            drunk.AddDrunkLevel(customDrunkAmount);
        }
        else
        {
            drunk.CollectBottle();
        }

        Destroy(gameObject);
    }
}