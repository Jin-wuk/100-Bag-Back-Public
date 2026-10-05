using UnityEngine;
using BagBack100.Combat;

namespace BagBack100.Player
{
    public enum PlayerState
    {
        Idle,
        Moving,
        Attacking
    }

    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(EntityCombat))]
    [RequireComponent(typeof(PlayerStatManager))]
    public class PlayerController : MonoBehaviour
    {
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        [Header("Combat Settings")]
        [SerializeField] private float _meleeOffset = 0.5f;
        [SerializeField] private Vector3 _meleeScale = Vector3.one;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _projectileSpawnPoint;
        
        private PlayerInput _input;
        private PlayerMovement _movement;
        private EntityCombat _combat;
        private PlayerStatManager _stats;
        private EquipmentManager _equipment;

        private Vector3 _facingDirection = Vector3.down;
        private Grid _grid;

        private System.Action _bufferedCommand;

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<EntityCombat>();
            _stats = GetComponent<PlayerStatManager>();
            _equipment = GetComponent<EquipmentManager>();
            
            _input.OnMoveInput += HandleMoveInput;
            _input.OnAimInput += HandleAimInput;
            _input.OnAttackInput += HandleAttackInput;
            _input.OnSpecialAttackKeyPressed += HandleSpecialAttack;
            _movement.OnMovementFinished += HandleMovementFinished;
            _movement.OnTileReached += HandleTileReached;
            _combat.OnAttackFinished += HandleAttackFinished;
        }

        private void Start()
        {
            _grid = FindObjectOfType<Grid>();
            if (Camera.main != null && Camera.main.TryGetComponent<BagBack100.Core.CameraFollow>(out var cameraFollow))
            {
                cameraFollow.SetTarget(this.transform);
            }
            if (_projectileSpawnPoint == null)
            {
                _projectileSpawnPoint = this.transform;
            }
            
            // Register initial tile
            if (_grid != null && BagBack100.Core.TileSystem.Instance != null)
            {
                BagBack100.Core.TileSystem.Instance.RegisterInitialTile(_grid.WorldToCell(transform.position), gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnMoveInput -= HandleMoveInput;
                _input.OnAimInput -= HandleAimInput;
                _input.OnAttackInput -= HandleAttackInput;
                _input.OnSpecialAttackKeyPressed -= HandleSpecialAttack;
            }
            if (_movement != null)
            {
                _movement.OnMovementFinished -= HandleMovementFinished;
                _movement.OnTileReached -= HandleTileReached;
            }
            if (_combat != null)
            {
                _combat.OnAttackFinished -= HandleAttackFinished;
            }
                
            if (_grid != null && BagBack100.Core.TileSystem.Instance != null)
            {
                BagBack100.Core.TileSystem.Instance.ReleaseTile(_grid.WorldToCell(transform.position), gameObject);
            }
        }

        private void HandleAttackFinished()
        {
            if (CurrentState == PlayerState.Attacking)
            {
                CurrentState = PlayerState.Idle;
            }
        }

        private void HandleAimInput(Vector3 aimDir)
        {
            if (CurrentState == PlayerState.Attacking) return;
            
            // 8방향 스냅(Snap)
            float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            float rad = snappedAngle * Mathf.Deg2Rad;
            _facingDirection = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0).normalized;
        }

        private void HandleMoveInput(Vector2Int moveDir)
        {
            if (CurrentState == PlayerState.Attacking || _combat.IsAttackOnCooldown) return;
            
            if (CurrentState == PlayerState.Moving)
            {
                _movement.CancelPath();
                return;
            }

            if (_grid == null) return;

            Vector3Int currentCellPos = _grid.WorldToCell(transform.position);
            Vector3Int targetCellPos = currentCellPos + new Vector3Int(moveDir.x, moveDir.y, 0);
            Vector3 targetPos = _grid.GetCellCenterWorld(targetCellPos);

            // 이동 (몬스터 유무 관계없이 이동 시도)
            CurrentState = PlayerState.Moving;
            _movement.MoveTo(targetPos);
        }

        private void HandleAttackInput(Vector3Int targetCell)
        {
            if (_combat.IsAttackOnCooldown) return;

            if (CurrentState == PlayerState.Moving)
            {
                // 선입력 (Input Buffering)
                _bufferedCommand = () => ExecuteMeleeAttack(targetCell);
            }
            else
            {
                ExecuteMeleeAttack(targetCell);
            }
        }

        private void ExecuteMeleeAttack(Vector3Int targetCellPos)
        {
            if (_grid == null || _combat.IsAttackOnCooldown) return;

            CurrentState = PlayerState.Attacking;

            // 1. 타일에서 몬스터 찾기
            GameObject occupant = null;
            if (BagBack100.Core.TileSystem.Instance != null)
            {
                occupant = BagBack100.Core.TileSystem.Instance.GetEntityAtTile(targetCellPos);
            }

            MonsterController monster = occupant != null ? occupant.GetComponent<MonsterController>() : null;
            
            // 현재 장착 중인 무기 가져오기
            GameObject currentMeleePrefab = null;
            if (_equipment != null)
            {
                var weaponItem = _equipment.GetEquippedItem(BagBack100.Data.Core.EquipType.Weapon);
                if (weaponItem != null && weaponItem.ItemData is BagBack100.Data.Models.WeaponDataSO weaponData)
                {
                    currentMeleePrefab = weaponData.weaponPrefab;
                }
            }

            // 2. 공격 실행 (몬스터가 없어도 헛스윙 및 모션 락 적용, 검 프리팹 생성)
            _combat.PerformMeleeAttack(monster != null ? monster.gameObject : null, _stats.CurrentAttackSpeed, _stats.CurrentAttack, _facingDirection, currentMeleePrefab, transform, _meleeOffset, _meleeScale);
        }

        private void HandleSpecialAttack()
        {
            if (CurrentState != PlayerState.Idle || _combat.IsAttackOnCooldown) return;

            _combat.PerformRangedAttack(_projectilePrefab, _projectileSpawnPoint, _stats.CurrentSpeed, _stats.CurrentAttack, _facingDirection);
        }

        private void HandleTileReached(Vector3 pos)
        {
            if (_grid != null && BagBack100.Core.TileSystem.Instance != null)
            {
                Vector3Int currentCell = _grid.WorldToCell(pos);
                currentCell.z = 0; // 2D 그리드에서 Z축 오차 방지

                var item = BagBack100.Core.TileSystem.Instance.GetItemAtTile(currentCell);
                if (item != null)
                {
                    item.PickUpByPlayer();
                }
            }
            
            // 타일 안착 시 선입력 커맨드 실행
            if (_bufferedCommand != null)
            {
                _bufferedCommand.Invoke();
                _bufferedCommand = null;
            }
        }

        private void HandleMovementFinished()
        {
            if (CurrentState == PlayerState.Moving)
            {
                CurrentState = PlayerState.Idle;
            }
        }
    }
}
