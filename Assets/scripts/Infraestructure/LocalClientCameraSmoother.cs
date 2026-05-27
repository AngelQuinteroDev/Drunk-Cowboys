using UnityEngine;

namespace FPSMultiplayer.Infrastructure
{
    /// <summary>
    /// Smooths local camera position after it gets parented to the local player head pivot.
    /// Keeps rotation immediate to avoid aim/input mismatch.
    /// </summary>
    public class LocalClientCameraSmoother : MonoBehaviour
    {
        [SerializeField] private bool enableSmoothing = true;
        [SerializeField, Min(0.001f)] private float positionSmoothTime = 0.035f;
        [SerializeField, Min(1f)] private float maxFollowSpeed = 80f;

        private Camera _mainCamera;
        private Transform _followTarget;
        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (!enableSmoothing)
                return;

            if (!TryResolveMainCamera())
                return;

            // PlayerController parents Camera.main to headPivot on local spawn.
            // We detach once and then smooth-follow that pivot in world space.
            if (_mainCamera.transform.parent != null)
            {
                _followTarget = _mainCamera.transform.parent;
                _mainCamera.transform.SetParent(null, true);
                _velocity = Vector3.zero;
            }

            if (_followTarget == null)
                return;

            _mainCamera.transform.position = Vector3.SmoothDamp(
                _mainCamera.transform.position,
                _followTarget.position,
                ref _velocity,
                positionSmoothTime,
                maxFollowSpeed,
                Time.unscaledDeltaTime
            );

            // Keep camera rotation exact to preserve shooting direction responsiveness.
            _mainCamera.transform.rotation = _followTarget.rotation;
        }

        private bool TryResolveMainCamera()
        {
            if (_mainCamera != null)
                return true;

            _mainCamera = Camera.main;
            return _mainCamera != null;
        }
    }
}