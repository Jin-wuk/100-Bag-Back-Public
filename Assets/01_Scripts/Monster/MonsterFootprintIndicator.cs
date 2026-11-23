using UnityEngine;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 여러 칸 몬스터(보스)의 실제 점유 칸을 발밑에 옅은 테두리로 보여 주는 View. 이동 중이면 옛 몸 + 새 몸 칸을 감싼다.
    ///       플레이 테스트로 유지 여부를 정하기 위해 _visible로 끄고 켤 수 있다 (11 설계안 4.5절).
    /// Dependencies: MonsterController (OnBodyCellsChanged, OnFogVisibilityChanged, TryGetOccupiedCellBounds)
    /// Events: 없음
    /// </summary>
    [RequireComponent(typeof(MonsterController))]
    public class MonsterFootprintIndicator : MonoBehaviour
    {
        [Tooltip("기본 꺼짐: 플레이 테스트에서 화면과 어울리지 않아 끔(2026-11-23 유저 결정). 점유 디버깅 때 켠다")]
        [SerializeField] private bool _visible = false;
        [Tooltip("9-slice 테두리 스프라이트 (Sprite Mode Single, Border 설정 필요)")]
        [SerializeField] private Sprite _outlineSprite;
        [SerializeField] private Color _color = new Color(1f, 0.35f, 0.3f, 0.25f);
        [Tooltip("바닥 체커(-19)보다 위, 몬스터(1)보다 아래")]
        [SerializeField] private int _sortingOrder = 0;

        private MonsterController _monster;
        private SpriteRenderer _renderer;

        private void Awake()
        {
            _monster = GetComponent<MonsterController>();
        }

        // 렌더러는 몬스터 밖(형제)에 Start에서 만든다. MonsterController.Awake가 자식 SpriteRenderer를 안개 대상으로 모으는데,
        // 거기 들어가면 SetVisibleByFog가 _visible을 무시하고 켜 버린다
        private void Start()
        {
            if (_outlineSprite == null || _monster.Footprint <= 1)
            {
                enabled = false;
                return;
            }

            // 몬스터 자식으로 두면 몸이 미끄러지는 동안 테두리도 따라 움직인다. 점유 칸에 고정하려고 같은 부모(층) 아래 형제로 둔다
            GameObject child = new GameObject(name + "_FootprintIndicator");
            child.transform.SetParent(transform.parent, false);
            _renderer = child.AddComponent<SpriteRenderer>();
            _renderer.sprite = _outlineSprite;
            _renderer.drawMode = SpriteDrawMode.Sliced;
            _renderer.color = _color;
            _renderer.sortingOrder = _sortingOrder;

            _monster.OnBodyCellsChanged += Refresh;
            _monster.OnFogVisibilityChanged += HandleFogChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_renderer != null) Destroy(_renderer.gameObject);
            if (_monster == null) return;
            _monster.OnBodyCellsChanged -= Refresh;
            _monster.OnFogVisibilityChanged -= HandleFogChanged;
        }

        // 인스펙터에서 _visible을 바꾸면 실행 중에도 바로 반영
        private void OnValidate()
        {
            if (Application.isPlaying && _renderer != null) Refresh();
        }

        private void HandleFogChanged(bool _) => Refresh();

        private void Refresh()
        {
            if (_renderer == null) return;

            Vector3Int min = Vector3Int.zero, max = Vector3Int.zero;
            bool show = _visible && _monster.IsVisibleByFog && _monster.Grid != null
                && _monster.TryGetOccupiedCellBounds(out min, out max);
            if (show)
            {
                // 점유 칸을 감싸는 사각형. 칸 크기는 Grid 기준
                Vector3 minCenter = _monster.Grid.GetCellCenterWorld(min);
                Vector3 maxCenter = _monster.Grid.GetCellCenterWorld(max);
                Vector3 cell = _monster.Grid.cellSize;
                Vector3 center = (minCenter + maxCenter) * 0.5f;
                _renderer.transform.position = new Vector3(center.x, center.y, 0f);
                _renderer.size = new Vector2(maxCenter.x - minCenter.x + cell.x, maxCenter.y - minCenter.y + cell.y);
            }

            _renderer.enabled = show;
        }
    }
}
