using Fusion;
using UnityEngine;
using FPSMultiplayer.Gameplay;

/// <summary>
/// Debugger temporal — clic izquierdo para diagnosticar daño.
/// Quitar antes de build final.
/// </summary>
public class DamageDebugger : NetworkBehaviour
{
    private HealthSystem _health;

    public override void Spawned()
    {
        _health = GetComponent<HealthSystem>();

        Debug.Log($"[DamageDebugger] Player spawned." +
                  $"\n  HasStateAuthority={HasStateAuthority}" +
                  $"\n  HasInputAuthority={HasInputAuthority}" +
                  $"\n  Layer={gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})" +
                  $"\n  HealthSystem found={_health != null}");

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            Debug.Log($"[DamageDebugger]   Collider: {col.gameObject.name}" +
                      $" | isTrigger={col.isTrigger}" +
                      $" | enabled={col.enabled}" +
                      $" | layer={LayerMask.LayerToName(col.gameObject.layer)}");
        }
    }

    private void Update()
    {
        if (!HasInputAuthority) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[DamageDebugger] No main camera."); return; }

        Vector3 origin = cam.transform.position;
        Vector3 dir    = cam.transform.forward;

        Debug.Log($"[DamageDebugger] === CLICK RAYCAST === origin={origin} dir={dir}");

        // 1) Sin LayerMask ni filtro de triggers — ve absolutamente TODO
        RaycastHit[] allHits = Physics.RaycastAll(origin, dir, 200f, ~0, QueryTriggerInteraction.Collide);
        if (allHits.Length == 0)
        {
            Debug.LogWarning("[DamageDebugger] Sin hits. Posibles causas: Physics Matrix bloquea el layer, camara fuera de escena, o no hay colliders en esa direccion.");
        }
        else
        {
            Debug.Log($"[DamageDebugger] {allHits.Length} hit(s) con QueryTriggerInteraction.Collide:");
            foreach (var hit in allHits)
            {
                var hs = hit.collider.GetComponentInParent<HealthSystem>();
                Debug.Log($"  → {hit.collider.gameObject.name}" +
                          $" | layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}" +
                          $" | isTrigger={hit.collider.isTrigger}" +
                          $" | dist={hit.distance:F2}" +
                          $" | HealthSystem={hs != null}");
            }
        }

        // 2) HealthSystems en escena y prueba de daño por proximidad
        var nearbyHealth = FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
        Debug.Log($"[DamageDebugger] HealthSystems en escena: {nearbyHealth.Length}");
        foreach (var hs in nearbyHealth)
        {
            if (hs == _health) continue;
            float dist = Vector3.Distance(transform.position, hs.transform.position);
            Debug.Log($"  HealthSystem: {hs.gameObject.name}" +
                      $" | dist={dist:F1}" +
                      $" | HasStateAuthority={hs.HasStateAuthority}" +
                      $" | IsAlive={hs.IsAlive}" +
                      $" | HP={hs.CurrentHealth}");

            if (dist < 30f)
            {
                Debug.LogWarning($"[DamageDebugger] TEST proximidad: intentando 10 dmg a {hs.gameObject.name} (HasStateAuthority={hs.HasStateAuthority})");
                if (hs.HasStateAuthority)
                    hs.TakeDamage(10f);
                else
                    hs.RPC_ApplyDamage(10f, PlayerRef.None);
            }
        }
    }
}