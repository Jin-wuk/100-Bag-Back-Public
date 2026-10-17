using System.Collections;
using UnityEngine;

namespace BagBack100.Core
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [Tooltip("추적할 대상 (런타임에 동적으로 할당될 수 있음)")]
        [SerializeField] private Transform _target;
        
        [Tooltip("카메라 추적 부드러움 정도 (값이 작을수록 부드러움)")]
        [SerializeField] private float _smoothTime = 0.3f;
        
        [Tooltip("카메라 위치 오프셋")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        private Vector3 _velocity = Vector3.zero;
        private Vector3 _basePosition;
        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeIntensity;
        private Coroutine _hitStopCoroutine;

        private void OnEnable()
        {
            GlobalEvents.OnCombatImpact += HandleCombatImpact;
        }

        private void OnDisable()
        {
            GlobalEvents.OnCombatImpact -= HandleCombatImpact;
            if (Time.timeScale < 1f)
            {
                Time.timeScale = 1f;
            }
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target != null)
            {
                // 타겟 등록 즉시 해당 위치로 스냅 (최초 진입 시 화면 이동 딜레이 방지)
                _basePosition = _target.position + _offset;
                transform.position = _basePosition;
            }
        }

        private void HandleCombatImpact(float shakeIntensity, float shakeDuration, float hitStopDuration)
        {
            if (shakeDuration > 0f && shakeIntensity >= _shakeIntensity)
            {
                _shakeIntensity = shakeIntensity;
                _shakeDuration = shakeDuration;
                _shakeTimer = shakeDuration;
            }

            if (hitStopDuration > 0f && gameObject.activeInHierarchy)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine(hitStopDuration));
            }
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Time.timeScale = 1f;
            _hitStopCoroutine = null;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPosition = _target.position + _offset;
            
            // Update/LateUpdate 내 Zero GC 원칙 준수 (Vector3.SmoothDamp 사용)
            _basePosition = Vector3.SmoothDamp(_basePosition, targetPosition, ref _velocity, _smoothTime);

            Vector3 finalPos = _basePosition;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.001f, _shakeDuration));
                float currentMag = _shakeIntensity * progress;
                Vector2 randOffset = Random.insideUnitCircle * currentMag;
                finalPos.x += randOffset.x;
                finalPos.y += randOffset.y;

                if (_shakeTimer <= 0f)
                {
                    _shakeIntensity = 0f;
                }
            }

            transform.position = finalPos;
        }
    }
}
