using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BagBack100.Core;
using BagBack100.Player;

namespace BagBack100.Combat
{
    public interface IMonsterState
    {
        void Enter(MonsterController context);
        IEnumerator ExecuteRoutine(MonsterController context);
        void Exit(MonsterController context);
    }

    public class MeleeIdleState : IMonsterState
    {
        public void Enter(MonsterController context) { }
        public void Exit(MonsterController context) { }

        public IEnumerator ExecuteRoutine(MonsterController context)
        {
            if (context.Player != null)
            {
                float dist = Vector3.Distance(context.transform.position, context.Player.transform.position);
                if (dist <= context.Data.zocRadius)
                {
                    context.ChangeState(new MeleeChaseState());
                    yield break;
                }
            }

            // Patrol: 무작위 4방향 이동
            if (Random.value < 0.4f && context.Grid != null && TileSystem.Instance != null)
            {
                Vector3Int currentCell = context.Grid.WorldToCell(context.transform.position);
                Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
                Vector3Int targetCell = currentCell + dirs[Random.Range(0, 4)];
                
                if (!Pathfinder.IsObstacle(context.Grid, new Vector2Int(targetCell.x, targetCell.y), context.ObstacleLayerMask))
                {
                    if (TileSystem.Instance.TryOccupyTile(targetCell, context.gameObject, currentCell))
                    {
                        Vector3 endPos = context.Grid.GetCellCenterWorld(targetCell);
                        yield return context.StartCoroutine(context.MoveToTileRoutine(endPos));
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    public class MeleeChaseState : IMonsterState
    {
        public void Enter(MonsterController context) { }
        public void Exit(MonsterController context) { }

        public IEnumerator ExecuteRoutine(MonsterController context)
        {
            if (context.Player == null)
            {
                context.ChangeState(new MeleeIdleState());
                yield break;
            }

            float dist = Vector3.Distance(context.transform.position, context.Player.transform.position);

            if (dist > context.Data.zocRadius)
            {
                context.ChangeState(new MeleeIdleState());
                yield break;
            }

            float cellSize = context.Grid != null ? context.Grid.cellSize.x : 1f;

            // 광역기 사거리(2.5칸) 내에 들어왔을 때 30% 확률로 광역 패턴 시전
            if (dist <= cellSize * 2.5f)
            {
                if (Random.value < 0.3f)
                {
                    context.ChangeState(new MeleeAoEState());
                    yield break;
                }
                // 광역기가 안 터졌는데 기본 공격 사거리(1.5칸)라면 일반 공격 시전
                else if (dist <= cellSize * 1.5f)
                {
                    context.ChangeState(new MeleeTelegraphState());
                    yield break;
                }
            }

            yield return new WaitForSeconds(Random.Range(0.15f, 0.25f));

            Vector3 startPos = context.Grid.GetCellCenterWorld(context.Grid.WorldToCell(context.transform.position));
            Vector3 targetPos = context.Grid.GetCellCenterWorld(context.Grid.WorldToCell(context.Player.transform.position));

            var path = Pathfinder.FindPath(context.Grid, startPos, targetPos, context.ObstacleLayerMask);
            if (path != null && path.Count > 0)
            {
                Vector3 nextStep = path[0];
                if (Vector3.Distance(nextStep, targetPos) >= 0.1f)
                {
                    Vector3Int currentCell = context.Grid.WorldToCell(context.transform.position);
                    Vector3Int targetCell = context.Grid.WorldToCell(nextStep);
                    
                    if (TileSystem.Instance != null && TileSystem.Instance.TryOccupyTile(targetCell, context.gameObject, currentCell))
                    {
                        yield return context.StartCoroutine(context.MoveToTileRoutine(nextStep));
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
            }
        }
    }

    public class MeleeTelegraphState : IMonsterState
    {
        public void Enter(MonsterController context) { }
        public void Exit(MonsterController context) { }

        public IEnumerator ExecuteRoutine(MonsterController context)
        {
            if (context.Grid == null || context.Player == null) 
            {
                context.ChangeState(new MeleeIdleState());
                yield break;
            }
            
            Vector3 targetPos = context.Player.transform.position;
            Vector3 cellTargetPos = context.Grid.GetCellCenterWorld(context.Grid.WorldToCell(targetPos));
            
            if (context.WarningPrefab != null)
            {
                var warning = Object.Instantiate(context.WarningPrefab);
                warning.ShowWarning(cellTargetPos, context.Data.telegraphTime);
            }

            yield return new WaitForSeconds(context.Data.telegraphTime);

            Vector3 dir = (cellTargetPos - context.transform.position).normalized;
            Vector3 playerCellPos = context.Grid.GetCellCenterWorld(context.Grid.WorldToCell(context.Player.transform.position));
            
            float distToTargetTile = Vector3.Distance(playerCellPos, cellTargetPos);
            if (distToTargetTile < 0.1f || distToTargetTile < context.Grid.cellSize.x * 1.5f) 
            {
                context.Combat.PerformMeleeAttack(context.Player.gameObject, context.Data.speed, context.Data.atk, dir);
                while (context.Combat.IsAttackOnCooldown) yield return null;
            }

            context.ChangeState(new MeleeChaseState());
        }
    }

    /// <summary>
    /// 기를 모으며 몬스터 중심으로 장판이 확장되는 광역(AoE) 공격 패턴
    /// </summary>
    public class MeleeAoEState : IMonsterState
    {
        private int _maxRadius = 2; // 최대 2칸 (총 5x5 마름모 반경)

        public void Enter(MonsterController context) { }
        public void Exit(MonsterController context) { }

        public IEnumerator ExecuteRoutine(MonsterController context)
        {
            if (context.Grid == null || context.Player == null) 
            {
                context.ChangeState(new MeleeIdleState());
                yield break;
            }

            Vector3Int centerCell = context.Grid.WorldToCell(context.transform.position);
            
            // 확장 단계별로 시간 분배
            float totalTelegraphTime = context.Data.telegraphTime * 1.5f; // 광역기는 기를 좀 더 오래 모음
            float stepTime = totalTelegraphTime / (_maxRadius + 1);

            // 1. 순차적으로 장판 확장 (기를 모으는 연출)
            for (int r = 0; r <= _maxRadius; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        // 정확히 현재 반지름(r)에 해당하는 테두리 타일(맨해튼 거리)에만 장판 생성
                        if (Mathf.Abs(x) + Mathf.Abs(y) == r)
                        {
                            Vector3Int cellPos = centerCell + new Vector3Int(x, y, 0);
                            Vector3 worldPos = context.Grid.GetCellCenterWorld(cellPos);
                            
                            // 맵 바깥이나 벽 너머는 무시할 수도 있지만 임시로 전부 표시
                            if (context.WarningPrefab != null)
                            {
                                var warning = Object.Instantiate(context.WarningPrefab);
                                // 폭발이 터지는 동시점까지 남은 시간만큼만 UI 유지
                                float remainingTime = totalTelegraphTime - (stepTime * r) + 0.2f; 
                                warning.ShowWarning(worldPos, remainingTime);
                            }
                        }
                    }
                }
                // 다음 반경으로 장판이 퍼지기 전 대기
                yield return new WaitForSeconds(stepTime);
            }

            // 모든 장판이 깔리고 난 후 터지기 직전 극히 짧은 딜레이
            yield return new WaitForSeconds(0.2f);

            // 2. 광역 폭발 판정
            Vector3Int playerCell = context.Grid.WorldToCell(context.Player.transform.position);
            int distToPlayer = Mathf.Abs(playerCell.x - centerCell.x) + Mathf.Abs(playerCell.y - centerCell.y);

            // 유저가 반경 안에 남아있으면 2배 데미지 피격
            if (distToPlayer <= _maxRadius)
            {
                Vector3 dir = (context.Player.transform.position - context.transform.position).normalized;
                context.Combat.PerformMeleeAttack(context.Player.gameObject, context.Data.speed, context.Data.atk * 2, dir);
                while (context.Combat.IsAttackOnCooldown) yield return null;
            }

            context.ChangeState(new MeleeChaseState());
        }
    }
}
