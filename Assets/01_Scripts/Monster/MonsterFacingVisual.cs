using UnityEngine;
using BagBack100.Data.Models;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 몬스터가 바라보는 방향에 맞춰 SpriteRenderer의 8방향 스프라이트만 바꾼다. 색·스케일·표시 여부(피격·스턴·안개 연출)는 건드리지 않는다.
    ///       이동 중 = 이동 방향, 전투 중 대기·예고·공격 = 플레이어 쪽, 비전투 대기 = 마지막 방향 유지.
    /// Dependencies: DirectionalSpriteSO, MonsterController(이동 상태·이동 방향·어그로·플레이어)
    /// Events: 없음
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterFacingVisual : MonoBehaviour
    {
        [SerializeField] private DirectionalSpriteSO _sprites;

        private SpriteRenderer _renderer;
        private MonsterController _monster;
        private FacingDir8 _facing = FacingDir8.S;
        private Sprite _lastSprite;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _monster = GetComponentInParent<MonsterController>();

            if (_sprites == null || _monster == null)
            {
                Debug.LogError($"[MonsterFacingVisual] {name}: 방향 스프라이트 SO 또는 MonsterController가 없어 비활성화합니다.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_monster.IsMovingToTile)
            {
                _facing = DirectionalSpriteSO.FromVector(_monster.CurrentMoveDirection, _facing);
            }
            else if (_monster.IsAggroed && _monster.Player != null)
            {
                Vector3 toPlayer = _monster.Player.transform.position - transform.position;
                _facing = DirectionalSpriteSO.FromVector(toPlayer, _facing);
            }

            Sprite next = _sprites.Get(_facing);
            if (next != null && next != _lastSprite)
            {
                _renderer.sprite = next;
                _lastSprite = next;
            }
        }
    }
}
