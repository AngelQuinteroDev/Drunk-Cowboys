using Fusion;
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    public class AlcoholBottle : NetworkBehaviour
    {
        [Header("Drunk")]
        [SerializeField] private float customDrunkAmount = 0f;

        [Header("Floating Animation")]
        [SerializeField] private float rotationSpeed = 80f;
        [SerializeField] private float floatHeight = 0.25f;
        [SerializeField] private float floatSpeed = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip drinkSound;

        private Vector3 _startPosition;

        public override void Spawned()
        {
            _startPosition = transform.position;
        }

        public override void Render()
        {
            RotateBottle();
            FloatBottle();
        }

        private void RotateBottle()
        {
            transform.Rotate(
                Vector3.up * rotationSpeed * Time.deltaTime,
                Space.World
            );
        }

        private void FloatBottle()
        {
            Vector3 pos = _startPosition;

            pos.y +=
                Mathf.Sin(Time.time * floatSpeed) * floatHeight;

            transform.position = pos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!HasStateAuthority) return;

            var drunk =
                other.GetComponentInParent<DrunkSystem>();

            if (drunk == null) return;

            if (audioSource != null && drinkSound != null)
                audioSource.PlayOneShot(drinkSound);

            if (customDrunkAmount > 0f)
                drunk.AddDrunkLevel(customDrunkAmount);
            else
                drunk.CollectBottle();

            Runner.Despawn(Object);
        }
    }
}