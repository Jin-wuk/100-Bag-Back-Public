using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Combat;
using BagBack100.Data.Core;
using BagBack100.Data.Models;
using BagBack100.Monster;

namespace BagBack100.Player
{
    public enum PlayerState
    {
        Idle,
        Moving,
        Attacking,
        Charging,
        Dashing
    }

    public enum BufferedCommandType
    {
        None = 0,
        Move,
        Attack,
        Charge
    }

    /// <summary>
    /// 람다 클로저(Action) 할당으로 인한 GC Spike를 원천 차단하는 값 타입(Value Type) 단일 슬롯 선입력 구조체입니다.
    /// </summary>
    public struct BufferedCommand
    {
        public BufferedCommandType Type;
        public Vector3Int TargetCell;
        public Vector2Int MoveDir;
        public float Timestamp;

        public void SetMove(Vector2Int moveDir, float now)
        {
            Type = BufferedCommandType.Move;
            MoveDir = moveDir;
            TargetCell = Vector3Int.zero;
            Timestamp = now;
        }

        public void SetAttack(Vector3Int targetCell, float now)
        {
            Type = BufferedCommandType.Attack;
            TargetCell = targetCell;
            MoveDir = Vector2Int.zero;
            Timestamp = now;
        }

        public void SetCharge(float now)
        {
            Type = BufferedCommandType.Charge;
            TargetCell = Vector3Int.zero;
            MoveDir = Vector2Int.zero;
            Timestamp = now;
        }

        public void Clear()
        {
            Type = BufferedCommandType.None;
        }

