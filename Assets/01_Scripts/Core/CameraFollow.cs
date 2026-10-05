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

        public void SetTarget(Transform target)
        {
            _target = target;
            if (_target != null)
            {
                // 타겟 등록 즉시 해당 위치로 스냅 (최초 진입 시 화면 이동 딜레이 방지)
                transform.position = _target.position + _offset;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPosition = _target.position + _offset;
            
            // Update/LateUpdate 내 Zero GC 원칙 준수 (Vector3.SmoothDamp 사용)
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime);
        }
    }
}
