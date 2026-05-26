using UnityEngine;

public class PruebaPersonaje : MonoBehaviour
{
    [Header("Configuración de movimiento")]
    [SerializeField] private float velocidad = 5f;
    [SerializeField] private bool usarFisicas = false; 

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

    private void MoverConTransform()
    {
        float horizontal = Input.GetAxis("Horizontal"); 
        float vertical   = Input.GetAxis("Vertical");  

        Vector3 direccion = new Vector3(horizontal, 0f, vertical).normalized;
        transform.Translate(direccion * velocidad * Time.deltaTime, Space.World);
    }

    private void MoverConRigidbody()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        Vector3 direccion = new Vector3(horizontal, 0f, vertical).normalized;
        rb.MovePosition(rb.position + direccion * velocidad * Time.fixedDeltaTime);
    }
}
