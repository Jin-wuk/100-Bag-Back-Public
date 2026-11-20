using UnityEngine;
using BagBack100.Data.Models;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 몬스터가 바라보는 방향에 맞춰 SpriteRenderer의 4방향 스프라이트만 바꾼다. 색·스케일·표시 여부(피격·스턴·안개 연출)는 건드리지 않는다.
    ///       방향은 플레이어처럼 마지막 행동 기준: 이동 중 = 이동 방향, 패턴(공격)을 시작하면 그 순간의 플레이어 쪽, 그 외에는 그대로 유지.
    /// Dependencies: DirectionalSpriteSO, MonsterController(이동 상태·이동 방향·마지막 행동 방향)
    /// Events: 없음
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterFacingVisual : MonoBehaviour
    {
        [SerializeField] private DirectionalSpriteSO _sprites;

        private SpriteRenderer _renderer;
        private MonsterController _monster;
        private FacingDir4 _facing = FacingDir4.South;
        private int _lastActionCount;
        private Sprite _lastSprite;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _monster = GetComponentInParent<MonsterController>();

            if (_sprites == null || _monster == null)
            {
                Debug.LogError($"[MonsterFacingVisual] {name}: 방향 스프라이트 SO 또는 MonsterController가 없어 비활성화합니다.", this);
                enabled = false;
                return;
            }
            _lastActionCount = _monster.ActionCount;
        }

        private void LateUpdate()
        {
            if (_monster.IsMovingToTile)
            {
                _facing = DirectionalSpriteSO.FromVector(_monster.CurrentMoveDirection, _facing);
            }
            else if (_monster.ActionCount != _lastActionCount)
            {
                // 패턴 시작 순간에만 방향을 잡는다. 예고·공격 중에 플레이어를 따라 돌지 않는다
                _facing = DirectionalSpriteSO.FromVector(_monster.LastActionDirection, _facing);
            }
            _lastActionCount = _monster.ActionCount;

            Sprite next = _sprites.Get(_facing);
            if (next != null && next != _lastSprite)
            {
                _renderer.sprite = next;
                _lastSprite = next;
            }
        }
    }
}
