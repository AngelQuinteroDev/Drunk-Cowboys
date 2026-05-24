using UnityEngine;

/// <summary>
/// Mueve el objeto hacia adelante, atrás y a los lados
/// usando las teclas WASD o las flechas del teclado.
/// </summary>
public class PruebaPersonaje : MonoBehaviour
{
    [Header("Configuración de movimiento")]
    [SerializeField] private float velocidad = 5f;
    [SerializeField] private bool usarFisicas = false; // true = Rigidbody, false = Transform

    private Rigidbody rb;

    void Start()
    {
        if (usarFisicas)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                Debug.LogWarning("PruebaPersonaje: 'Usar Fisicas' está activado pero no hay Rigidbody en el objeto.");
        }
    }

    void Update()
    {
        if (!usarFisicas)
            MoverConTransform();
    }

    void FixedUpdate()
    {
        if (usarFisicas)
            MoverConRigidbody();
    }

    /// <summary>
    /// Movimiento directo sobre el Transform (sin físicas).
    /// Ideal para objetos simples, cámaras o prototipos.
    /// </summary>
    private void MoverConTransform()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A/D o ←/→
        float vertical   = Input.GetAxis("Vertical");   // W/S o ↑/↓

        Vector3 direccion = new Vector3(horizontal, 0f, vertical).normalized;
        transform.Translate(direccion * velocidad * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// Movimiento mediante Rigidbody (con físicas).
    /// Ideal cuando el objeto necesita colisiones realistas.
    /// </summary>
    private void MoverConRigidbody()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        Vector3 direccion = new Vector3(horizontal, 0f, vertical).normalized;
        rb.MovePosition(rb.position + direccion * velocidad * Time.fixedDeltaTime);
    }
}
