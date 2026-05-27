using UnityEngine;
using Fusion;

namespace FPSMultiplayer.Gameplay
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        private Animator     _animator;
        private HealthSystem _health;

        private static readonly int _speedHash      = Animator.StringToHash("Speed");
        private static readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int _isRunningHash  = Animator.StringToHash("IsRunning");
        private static readonly int _isCrouchHash   = Animator.StringToHash("IsCrouching");
        private static readonly int _velocityXHash  = Animator.StringToHash("VelocityX");
        private static readonly int _velocityZHash  = Animator.StringToHash("VelocityZ");
        private static readonly int _isAliveHash    = Animator.StringToHash("IsAlive");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _health   = GetComponentInParent<HealthSystem>();
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.OnDeath.AddListener(OnDeath);
            _health.OnRespawn.AddListener(OnRespawn);
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.OnDeath.RemoveListener(OnDeath);
            _health.OnRespawn.RemoveListener(OnRespawn);
        }

        private void OnDeath()
        {
            _animator.SetBool(_isAliveHash, false);
            _animator.SetTrigger("Death");
        }

        private void OnRespawn()
        {

            _animator.Rebind();
            _animator.Update(0f);

            _animator.ResetTrigger("Death");
            _animator.ResetTrigger("Respawn");

            _animator.SetBool(_isAliveHash, true);
            _animator.SetTrigger("Respawn");

            _animator.SetFloat(_speedHash,     0f);
            _animator.SetBool(_isGroundedHash, true);
            _animator.SetBool(_isRunningHash,  false);
            _animator.SetBool(_isCrouchHash,   false);
            _animator.SetFloat(_velocityXHash, 0f);
            _animator.SetFloat(_velocityZHash, 0f);
        }

        public void UpdateAnimatorState(Vector3 velocity, bool isGrounded, bool isRunning, bool isCrouching)
        {
            if (_health != null && !_health.IsAlive) return;

            float speed = new Vector2(velocity.x, velocity.z).magnitude;

            _animator.SetFloat(_speedHash,      speed,       0.1f, Time.deltaTime);
            _animator.SetBool (_isGroundedHash, isGrounded);
            _animator.SetBool (_isRunningHash,  isRunning);
            _animator.SetBool (_isCrouchHash,   isCrouching);

            var localVel = transform.InverseTransformDirection(velocity);
            _animator.SetFloat(_velocityXHash, localVel.x, 0.1f, Time.deltaTime);
            _animator.SetFloat(_velocityZHash, localVel.z, 0.1f, Time.deltaTime);
        }

        public void TriggerDeath()   => OnDeath();
        public void TriggerRespawn() => OnRespawn();
        public void TriggerFire()    => _animator.SetTrigger("Fire");
        public void TriggerReload()  => _animator.SetTrigger("Reload");
    }
}