using UnityEngine;
using DimensionLooter.Combat;

namespace DimensionLooter.Player
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
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _projectileSpawnPoint;
        
        private PlayerInput _input;
        private PlayerMovement _movement;
        private EntityCombat _combat;
        private PlayerStatManager _stats;

        private Vector3 _facingDirection = Vector3.down;
        private Grid _grid;

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<EntityCombat>();
            _stats = GetComponent<PlayerStatManager>();
            
            _input.OnTileClicked += HandleTileClicked;
            _input.OnDirectionalInput += HandleDirectionalInput;
            _input.OnSpecialAttackKeyPressed += HandleSpecialAttack;
            _movement.OnMovementFinished += HandleMovementFinished;
        }

        private void Start()
        {
            _grid = FindObjectOfType<Grid>();
            if (Camera.main != null && Camera.main.TryGetComponent<DimensionLooter.Core.CameraFollow>(out var cameraFollow))
            {
                cameraFollow.SetTarget(this.transform);
            }
            if (_projectileSpawnPoint == null)
            {
                _projectileSpawnPoint = this.transform;
            }
            
            // Register initial tile
            if (_grid != null && DimensionLooter.Core.TileSystem.Instance != null)
            {
                DimensionLooter.Core.TileSystem.Instance.RegisterInitialTile(_grid.WorldToCell(transform.position), gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnTileClicked -= HandleTileClicked;
                _input.OnDirectionalInput -= HandleDirectionalInput;
                _input.OnSpecialAttackKeyPressed -= HandleSpecialAttack;
            }
            if (_movement != null)
                _movement.OnMovementFinished -= HandleMovementFinished;
                
            if (_grid != null && DimensionLooter.Core.TileSystem.Instance != null)
            {
                DimensionLooter.Core.TileSystem.Instance.ReleaseTile(_grid.WorldToCell(transform.position), gameObject);
            }
        }

        private void HandleTileClicked(Vector3 targetPos)
        {
            if (CurrentState == PlayerState.Attacking || _combat.IsAttackOnCooldown) return;
            
            // Update facing direction
            Vector3 dir = (targetPos - transform.position).normalized;
            if (dir.magnitude > 0.1f) _facingDirection = dir;

            _movement.MoveTo(targetPos);
            CurrentState = PlayerState.Moving;
        }

        private void HandleDirectionalInput(Vector2Int dirInt)
        {
            if (CurrentState != PlayerState.Idle || _combat.IsAttackOnCooldown) return;

            Vector3 direction = new Vector3(dirInt.x, dirInt.y, 0);
            _facingDirection = direction;

            if (_grid == null) return;

            Vector3Int currentCellPos = _grid.WorldToCell(transform.position);
            Vector3Int targetCellPos = currentCellPos + new Vector3Int(dirInt.x, dirInt.y, 0);
            Vector3 targetPos = _grid.GetCellCenterWorld(targetCellPos);

            // Check for monster in the target tile
            GameObject occupant = null;
            if (DimensionLooter.Core.TileSystem.Instance != null)
            {
                occupant = DimensionLooter.Core.TileSystem.Instance.GetEntityAtTile(targetCellPos);
            }
            else
            {
                Collider2D col = Physics2D.OverlapPoint(targetPos);
                if (col != null) occupant = col.gameObject;
            }

            MonsterController monster = null;
            if (occupant != null)
            {
                monster = occupant.GetComponent<MonsterController>();
            }

            if (monster != null)
            {
                // Bump Attack
                _combat.PerformMeleeAttack(monster.gameObject, _stats.CurrentSpeed, _stats.CurrentAttack, direction);
            }
            else
            {
                // Move
                _movement.MoveTo(targetPos);
                CurrentState = PlayerState.Moving;
            }
        }

        private void HandleSpecialAttack()
        {
            if (CurrentState != PlayerState.Idle || _combat.IsAttackOnCooldown) return;

            _combat.PerformRangedAttack(_projectilePrefab, _projectileSpawnPoint, _stats.CurrentSpeed, _stats.CurrentAttack, _facingDirection);
        }

        private void HandleMovementFinished()
        {
            CurrentState = PlayerState.Idle;
        }
    }
}
