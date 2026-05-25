using UnityEngine;

public class WeaponAttacher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private Transform rightHandBone;

    [Header("Offset")]
    [Tooltip("Si esta activo respeta la posicion y rotacion que ya tiene el arma. Si esta desactivado aplica el offset manual de abajo.")]
    [SerializeField] private bool preserveCurrentOffset = true;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private void Start()
    {
        if (weaponRoot == null || rightHandBone == null)
        {
            Debug.LogWarning("[WeaponAttacher] WeaponRoot o RightHandBone no asignados.");
            return;
        }

        if (preserveCurrentOffset)
        {
            Vector3 savedPos = weaponRoot.localPosition;
            Quaternion savedRot = weaponRoot.localRotation;

            weaponRoot.SetParent(rightHandBone);

            weaponRoot.localPosition = savedPos;
            weaponRoot.localRotation = savedRot;
        }
        else
        {
            weaponRoot.SetParent(rightHandBone);
            weaponRoot.localPosition = positionOffset;
            weaponRoot.localRotation = Quaternion.Euler(rotationOffset);
        }
    }

    public void DetachWeapon()
    {
        if (weaponRoot != null)
            weaponRoot.SetParent(transform);
    }
}