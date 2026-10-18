using UnityEngine;

/// <summary>
/// 횃불, 빛나는 성소 등 자체적으로 주변 안개를 걷어내는 광원 오브젝트에 부착하는 컴포넌트입니다.
/// </summary>

namespace BagBack100.Dungeon
{
    public class FogVisionSource : MonoBehaviour
    {
        [Header("Vision Settings")]
        [Tooltip("이 광원이 밝히는 시야 반경 (타일 수)")]
        [SerializeField] private int _visionRadius = 3;
        [Tooltip("광원 시야 가장자리의 부드러운 감쇠 폭 (타일 단위)")]
        [SerializeField] private float _softEdgeWidth = 1.2f;
        [Tooltip("벽 너머 시야 차단 적용 여부")]
        [SerializeField] private bool _useLineOfSight = true;
        [Tooltip("체크하면 플레이어가 이 광원 위치를 한 번이라도 발견한 뒤부터 주변을 밝힘")]
        [SerializeField] private bool _requireDiscoveredToEmit = false;

        public int VisionRadius => _visionRadius;
        public float SoftEdgeWidth => _softEdgeWidth;
        public bool UseLineOfSight => _useLineOfSight;
        public bool RequireDiscoveredToEmit => _requireDiscoveredToEmit;

        private void Start()
        {
            Register();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Register()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RegisterVisionSource(this);
            }
        }

        private void Unregister()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UnregisterVisionSource(this);
            }
        }

        public void NotifyVisionChanged()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RequestVisionUpdate();
            }
        }
    }
}