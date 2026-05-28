using UnityEngine;

namespace FPSMultiplayer.Infrastructure
{
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