using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DimensionLooter.Data.Models;
using DimensionLooter.Player;
using DimensionLooter.Core;

namespace DimensionLooter.Combat
{
    public enum MonsterState { Idle, Chase, Telegraphing }

    [RequireComponent(typeof(EntityCombat))]
    public class MonsterController : MonoBehaviour
    {
        [SerializeField] private MonsterDataSO _monsterData;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [SerializeField] private AttackWarningUI _warningPrefab;

        public MonsterDataSO Data => _monsterData;
        public int CurrentHp { get; private set; } // Local setter for now
        public int InstanceID => gameObject.GetInstanceID();

        private MonsterState _state = MonsterState.Idle;
        private EntityCombat _combat;
        private Grid _grid;
        private PlayerController _player;
        private Coroutine _behaviorCoroutine;
        private bool _isMoving = false;

        public void Initialize(MonsterDataSO data)
        {
            _monsterData = data;
            CurrentHp = _monsterData.hp;
        }

        private void Start()
        {
            _combat = GetComponent<EntityCombat>();
            _grid = FindObjectOfType<Grid>();
            _player = FindObjectOfType<PlayerController>();

            if (_monsterData != null)
            {
                CurrentHp = _monsterData.hp;
            }
            
            if (_grid != null && TileSystem.Instance != null)
            {
                TileSystem.Instance.RegisterInitialTile(_grid.WorldToCell(transform.position), gameObject);
            }
            
            _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
        }
        
        private void OnDestroy()
        {
            if (_grid != null && TileSystem.Instance != null)
            {
                TileSystem.Instance.ReleaseTile(_grid.WorldToCell(transform.position), gameObject);
            }
        }

        public void TakeDamage(int damage)
        {
            CurrentHp -= damage;
            Debug.Log($"[Monster] {_monsterData.monsterName} 가 {damage} 피해를 입음! 남은 HP: {CurrentHp}");
            StartCoroutine(FlashRedRoutine());
            
            if (CurrentHp <= 0) 
            {
                CurrentHp = 0;
                Debug.Log($"[Monster] {_monsterData.monsterName} 처치됨!");
                Die();
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            var renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                Color original = renderer.color;
                renderer.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                renderer.color = original;
            }
        }

        private void Die()
        {
            if (_behaviorCoroutine != null) StopCoroutine(_behaviorCoroutine);
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            CurrentHp += amount;
            if (CurrentHp > _monsterData.hp) CurrentHp = _monsterData.hp;
        }

        private IEnumerator BehaviorRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.3f); // AI Tick

                if (_player == null || _state == MonsterState.Telegraphing || _combat.IsAttackOnCooldown || _isMoving) continue;

                float dist = Vector3.Distance(transform.position, _player.transform.position);

                if (dist <= _monsterData.zocRadius)
                {
                    if (dist <= (_grid != null ? _grid.cellSize.x * 1.5f : 1.5f))
                    {
                        _state = MonsterState.Telegraphing;
                        StartCoroutine(TelegraphAndAttackRoutine());
                    }
                    else
                    {
                        _state = MonsterState.Chase;
                        MoveTowardsPlayer();
                    }
                }
                else
                {
                    _state = MonsterState.Idle;
                }
            }
        }

        private void MoveTowardsPlayer()
        {
            if (_grid == null || _player == null) return;
            Vector3 startPos = _grid.GetCellCenterWorld(_grid.WorldToCell(transform.position));
            Vector3 targetPos = _grid.GetCellCenterWorld(_grid.WorldToCell(_player.transform.position));

            var path = Pathfinder.FindPath(_grid, startPos, targetPos, _obstacleLayerMask);
            if (path != null && path.Count > 0)
            {
                Vector3 nextStep = path[0];
                // Prevent stepping on same tile as player
                if (Vector3.Distance(nextStep, targetPos) < 0.1f) return;
                
                Vector3Int currentCell = _grid.WorldToCell(transform.position);
                Vector3Int targetCell = _grid.WorldToCell(nextStep);
                
                if (TileSystem.Instance != null)
                {
                    if (!TileSystem.Instance.TryOccupyTile(targetCell, gameObject, currentCell))
                    {
                        return; // Occupation failed, wait for next AI tick
                    }
                }
                
                StartCoroutine(MoveToTileRoutine(nextStep));
            }
        }

        private IEnumerator MoveToTileRoutine(Vector3 endPos)
        {
            _isMoving = true;
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Vector3 lerpedPos = Vector3.Lerp(startPos, endPos, t);
                lerpedPos.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
                transform.position = lerpedPos;
                yield return null;
            }
            transform.position = endPos;
            _isMoving = false;
        }

        private IEnumerator TelegraphAndAttackRoutine()
        {
            if (_grid == null) yield break;
            
            Vector3 targetPos = _player.transform.position;
            Vector3 cellTargetPos = _grid.GetCellCenterWorld(_grid.WorldToCell(targetPos));
            
            if (_warningPrefab != null)
            {
                var warning = Instantiate(_warningPrefab);
                warning.ShowWarning(cellTargetPos, _monsterData.telegraphTime);
            }

            yield return new WaitForSeconds(_monsterData.telegraphTime);

            Vector3 dir = (cellTargetPos - transform.position).normalized;
            Vector3 playerCellPos = _grid.GetCellCenterWorld(_grid.WorldToCell(_player.transform.position));
            
            if (Vector3.Distance(playerCellPos, cellTargetPos) < 0.1f)
            {
                _combat.PerformMeleeAttack(_player.gameObject, _monsterData.speed, _monsterData.atk, dir);
            }

            _state = MonsterState.Idle;
        }

        private void OnDrawGizmosSelected()
        {
            if (_monsterData != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawSphere(transform.position, _monsterData.zocRadius);
            }
        }
    }
}
