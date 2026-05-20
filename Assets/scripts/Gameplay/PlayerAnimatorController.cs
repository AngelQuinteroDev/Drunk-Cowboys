// Maneja el Animator local. NUNCA sincroniza parámetros por red directamente.
// Lee el estado Networked del PlayerNetworkController y lo aplica localmente.
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        private Animator _animator;

        // Hashes para performance — evita string lookups en cada frame
        private static readonly int _speedHash      = Animator.StringToHash("Speed");
        private static readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int _isRunningHash  = Animator.StringToHash("IsRunning");
        private static readonly int _isCrouchHash   = Animator.StringToHash("IsCrouching");
        private static readonly int _velocityXHash  = Animator.StringToHash("VelocityX");
        private static readonly int _velocityZHash  = Animator.StringToHash("VelocityZ");

        private void Awake() => _animator = GetComponent<Animator>();

        // Llamado desde PlayerNetworkController.Render()
        public void UpdateAnimatorState(Vector3 velocity, bool isGrounded, bool isRunning, bool isCrouching)
        {
            float speed = new Vector2(velocity.x, velocity.z).magnitude;

            _animator.SetFloat(_speedHash,      speed,       0.1f, Time.deltaTime);
            _animator.SetBool (_isGroundedHash, isGrounded);
            _animator.SetBool (_isRunningHash,  isRunning);
            _animator.SetBool (_isCrouchHash,   isCrouching);

            // Dirección relativa para blending trees 2D
            var localVel = transform.InverseTransformDirection(velocity);
            _animator.SetFloat(_velocityXHash, localVel.x, 0.1f, Time.deltaTime);
            _animator.SetFloat(_velocityZHash, localVel.z, 0.1f, Time.deltaTime);
        }

        public void TriggerDeath()     => _animator.SetTrigger("Death");
        public void TriggerRespawn()   => _animator.SetTrigger("Respawn");
        public void TriggerFire()      => _animator.SetTrigger("Fire");
        public void TriggerReload()    => _animator.SetTrigger("Reload");
    }
}