using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Combat;

namespace BagBack100.Player
{
    public enum PlayerState
    {
        Idle,
        Moving,
        Attacking,
        Charging
    }

    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(EntityCombat))]
    [RequireComponent(typeof(PlayerStatManager))]
    public class PlayerController : MonoBehaviour
    {
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        [Header("Combat & Combo Settings")]
        [SerializeField] private float _meleeOffset = 0.5f;
        [SerializeField] private Vector3 _meleeScale = Vector3.one;
        [Tooltip("무기 미장착 시 사용할 기본 근접 이펙트 프리팹")]
        [SerializeField] private GameObject _defaultMeleeEffectPrefab;
        [Tooltip("이동 중에도 콤보(1~3타)가 유지되는 최대 시간(초)")]
        [SerializeField] private float _comboResetTime = 1.5f;

        [Header("Right-Click Charge Attack Settings")]
        [Tooltip("우클릭 차징 완충까지 걸리는 시간(초) - 완충 전 손을 떼면 무효")]
        [SerializeField] private float _chargeDuration = 0.6f;
        [Tooltip("차징 강공격 전방 직선 타일 사거리")]
        [SerializeField] private int _chargeLineLength = 2;
        [Tooltip("차징 강공격 데미지 배율")]
        [SerializeField] private float _chargeDamageMultiplier = 2.0f;

        [Header("Ranged / Special Settings")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _projectileSpawnPoint;
        
        private PlayerInput _input;
        private PlayerMovement _movement;
        private EntityCombat _combat;
        private PlayerStatManager _stats;
        private EquipmentManager _equipment;
        private SpriteRenderer _spriteRenderer;

        private Vector3 _facingDirection = Vector3.down;
        private Grid _grid;

        private System.Action _bufferedCommand;

        // 3타 콤보 상태
        private int _comboStep = 0; // 0: 1타, 1: 2타, 2: 3타
        private float _lastComboAttackTime = -99f;

        // 우클릭 홀드 차징 상태
        private Coroutine _chargeCoroutine;
        private bool _isChargeReady = false;
        private readonly List<GameObject> _chargedTargetBuffer = new List<GameObject>();

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<EntityCombat>();
            _stats = GetComponent<PlayerStatManager>();
            _equipment = GetComponent<EquipmentManager>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            _input.OnMoveInput += HandleMoveInput;
            _input.OnAimInput += HandleAimInput;
            _input.OnAttackInput += HandleAttackInput;
            _input.OnChargeStartInput += HandleChargeStartInput;
            _input.OnChargeReleaseInput += HandleChargeReleaseInput;
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
            if (_grid != null)
            {
                Vector3Int startCell = _grid.WorldToCell(transform.position);
                startCell.z = 0;
                if (BagBack100.Core.TileSystem.Instance != null)
                {
                    BagBack100.Core.TileSystem.Instance.RegisterInitialTile(startCell, gameObject);
                }
                BagBack100.Core.GlobalEvents.OnPlayerCellChanged?.Invoke(startCell);
            }
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnMoveInput -= HandleMoveInput;
                _input.OnAimInput -= HandleAimInput;
                _input.OnAttackInput -= HandleAttackInput;
                _input.OnChargeStartInput -= HandleChargeStartInput;
                _input.OnChargeReleaseInput -= HandleChargeReleaseInput;
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
            // 공격 모션 중이거나 우클릭 홀드 차징 중에는 이동 불가
            if (CurrentState == PlayerState.Attacking || CurrentState == PlayerState.Charging || _combat.IsAttackOnCooldown) return;
            
            if (CurrentState == PlayerState.Moving)
            {
                _movement.CancelPath();
                return;
            }

            if (_grid == null) return;

            Vector3Int currentCellPos = _grid.WorldToCell(transform.position);
            Vector3Int targetCellPos = currentCellPos + new Vector3Int(moveDir.x, moveDir.y, 0);
            Vector3 targetPos = _grid.GetCellCenterWorld(targetCellPos);

            // 이동 (몬스터 유무 관계없이 이동 시도, 콤보 카운트는 리셋하지 않고 시간 기준으로만 유지)
            CurrentState = PlayerState.Moving;
            _movement.MoveTo(targetPos);
        }

        #region Left-Click: 3-Hit Combo Attack

        private void HandleAttackInput(Vector3Int targetCell)
        {
            if (CurrentState == PlayerState.Charging || _combat.IsAttackOnCooldown) return;

            if (CurrentState == PlayerState.Moving)
            {
                // 선입력 (Input Buffering)
                _bufferedCommand = () => ExecuteMeleeComboAttack(targetCell);
            }
            else
            {
                ExecuteMeleeComboAttack(targetCell);
            }
        }

        private void ExecuteMeleeComboAttack(Vector3Int targetCellPos)
        {
            if (_grid == null || _combat.IsAttackOnCooldown) return;

            // 콤보 유지 시간 체크 (이동했더라도 _comboResetTime 이내면 다음 타수로 진행)
            if (Time.time - _lastComboAttackTime > _comboResetTime)
            {
                _comboStep = 0;
            }

            int currentCombo = _comboStep;
            _comboStep = (_comboStep + 1) % 3;
            _lastComboAttackTime = Time.time;

            CurrentState = PlayerState.Attacking;

            // 1. 타일에서 몬스터 찾기 및 위험도 맵(Danger Map) 갱신
            GameObject occupant = null;
            if (BagBack100.Core.TileSystem.Instance != null)
            {
                occupant = BagBack100.Core.TileSystem.Instance.GetEntityAtTile(targetCellPos);
                BagBack100.Core.TileSystem.Instance.SetDanger(targetCellPos, 1f);
            }

            MonsterController monster = occupant != null ? occupant.GetComponent<MonsterController>() : null;

            // 2. 장착 무기 및 타수별 이펙트/배율 결정
            GameObject effectPrefab = _defaultMeleeEffectPrefab;
            if (_equipment != null)
            {
                var weaponItem = _equipment.GetEquippedItem(BagBack100.Data.Core.EquipType.Weapon);
                if (weaponItem != null && weaponItem.ItemData is BagBack100.Data.Models.WeaponDataSO weaponData)
                {
                    GameObject weaponEffect = weaponData.GetComboEffectPrefab(currentCombo);
                    if (weaponEffect != null) effectPrefab = weaponEffect;
                }
            }

            // 타수별 데미지 배율 및 스케일 변형 (1타: 정방향 1.0x / 2타: Y축 반전 1.15x / 3타: 확대 강타 1.5x)
            float dmgMultiplier = 1.0f;
            Vector3 comboScale = _meleeScale;
            bool isFinisher = (currentCombo == 2);

            if (currentCombo == 1)
            {
                dmgMultiplier = 1.15f;
                comboScale = new Vector3(_meleeScale.x * 1.08f, -_meleeScale.y * 1.08f, _meleeScale.z);
            }
            else if (currentCombo == 2)
            {
                dmgMultiplier = 1.5f;
                comboScale = _meleeScale * 1.35f;
            }

            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_stats.CurrentAttack * dmgMultiplier));

            _combat.PerformMeleeAttack(
                monster != null ? monster.gameObject : null,
                _stats.CurrentAttackSpeed,
                finalDamage,
                _facingDirection,
                effectPrefab,
                transform,
                _meleeOffset,
                comboScale,
                isFinisher
            );
        }

        #endregion

        #region Right-Click: Hold Charge 2-Tile Line Attack

        private void HandleChargeStartInput()
        {
            if (_combat.IsAttackOnCooldown || CurrentState == PlayerState.Attacking || CurrentState == PlayerState.Charging) return;

            if (CurrentState == PlayerState.Moving)
            {
                _bufferedCommand = StartCharging;
                _movement.CancelPath();
                return;
            }

            StartCharging();
        }

        private void StartCharging()
        {
            if (_combat.IsAttackOnCooldown) return;

            CurrentState = PlayerState.Charging;
            _isChargeReady = false;

            if (_chargeCoroutine != null) StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = StartCoroutine(ChargeHoldRoutine());
        }

        private IEnumerator ChargeHoldRoutine()
        {
            float elapsed = 0f;
            Color origColor = Color.white;
            Vector3 origScale = _spriteRenderer != null ? _spriteRenderer.transform.localScale : Vector3.one;

            while (elapsed < _chargeDuration)
            {
                // 마우스 우클릭이 도중에 풀렸다면 루틴 종료
                if (!Input.GetMouseButton(1))
                {
                    CancelChargeVisuals(origColor, origScale);
                    CurrentState = PlayerState.Idle;
                    _chargeCoroutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _chargeDuration);

                if (_spriteRenderer != null)
                {
                    // 차징 진행 중 노란빛 점멸 및 몸을 웅크리는 연출
                    float flash = Mathf.PingPong(elapsed * 12f, 1f);
                    _spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.3f, 1f), flash * 0.7f);
                    _spriteRenderer.transform.localScale = new Vector3(origScale.x * (1f + 0.12f * t), origScale.y * (1f - 0.12f * t), origScale.z);
                }

                yield return null;
            }

            // 완충 도달!
            _isChargeReady = true;
            CombatFeedbackSystem.EnsureInstance().SpawnProceduralHitFlash(transform.position, 1.1f, new Color(1f, 0.9f, 0.2f, 0.85f), 0.18f);

            // 완충 상태에서 마우스 우클릭을 유지하는 동안 강렬한 황금빛 유지
            while (Input.GetMouseButton(1))
            {
                if (_spriteRenderer != null)
                {
                    float pulse = Mathf.PingPong(Time.time * 18f, 1f);
                    _spriteRenderer.color = Color.Lerp(new Color(1f, 0.8f, 0.1f, 1f), new Color(1f, 0.4f, 0.1f, 1f), pulse);
                }
                yield return null;
            }

            // 완충 상태에서 우클릭을 떼면 2칸 직선 강공격 발동
            CancelChargeVisuals(origColor, origScale);
            _chargeCoroutine = null;
            ExecuteChargedLineAttack();
        }

        private void HandleChargeReleaseInput()
        {
            if (CurrentState != PlayerState.Charging) return;

            // ChargeHoldRoutine 내부에서도 감지하지만, 즉각적인 해제를 보장
            if (!_isChargeReady)
            {
                if (_chargeCoroutine != null)
                {
                    StopCoroutine(_chargeCoroutine);
                    _chargeCoroutine = null;
                }
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.color = Color.white;
                    _spriteRenderer.transform.localScale = Vector3.one;
                }
                CurrentState = PlayerState.Idle;
            }
        }

        private void CancelChargeVisuals(Color origColor, Vector3 origScale)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = origColor;
                _spriteRenderer.transform.localScale = origScale;
            }
        }

        private void ExecuteChargedLineAttack()
        {
            if (_grid == null)
            {
                CurrentState = PlayerState.Idle;
                return;
            }

            _isChargeReady = false;
            CurrentState = PlayerState.Attacking;

            // 조준 방향을 그리드 오프셋(dx, dy)으로 변환하여 전방 N칸(기본 2칸) 직선 타일 산출
            Vector3Int playerCell = _grid.WorldToCell(transform.position);
            playerCell.z = 0;

            int stepX = Mathf.RoundToInt(_facingDirection.x);
            int stepY = Mathf.RoundToInt(_facingDirection.y);
            if (stepX == 0 && stepY == 0) stepY = -1;

            _chargedTargetBuffer.Clear();

            for (int i = 1; i <= _chargeLineLength; i++)
            {
                Vector3Int targetCell = new Vector3Int(playerCell.x + stepX * i, playerCell.y + stepY * i, 0);
                Vector3 cellWorldPos = _grid.GetCellCenterWorld(targetCell);

                // 타일마다 충격파 잔상 표시
                CombatFeedbackSystem.EnsureInstance().SpawnProceduralHitFlash(
                    cellWorldPos,
                    1.15f,
                    new Color(1f, 0.75f, 0.2f, 0.75f),
                    0.2f
                );

                if (BagBack100.Core.TileSystem.Instance != null)
                {
                    BagBack100.Core.TileSystem.Instance.SetDanger(targetCell, 1.2f);
                    GameObject occupant = BagBack100.Core.TileSystem.Instance.GetEntityAtTile(targetCell);
                    if (occupant != null && occupant != gameObject && !_chargedTargetBuffer.Contains(occupant))
                    {
                        _chargedTargetBuffer.Add(occupant);
                    }
                }
            }

            GameObject effectPrefab = _defaultMeleeEffectPrefab;
            if (_equipment != null)
            {
                var weaponItem = _equipment.GetEquippedItem(BagBack100.Data.Core.EquipType.Weapon);
                if (weaponItem != null && weaponItem.ItemData is BagBack100.Data.Models.WeaponDataSO weaponData)
                {
                    GameObject chargedFx = weaponData.GetChargedEffectPrefab();
                    if (chargedFx != null) effectPrefab = chargedFx;
                }
            }

            int chargedDamage = Mathf.Max(1, Mathf.RoundToInt(_stats.CurrentAttack * _chargeDamageMultiplier));
            // 2칸 직선 범위를 덮도록 오프셋과 스케일 확장
            float lineOffset = _meleeOffset + (_grid.cellSize.x * 0.5f);
            Vector3 lineScale = new Vector3(_meleeScale.x * 1.75f, _meleeScale.y * 1.45f, _meleeScale.z);

            _combat.PerformMultiTargetMeleeAttack(
                _chargedTargetBuffer,
                _stats.CurrentAttackSpeed,
                chargedDamage,
                _facingDirection,
                effectPrefab,
                transform,
                lineOffset,
                lineScale,
                true
            );
        }

        #endregion

        private void HandleSpecialAttack()
        {
            if (CurrentState != PlayerState.Idle || _combat.IsAttackOnCooldown) return;

            _combat.PerformRangedAttack(_projectilePrefab, _projectileSpawnPoint, _stats.CurrentSpeed, _stats.CurrentAttack, _facingDirection);
        }

        private void HandleTileReached(Vector3 pos)
        {
            if (_grid != null)
            {
                Vector3Int currentCell = _grid.WorldToCell(pos);
                currentCell.z = 0; // 2D 그리드에서 Z축 오차 방지

                BagBack100.Core.GlobalEvents.OnPlayerCellChanged?.Invoke(currentCell);

                if (BagBack100.Core.TileSystem.Instance != null)
                {
                    var item = BagBack100.Core.TileSystem.Instance.GetItemAtTile(currentCell);
                    if (item != null)
                    {
                        item.PickUpByPlayer();
                    }
                }
            }
            
            // 타일 안착 시 선입력 커맨드 실행
            if (_bufferedCommand != null)
            {
                var cmd = _bufferedCommand;
                _bufferedCommand = null;
                cmd.Invoke();
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
