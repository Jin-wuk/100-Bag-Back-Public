using UnityEngine;
using System.Collections;
using BagBack100.Core;

namespace BagBack100.Combat
{
    /// <summary>
    /// 하체(이동)와 상체(공격)가 분리되어 병렬로 동작하며,
    /// 피격 시 이벤트를 받아 회피 기동을 수행하는 원거리 몬스터용 고급 AI 브레인입니다.
    /// </summary>
    public class RangedMonsterBrain : IMonsterState
    {
        private Coroutine _locomotionCoroutine;
        private Coroutine _combatCoroutine;
        private Coroutine _evasionCoroutine;
        
        private MonsterController _context;
        private bool _isEvading = false;

        public void Enter(MonsterController context)
        {
            _context = context;
            
            // 피격 이벤트 구독 (반응형 AI)
            _context.OnDamaged += HandleDamageReactive;
            
            // 이동과 공격을 분리하여 병렬(Parallel)로 실행
            _locomotionCoroutine = _context.StartCoroutine(LocomotionRoutine());
            _combatCoroutine = _context.StartCoroutine(CombatRoutine());
        }

        public void Exit(MonsterController context)
        {
            _context.OnDamaged -= HandleDamageReactive;
            if (_locomotionCoroutine != null) _context.StopCoroutine(_locomotionCoroutine);
            if (_combatCoroutine != null) _context.StopCoroutine(_combatCoroutine);
            if (_evasionCoroutine != null) _context.StopCoroutine(_evasionCoroutine);
        }

        public IEnumerator ExecuteRoutine(MonsterController context)
        {
            // 이 브레인은 내부 병렬 코루틴으로 작동하므로 FSM 루프를 강제로 딜레이시킴
            yield return new WaitForSeconds(1f);
        }

        /// <summary>
        /// 피격 시 즉각적으로 기존 행동을 취소하고 회피 기동에 돌입합니다.
        /// </summary>
        private void HandleDamageReactive()
        {
            if (_isEvading || _context.Player == null) return;
            
            // 하체와 상체의 기존 루틴을 강제 정지 (Interrupt)
            if (_locomotionCoroutine != null) _context.StopCoroutine(_locomotionCoroutine);
            if (_combatCoroutine != null) _context.StopCoroutine(_combatCoroutine);
            
            _evasionCoroutine = _context.StartCoroutine(EvasionAndCounterRoutine());
        }

        private IEnumerator EvasionAndCounterRoutine()
        {
            _isEvading = true;
            
            // 1. 유저 반대 방향으로 길게 대시/텔레포트 (3칸)
            Vector3 dirFromPlayer = (_context.transform.position - _context.Player.transform.position).normalized;
            dirFromPlayer = SnapTo8Way(dirFromPlayer);
            
            Vector3Int currentCell = _context.Grid.WorldToCell(_context.transform.position);
            Vector3Int retreatCell = currentCell + new Vector3Int(Mathf.RoundToInt(dirFromPlayer.x * 3), Mathf.RoundToInt(dirFromPlayer.y * 3), 0);
            
            // 장애물이 없는지 확인 후 즉시 공간 이동 (텔레포트 기믹)
            if (!Pathfinder.IsObstacle(_context.Grid, new Vector2Int(retreatCell.x, retreatCell.y), _context.ObstacleLayerMask))
            {
                if (TileSystem.Instance.TryOccupyTile(retreatCell, _context.gameObject, currentCell))
                {
                    // TODO: 파티클 등 텔레포트 연출 추가
                    _context.transform.position = _context.Grid.GetCellCenterWorld(retreatCell);
                }
            }

            // 회피 후 자세를 추스르는 시간 (0.2초)
            yield return new WaitForSeconds(0.2f);

            // 2. 즉시 반격 (Counter Attack)
            yield return _context.StartCoroutine(FireProjectileRoutine());

            // 3. 다시 일상 패턴(병렬 AI) 재가동
            _isEvading = false;
            _locomotionCoroutine = _context.StartCoroutine(LocomotionRoutine());
            _combatCoroutine = _context.StartCoroutine(CombatRoutine());
        }

        /// <summary>
        /// 하체 로직: 거리 유지(Kiting)만 담당
        /// </summary>
        private IEnumerator LocomotionRoutine()
        {
            while (true)
            {
                if (_context.Player == null) { yield return new WaitForSeconds(0.5f); continue; }

                float dist = Vector3.Distance(_context.transform.position, _context.Player.transform.position);

                // 유저가 3칸 이내로 접근하면 뒤로 도망감 (Kiting)
                if (dist < 3f) 
                {
                    Vector3 startPos = _context.transform.position;
                    Vector3 targetPos = startPos + (startPos - _context.Player.transform.position).normalized * 2f;
                    
                    var path = Pathfinder.FindPath(_context.Grid, startPos, targetPos, _context.ObstacleLayerMask);
                    if (path != null && path.Count > 0)
                    {
                        Vector3 nextStep = path[0];
                        Vector3Int currentCell = _context.Grid.WorldToCell(startPos);
                        Vector3Int targetCell = _context.Grid.WorldToCell(nextStep);
                        
                        if (TileSystem.Instance.TryOccupyTile(targetCell, _context.gameObject, currentCell))
                        {
                            yield return _context.StartCoroutine(_context.MoveToTileRoutine(nextStep));
                        }
                    }
                }
                
                yield return new WaitForSeconds(0.3f); // AI 이동 판단 틱
            }
        }

        /// <summary>
        /// 상체 로직: 쿨타임과 공격만 담당
        /// </summary>
        private IEnumerator CombatRoutine()
        {
            while (true)
            {
                if (_context.Player != null)
                {
                    float dist = Vector3.Distance(_context.transform.position, _context.Player.transform.position);
                    // 적정 사거리(2.5 ~ ZOC 반경)에 들어오면 사격 개시
                    if (dist <= _context.Data.zocRadius && dist >= 2.5f) 
                    {
                        yield return _context.StartCoroutine(FireProjectileRoutine());
                        yield return new WaitForSeconds(2.5f); // 원거리 공격 쿨타임
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator FireProjectileRoutine()
        {
            Vector3 targetPos = _context.Player.transform.position;
            Vector3 cellTargetPos = _context.Grid.GetCellCenterWorld(_context.Grid.WorldToCell(targetPos));
            
            if (_context.WarningPrefab != null)
            {
                var warning = Object.Instantiate(_context.WarningPrefab);
                warning.ShowWarning(cellTargetPos, _context.Data.telegraphTime);
            }

            yield return new WaitForSeconds(_context.Data.telegraphTime);
            
            Vector3 dir = (cellTargetPos - _context.transform.position).normalized;
            
            // 현재 근접 공격 메서드를 호출하고 있으나, 나중에 EntityCombat.PerformRangedAttack으로 교체 가능
            _context.Combat.PerformMeleeAttack(_context.Player.gameObject, _context.Data.speed, _context.Data.atk, dir);

            while (_context.Combat.IsAttackOnCooldown) yield return null;
        }

        private Vector3 SnapTo8Way(Vector3 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            float rad = snappedAngle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0).normalized;
        }
    }
}