        public bool IsValid(float now, float window)
        {
            return Type != BufferedCommandType.None && (now - Timestamp) <= window;
        }
    }

    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(EntityCombat))]
    [RequireComponent(typeof(PlayerStatManager))]
    /// <summary>
    /// Role: 플레이어 상태(Idle/Moving/Attacking/Charging/Dashing)와 전투 입력 총괄. 선입력 버퍼, N타 콤보(기본 2타), 우클릭 차징(_chargeAttackEnabled 스위치, 기본 꺼짐), 2칸 대시(쿨타임, 공격 후 잠금·걸음·차징 끊기), 일시정지 시 입력·차징 정리.
    /// Dependencies: PlayerInput, PlayerMovement, EntityCombat, PlayerStatManager, EquipmentManager, GamePauseService, TileSystem
    /// Events: GlobalEvents.OnPlayerCellChanged (구독: GamePauseService.OnPauseChanged)
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        [Header("Combat & Combo Settings")]
        [SerializeField] private float _meleeOffset = 0.5f;
        [SerializeField] private Vector3 _meleeScale = Vector3.one;
        [Tooltip("무기 미장착 시 사용할 기본 근접 이펙트 프리팹")]
        [SerializeField] private GameObject _defaultMeleeEffectPrefab;
        [Tooltip("기본 공격 콤보 타수. 2 = 1타·2타 반복(마무리 강타 없음), 3 이상이면 3타가 마무리 강타(1.5배)")]
        [SerializeField, Min(1)] private int _comboHitCount = 2;
        [Tooltip("이동 중에도 콤보가 유지되는 최대 시간(초)")]
        [SerializeField] private float _comboResetTime = 1.5f;
        [Tooltip("기본 공격 판정 칸을 몬스터 길찾기의 위험 칸으로 표시하는 시간(초). 공격 간격(약 0.55초)보다 짧아야 연타 사이에 몬스터가 들어올 틈이 생긴다")]
        [SerializeField] private float _meleeDangerDuration = 0.3f;

        [Header("Micro-Mechanics: Input Buffer Settings")]
        [Tooltip("선입력(Input Buffering)이 유효하게 유지되는 최대 시간(초). 모션 종료/타일 안착 직전 입력만 수용하여 광클 폭주를 방지합니다.")]
        [SerializeField] private float _inputBufferWindow = 0.22f;

        [Header("Right-Click Charge Attack Settings")]
        [Tooltip("우클릭 차징 공격 사용 여부. 꺼 두면 우클릭 입력(선입력 포함)을 무시한다")]
        [SerializeField] private bool _chargeAttackEnabled = false;
        [Tooltip("우클릭 차징 완충까지 걸리는 시간(초) - 완충 전 손을 떼면 무효")]
        [SerializeField] private float _chargeDuration = 0.6f;
        [Tooltip("차징 강공격 전방 직선 타일 사거리")]
        [SerializeField] private int _chargeLineLength = 2;
        [Tooltip("차징 강공격 데미지 배율")]
        [SerializeField] private float _chargeDamageMultiplier = 2.0f;

        [Header("Dash (09 기획안 2절, 수치는 실험 초기값)")]
        [Tooltip("대시 최대 칸 수. 경로 위 몬스터는 넘어가고 벽·상자에서 멈춘다")]
        [SerializeField, Min(1)] private int _dashDistance = 2;
        [Tooltip("최대 칸 수를 이동할 때 걸리는 시간(초). 덜 가면 칸 수에 비례해 줄어든다")]
        [SerializeField, Min(0.01f)] private float _dashDuration = 0.12f;
        [Tooltip("대시 쿨타임(초). 발동 순간 시작, 일시정지 중에는 흐르지 않음")]
        [SerializeField, Min(0f)] private float _dashCooldown = 3f;

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

        // Zero-GC 단일 슬롯 덮어쓰기 선입력 버퍼
        private BufferedCommand _inputBuffer;

        // 3타 콤보 상태
        private int _comboStep = 0; // 0: 1타, 1: 2타, 2: 3타(_comboHitCount ≥ 3일 때)
        private float _lastComboAttackTime = -99f;

        // 우클릭 홀드 차징 및 무기 히트박스 버퍼 (GC Zero를 위해 사전 할당)
        private Coroutine _chargeCoroutine;
        private Vector3 _baseSpriteScale = Vector3.one;
        private bool _isChargeReady = false;
        private readonly List<GameObject> _chargedTargetBuffer = new List<GameObject>(8);
        private readonly List<Vector3Int> _weaponHitboxCellBuffer = new List<Vector3Int>(8);
        private readonly List<GameObject> _meleeTargetBuffer = new List<GameObject>(8);

        // 대시 쿨타임과 이동 잠금 해제 (공격 주기는 그대로 두고 이동 잠금만 푼다)
        private float _dashCooldownRemaining = 0f;
        private bool _moveLockReleased = false;
        private bool IsMoveLocked => _combat.IsAttackOnCooldown && !_moveLockReleased;
        public float DashCooldownNormalized => _dashCooldown > 0f ? Mathf.Clamp01(_dashCooldownRemaining / _dashCooldown) : 0f;

        public float ChargeProgress { get; private set; } = 0f;
        public int ChargeLineLength => _chargeLineLength;
        public Vector3 FacingDirection => _facingDirection;

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<EntityCombat>();
            _stats = GetComponent<PlayerStatManager>();
            _equipment = GetComponent<EquipmentManager>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            // 차징 연출이 스케일을 바꾸므로, 취소 경로에서 되돌릴 원래 스케일을 미리 저장
            _baseSpriteScale = _spriteRenderer != null ? _spriteRenderer.transform.localScale : Vector3.one;
            
            _input.OnMoveInput += HandleMoveInput;
            _input.OnDashInput += HandleDashInput;
            _input.OnAimInput += HandleAimInput;
            _input.OnAttackInput += HandleAttackInput;
            _input.OnChargeStartInput += HandleChargeStartInput;
            _input.OnChargeReleaseInput += HandleChargeReleaseInput;
            _input.OnSpecialAttackKeyPressed += HandleSpecialAttack;
            _movement.OnMovementFinished += HandleMovementFinished;
            _movement.OnTileReached += HandleTileReached;
            _movement.OnDashFinished += HandleDashFinished;
            _combat.OnAttackFinished += HandleAttackFinished;
            BagBack100.Core.GamePauseService.OnPauseChanged += HandlePauseChanged;
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
                _input.OnDashInput -= HandleDashInput;
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
                _movement.OnDashFinished -= HandleDashFinished;
            }
            if (_combat != null)
            {
                _combat.OnAttackFinished -= HandleAttackFinished;
            }
            BagBack100.Core.GamePauseService.OnPauseChanged -= HandlePauseChanged;
                
            if (_grid != null && BagBack100.Core.TileSystem.Instance != null)
            {
                BagBack100.Core.TileSystem.Instance.ReleaseTile(_grid.WorldToCell(transform.position), gameObject);
                // 걷기 예약 칸·대시 착지 칸이 남지 않게 함께 놓는다
                if (_movement != null) BagBack100.Core.TileSystem.Instance.ReleaseTile(_movement.ReservedCell, gameObject);
            }
        }

        private void Update()
        {
            // EntityCombat 공격 주기와 같은 기준: 히트스톱에는 흐르고 가방 일시정지에는 멈춘다
            if (_dashCooldownRemaining > 0f && !BagBack100.Core.GamePauseService.IsPaused)
            {
                _dashCooldownRemaining = Mathf.Max(0f, _dashCooldownRemaining - Time.unscaledDeltaTime);
            }
        }

        private void HandleAttackFinished()
        {
            _moveLockReleased = false;

            if (CurrentState == PlayerState.Attacking)
            {
                CurrentState = PlayerState.Idle;
            }

            // 대시로 잠금을 끊고 걷거나 대시하는 중이면, 칸 사이에서 공격이 나가지 않게 도착·착지 때 소비한다
            if (CurrentState == PlayerState.Moving || CurrentState == PlayerState.Dashing) return;

            // 모션 락(Attack Recovery) 해제 프레임에 유효한 선입력(이동 또는 다음 콤보/차징)이 있다면 즉시 소비
            TryConsumeBufferedCommand();
        }

        private void HandleAimInput(Vector3 aimDir)
        {
            if (CurrentState == PlayerState.Attacking) return;
            SetFacingSnapped8(aimDir);
        }

        private void SetFacingSnapped8(Vector3 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            float rad = snappedAngle * Mathf.Deg2Rad;
            _facingDirection = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0).normalized;
        }

        private void HandleMoveInput(Vector2Int moveDir)
        {
            // 우클릭 홀드 차징 중에는 이동 불가
            if (CurrentState == PlayerState.Charging) return;

            // 공격 모션 락(Motion Lock) 중에는 현재 공격이 절대 취소되지 않으며, 모션 종료 직후 실행될 단일 선입력 슬롯을 '이동'으로 덮어씀
            if (CurrentState == PlayerState.Attacking || CurrentState == PlayerState.Dashing || IsMoveLocked)
            {
                _inputBuffer.SetMove(moveDir, Time.unscaledTime);
                return;
            }
            
            // 타일 이동 중 입력 시: 현재 타일까지는 안전하게 이동하되 도착 즉시 실행할 다음 행동을 '이동'으로 예약
            if (CurrentState == PlayerState.Moving)
            {
                _inputBuffer.SetMove(moveDir, Time.unscaledTime);
                return;
            }

            ExecuteStepMove(moveDir);
        }

        private void ExecuteStepMove(Vector2Int moveDir)
        {
            if (_grid == null || CurrentState == PlayerState.Attacking || CurrentState == PlayerState.Charging || CurrentState == PlayerState.Dashing || IsMoveLocked) return;

            Vector3Int currentCellPos = _grid.WorldToCell(transform.position);
            Vector3Int targetCellPos = currentCellPos + new Vector3Int(moveDir.x, moveDir.y, 0);
            Vector3 targetPos = _grid.GetCellCenterWorld(targetCellPos);

            CurrentState = PlayerState.Moving;
            _movement.MoveTo(targetPos);
        }

        #region Left-Click: 3-Hit Combo Attack & Weapon Grid Hitbox

        private void HandleAttackInput(Vector3Int targetCell)
        {
            if (CurrentState == PlayerState.Charging) return;

            // 이동 중이거나 공격 모션 락 중일 때: 단일 슬롯 선입력 버퍼에 최신 공격 좌표를 덮어쓰기 (광클 누적 방지)
            if (CurrentState == PlayerState.Moving || CurrentState == PlayerState.Attacking || CurrentState == PlayerState.Dashing || _combat.IsAttackOnCooldown)
            {
                _inputBuffer.SetAttack(targetCell, Time.unscaledTime);
                return;
            }

            ExecuteMeleeComboAttack(targetCell);
        }

        private void ExecuteMeleeComboAttack(Vector3Int targetCellPos)
        {
            if (_grid == null || _combat.IsAttackOnCooldown) return;

            // 타일 이동 후 선입력으로 발동된 경우, 도착한 현재 타일 기준으로 사거리 재검증 및 보정
            Vector3Int playerCell = _grid.WorldToCell(transform.position);
            playerCell.z = 0;

            // 선입력 공격은 공격 종료 프레임에 바로 이어져 조준 입력(Attacking 중 무시)이 _facingDirection을 갱신할 틈이 없다.
            // 애니·이펙트가 이전 방향을 보지 않도록, 사거리 보정보다 먼저 클릭한 칸 방향으로 바라보는 방향을 맞춘다
            Vector3Int toTarget = targetCellPos - playerCell;
            if (toTarget.x != 0 || toTarget.y != 0)
            {
                SetFacingSnapped8(new Vector3(toTarget.x, toTarget.y, 0f));
            }

            int chebyshevDist = Mathf.Max(Mathf.Abs(playerCell.x - targetCellPos.x), Mathf.Abs(playerCell.y - targetCellPos.y));
            if (chebyshevDist == 0 || chebyshevDist > _stats.CurrentRange)
            {
                int stepX = Mathf.RoundToInt(_facingDirection.x);
                int stepY = Mathf.RoundToInt(_facingDirection.y);
                if (stepX == 0 && stepY == 0) stepY = -1;
                targetCellPos = new Vector3Int(playerCell.x + stepX, playerCell.y + stepY, 0);
            }

            // 콤보 유지 시간 체크 (이동했더라도 _comboResetTime 이내면 다음 타수로 진행)
            if (Time.time - _lastComboAttackTime > _comboResetTime)
            {
                _comboStep = 0;
            }

            // 플레이 중 인스펙터에서 타수를 줄여도 범위를 벗어나지 않게 매번 나머지를 취한다
            int hitCount = Mathf.Max(1, _comboHitCount);
            int currentCombo = _comboStep % hitCount;
            _comboStep = (currentCombo + 1) % hitCount;
            _lastComboAttackTime = Time.time;

            CurrentState = PlayerState.Attacking;

            // 1. 장착 무기 데이터(SO) 조회 및 히트박스/피드백 배율 추출
            GameObject effectPrefab = _defaultMeleeEffectPrefab;
            WeaponDataSO equippedWeapon = null;
            float shakeMult = 1.0f;
            float hitStopMult = 1.0f;

            if (_equipment != null)
            {
                var weaponItem = _equipment.GetEquippedItem(EquipType.Weapon);
                if (weaponItem != null && weaponItem.ItemData is WeaponDataSO weaponData)
                {
                    equippedWeapon = weaponData;
                    shakeMult = weaponData.shakeMultiplier;
                    hitStopMult = weaponData.hitStopMultiplier;
                    GameObject weaponEffect = weaponData.GetComboEffectPrefab(currentCombo);
                    if (weaponEffect != null) effectPrefab = weaponEffect;
                }
            }

            // 2. 무기별 그리드 히트박스(SingleTile / ForwardLine2 / WideSweep3) 산출 및 타겟 수집
            _weaponHitboxCellBuffer.Clear();
            if (equippedWeapon != null)
            {
                equippedWeapon.PopulateHitboxCells(playerCell, targetCellPos, _facingDirection, _weaponHitboxCellBuffer);
            }
            else
            {
                _weaponHitboxCellBuffer.Add(targetCellPos);
            }

            _meleeTargetBuffer.Clear();
            if (BagBack100.Core.TileSystem.Instance != null)
            {
                for (int i = 0; i < _weaponHitboxCellBuffer.Count; i++)
                {
                    Vector3Int cell = _weaponHitboxCellBuffer[i];
                    BagBack100.Core.TileSystem.Instance.SetDanger(cell, _meleeDangerDuration);
                    GameObject occupant = BagBack100.Core.TileSystem.Instance.GetEntityAtTile(cell);
                    if (occupant != null && occupant != gameObject && !_meleeTargetBuffer.Contains(occupant))
                    {
                        _meleeTargetBuffer.Add(occupant);
                    }
                }
            }

            // 타수별 데미지 배율 및 스케일 변형 (1타: 정방향 1.0x / 2타: Y축 반전 1.15x / 3타: 확대 강타 1.5x)
            float dmgMultiplier = 1.0f;
            Vector3 comboScale = _meleeScale;
            bool isFinisher = (currentCombo == 2);

            if (currentCombo == 1)
            {
                dmgMultiplier = 1.15f;
                bool hasDedicatedEffect = equippedWeapon != null && equippedWeapon.HasComboEffectPrefab(currentCombo);
                float flipY = hasDedicatedEffect ? 1f : -1f;
                comboScale = new Vector3(_meleeScale.x * 1.08f, flipY * _meleeScale.y * 1.08f, _meleeScale.z);
            }
            else if (currentCombo == 2)
            {
                dmgMultiplier = 1.5f;
                comboScale = _meleeScale * 1.35f;
            }

            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_stats.CurrentAttack * dmgMultiplier));

            if (_weaponHitboxCellBuffer.Count > 1)
            {
                _combat.PerformMultiTargetMeleeAttack(
                    _meleeTargetBuffer,
                    _stats.CurrentAttackSpeed,
                    finalDamage,
                    _facingDirection,
                    effectPrefab,
                    transform,
                    _meleeOffset,
                    comboScale,
                    isFinisher,
                    shakeMult,
                    hitStopMult
                );
            }
            else
            {
                GameObject singleTarget = _meleeTargetBuffer.Count > 0 ? _meleeTargetBuffer[0] : null;
                _combat.PerformMeleeAttack(
                    singleTarget,
                    _stats.CurrentAttackSpeed,
                    finalDamage,
                    _facingDirection,
                    effectPrefab,
                    transform,
                    _meleeOffset,
                    comboScale,
                    isFinisher,
                    shakeMult,
                    hitStopMult
                );
            }
        }

        #endregion

        #region Space: Dash

        private void HandleDashInput(Vector2Int dir)
        {
            if (_grid == null || dir == Vector2Int.zero) return;
            if (BagBack100.Core.GamePauseService.IsPaused) return;
            if (CurrentState == PlayerState.Dashing || _dashCooldownRemaining > 0f) return;
            if (_stats != null && _stats.IsDead) return;

            var tiles = BagBack100.Core.TileSystem.Instance;

            // 시작 칸은 몬스터 피격 판정과 같은 기준(현재 위치가 속한 칸). 걷기 출발 칸을 그새 몬스터가 차지했으면 예약한 목적지 칸
            Vector3Int startCell = _grid.WorldToCell(transform.position);
            startCell.z = 0;
            if (_movement.IsMoving && tiles != null)
            {
                GameObject occupant = tiles.GetEntityAtTile(startCell);
                if (occupant != null && occupant != gameObject) startCell = _movement.ReservedCell;
            }

            // 착지 칸이 없으면 발동하지 않고 쿨타임도 쓰지 않는다
            if (!TryFindDashLanding(startCell, dir, out Vector3Int landingCell, out int steps)) return;

            if (CurrentState == PlayerState.Charging)
            {
                if (_chargeCoroutine != null)
                {
                    StopCoroutine(_chargeCoroutine);
                    _chargeCoroutine = null;
                }
                _isChargeReady = false;
                CancelChargeVisuals(Color.white, _baseSpriteScale);
            }

            if (_combat.IsAttackOnCooldown) _moveLockReleased = true;

            CurrentState = PlayerState.Dashing;
            _dashCooldownRemaining = _dashCooldown;
            _movement.Dash(startCell, landingCell, _dashDuration * steps / Mathf.Max(1, _dashDistance));
        }

        /// <summary>
        /// 시작 칸에서 dir로 최대 _dashDistance칸을 본다. 벽·바닥 없음·상자에서 멈추고, 다른 엔티티가 있는 칸은 지나가되 착지 후보가 아니다.
        /// 가장 먼 빈 칸(또는 자신이 점유한 칸)이 착지 칸이다. 기획 09 2절 착지 표와 같은 결과.
        /// </summary>
        private bool TryFindDashLanding(Vector3Int startCell, Vector2Int dir, out Vector3Int landingCell, out int steps)
        {
            landingCell = startCell;
            steps = 0;
            var tiles = BagBack100.Core.TileSystem.Instance;

            for (int i = 1; i <= _dashDistance; i++)
            {
                Vector3Int cell = new Vector3Int(startCell.x + dir.x * i, startCell.y + dir.y * i, 0);
                if (BagBack100.Core.Pathfinder.IsObstacle(_grid, new Vector2Int(cell.x, cell.y), _movement.ObstacleLayerMask))
                {
                    break;
                }

                GameObject occupant = tiles != null ? tiles.GetEntityAtTile(cell) : null;
                if (occupant != null && occupant != gameObject) continue;

                landingCell = cell;
                steps = i;
            }
            return steps > 0;
        }

        private void HandleDashFinished()
        {
            if (CurrentState == PlayerState.Dashing)
            {
                CurrentState = PlayerState.Idle;
            }
            TryConsumeBufferedCommand();
        }

        #endregion

        #region Right-Click: Hold Charge 2-Tile Line Attack

        private void HandleChargeStartInput()
        {
            if (!_chargeAttackEnabled) return;
            if (CurrentState == PlayerState.Charging) return;

            if (CurrentState == PlayerState.Moving || CurrentState == PlayerState.Attacking || CurrentState == PlayerState.Dashing || _combat.IsAttackOnCooldown)
            {
                _inputBuffer.SetCharge(Time.unscaledTime);
                return;
            }

            StartCharging();
        }

        private void StartCharging()
        {
            if (_combat.IsAttackOnCooldown) return;

            CurrentState = PlayerState.Charging;
            _isChargeReady = false;
            ChargeProgress = 0f;

            if (_chargeCoroutine != null) StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = StartCoroutine(ChargeHoldRoutine());
        }

        private IEnumerator ChargeHoldRoutine()
        {
            float elapsed = 0f;
            Color origColor = Color.white;
            Vector3 origScale = _spriteRenderer != null ? _spriteRenderer.transform.localScale : Vector3.one;

            int lastUnlockedTile = 1;

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
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, _chargeDuration));
                ChargeProgress = t;

                // 중간 단계 타일이 해금될 때마다 가벼운 플래시 피드백
                int currentUnlockedTile = 1 + Mathf.FloorToInt(t * Mathf.Max(0, _chargeLineLength - 1));
                if (currentUnlockedTile > lastUnlockedTile && currentUnlockedTile < _chargeLineLength)
                {
                    lastUnlockedTile = currentUnlockedTile;
                    CombatFeedbackSystem.EnsureInstance().SpawnProceduralHitFlash(transform.position, 0.75f, new Color(1f, 0.9f, 0.4f, 0.6f), 0.12f);
                }

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
            ChargeProgress = 1f;
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

            // 완충 상태에서 우클릭을 떼면 N칸 직선 강공격 발동
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
                CancelChargeVisuals(Color.white, _baseSpriteScale);
                CurrentState = PlayerState.Idle;
            }
        }

        /// <summary>
        /// 일시정지 진입 시 선입력을 비우고 차징을 취소한다.
        /// 차징 루틴은 우클릭 해제를 감지해 강공격을 쏘므로, 가방을 열고 마우스를 떼면 멈춘 화면에서 공격이 발동하는 것을 막는다.
        /// </summary>
        private void HandlePauseChanged(bool isPaused)
        {
            if (!isPaused) return;

            _inputBuffer.Clear();

            if (CurrentState != PlayerState.Charging) return;

            if (_chargeCoroutine != null)
            {
                StopCoroutine(_chargeCoroutine);
                _chargeCoroutine = null;
            }
            _isChargeReady = false;
            CancelChargeVisuals(Color.white, _baseSpriteScale);
            CurrentState = PlayerState.Idle;
        }

        /// <summary>
        /// 층 전환 순간이동. 이동 코루틴·차징·선입력을 모두 끊고 Idle로 되돌린 뒤 새 칸에 세운다.
        /// HP·MP·쿨타임·HoT는 컴포넌트 상태 그대로 이어진다(기획 Q2).
        /// </summary>
        public void ResetForFloorTransition(Vector3 worldPos, Vector3Int cell)
        {
            if (_movement != null) _movement.StopImmediately();

            if (_chargeCoroutine != null)
            {
                StopCoroutine(_chargeCoroutine);
                _chargeCoroutine = null;
            }
            _isChargeReady = false;
            CancelChargeVisuals(Color.white, _baseSpriteScale);
            _inputBuffer.Clear();
            _moveLockReleased = false;
            CurrentState = PlayerState.Idle;

            transform.position = worldPos;
            cell.z = 0;
            if (BagBack100.Core.TileSystem.Instance != null)
            {
                BagBack100.Core.TileSystem.Instance.RegisterInitialTile(cell, gameObject);
            }
            BagBack100.Core.GlobalEvents.OnPlayerCellChanged?.Invoke(cell);

            if (Camera.main != null && Camera.main.TryGetComponent<BagBack100.Core.CameraFollow>(out var cameraFollow))
            {
                cameraFollow.SnapToTarget();
            }
        }

        private void CancelChargeVisuals(Color origColor, Vector3 origScale)
        {
            ChargeProgress = 0f;
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
            ChargeProgress = 0f;
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
            float shakeMult = 1.0f;
            float hitStopMult = 1.0f;

            if (_equipment != null)
            {
                var weaponItem = _equipment.GetEquippedItem(EquipType.Weapon);
                if (weaponItem != null && weaponItem.ItemData is WeaponDataSO weaponData)
                {
                    shakeMult = weaponData.shakeMultiplier;
                    hitStopMult = weaponData.hitStopMultiplier;
                    GameObject chargedFx = weaponData.GetChargedEffectPrefab();
                    if (chargedFx != null) effectPrefab = chargedFx;
                }
            }

            int chargedDamage = Mathf.Max(1, Mathf.RoundToInt(_stats.CurrentAttack * _chargeDamageMultiplier));
            // N칸 직선 범위를 덮도록 오프셋과 스케일 확장
            int extraTiles = Mathf.Max(0, _chargeLineLength - 1);
            float lineOffset = _meleeOffset + (_grid.cellSize.x * 0.5f * extraTiles);
            Vector3 lineScale = new Vector3(_meleeScale.x * (1f + 0.75f * extraTiles), _meleeScale.y * 1.45f, _meleeScale.z);

            _combat.PerformMultiTargetMeleeAttack(
                _chargedTargetBuffer,
                _stats.CurrentAttackSpeed,
                chargedDamage,
                _facingDirection,
                effectPrefab,
                transform,
                lineOffset,
                lineScale,
                true,
                shakeMult,
                hitStopMult
            );
        }

        #endregion

        private void HandleSpecialAttack()
        {
            if (CurrentState != PlayerState.Idle || _combat.IsAttackOnCooldown) return;

            _combat.PerformRangedAttack(_projectilePrefab, _projectileSpawnPoint, _stats.CurrentAttackSpeed, _stats.CurrentAttack, _facingDirection);
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

            // 대시 착지는 HandleDashFinished에서 상태를 되돌린 뒤 소비한다
            if (CurrentState == PlayerState.Dashing) return;
            
            // 타일 안착 시 유효 시간(Window) 내의 선입력 커맨드가 있다면 즉시 소비
            TryConsumeBufferedCommand();
        }

        private void HandleMovementFinished()
        {
            if (CurrentState == PlayerState.Moving)
            {
                CurrentState = PlayerState.Idle;
            }

            TryConsumeBufferedCommand();
        }

        /// <summary>
        /// 타일 안착 프레임 또는 공격 모션 락 해제 프레임에 호출되어,
        /// 유효 시간(_inputBufferWindow) 내에 입력된 마지막 1개의 커맨드를 GC 할당 없이 즉시 발동합니다.
        /// </summary>
        private void TryConsumeBufferedCommand()
        {
            // 가방이 열려 일시정지된 동안 선입력이 발동하면 멈춘 화면에서 공격·이동이 나가므로 버린다
            if (BagBack100.Core.GamePauseService.IsPaused)
            {
                _inputBuffer.Clear();
                return;
            }

            if (!_inputBuffer.IsValid(Time.unscaledTime, _inputBufferWindow))
            {
                _inputBuffer.Clear();
                return;
            }

            // 대시로 이동 잠금만 끊은 경우 공격 주기가 남아 있다. 공격·차징은 버리지 않고 주기 종료(HandleAttackFinished) 때 소비한다
            if (_combat.IsAttackOnCooldown &&
                (_inputBuffer.Type == BufferedCommandType.Attack || _inputBuffer.Type == BufferedCommandType.Charge))
            {
                return;
            }

            BufferedCommand cmd = _inputBuffer;
            _inputBuffer.Clear();

            switch (cmd.Type)
            {
                case BufferedCommandType.Attack:
                    if (_movement.IsMoving) _movement.CancelPath();
                    ExecuteMeleeComboAttack(cmd.TargetCell);
                    break;

                case BufferedCommandType.Move:
                    ExecuteStepMove(cmd.MoveDir);
                    break;

                case BufferedCommandType.Charge:
                    if (Input.GetMouseButton(1))
                    {
                        if (_movement.IsMoving) _movement.CancelPath();
                        StartCharging();
                    }
                    break;
            }
        }
    }
}
