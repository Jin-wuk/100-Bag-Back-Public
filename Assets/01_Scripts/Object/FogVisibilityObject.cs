using UnityEngine;

namespace BagBack100.Dungeon
{
    public enum FogVisibilityMode
    {
        [Tooltip("현재 시야(Visible) 안에 있을 때만 보임 (드랍 아이템, 은신 함정 등)")]
        VisibleOnly,
        [Tooltip("한 번 발견하면 시야 밖(Explored)으로 나가도 안개 아래에 계속 보임 (보물상자, 계단, 성소 등)")]
        RememberWhenExplored
    }

    public class FogVisibilityObject : MonoBehaviour
    {
        [Header("Visibility Rules")]
        [SerializeField] private FogVisibilityMode _visibilityMode = FogVisibilityMode.RememberWhenExplored;

        [Header("Target Renderers (비워두면 하위에서 자동 탐색)")]
        [SerializeField] private SpriteRenderer[] _renderers;

        [Header("Visible-Only Extras (선택 사항)")]
        [Tooltip("현재 시야(Visible) 안에 있을 때만 켤 오브젝트 (반짝이는 이펙트, 상호작용 UI 등)")]
        [SerializeField] private GameObject[] _activeOnlyWhenVisible;

        private void Awake()
        {
            RefreshRendererCache();
        }

        private void Start()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RegisterObject(this);
            }
        }

        private void OnEnable()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RegisterObject(this);
            }
        }

        private void OnDisable()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UnregisterObject(this);
            }
        }

        /// <summary>
        /// 프리팹 없이 런타임(코드)에서 AddComponent로 부착할 때 호출하여 모드 설정 및 즉시 동기화
        /// </summary>
        public void InitializeRuntime(FogVisibilityMode mode)
        {
            _visibilityMode = mode;
            RefreshRendererCache();
            RequestVisibilityUpdate();
        }

        public void RefreshRendererCache()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        public void RequestVisibilityUpdate()
        {
            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UpdateSingleObjectVisibility(this);
            }
        }

        public void ApplyFogState(FogState state)
        {
            bool showSprite = state == FogState.Visible ||
                              (_visibilityMode == FogVisibilityMode.RememberWhenExplored && state == FogState.Explored);

            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].enabled = showSprite;
                    }
                }
            }

            bool isCurrentlyVisible = (state == FogState.Visible);
            if (_activeOnlyWhenVisible != null)
            {
                for (int i = 0; i < _activeOnlyWhenVisible.Length; i++)
                {
                    if (_activeOnlyWhenVisible[i] != null && _activeOnlyWhenVisible[i].activeSelf != isCurrentlyVisible)
                    {
                        _activeOnlyWhenVisible[i].SetActive(isCurrentlyVisible);
                    }
                }
            }
        }
    }
}