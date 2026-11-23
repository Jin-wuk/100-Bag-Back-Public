using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Combat;
using BagBack100.Core;
using BagBack100.Data.Core;
using BagBack100.Data.Models;
using BagBack100.Dungeon;
using BagBack100.Player;

namespace BagBack100.Monster
{
    public enum MonsterAIState
    {
        Patrol,     // 비인식 상태: 주변 4방향 타일 랜덤 배회 및 대기
        Aggro,      // 전투/추적 상태: 이동 스타일 포지셔닝 + 가중치 랜덤 패턴 시전
        Evading,    // 회피 인터럽트 상태: 능동 스텝 회피 또는 피격 후방 이탈 중
        Stunned,    // 기절 인터럽트 상태: 돌진 중 벽 충돌 등으로 행동 불능
        Dead        // 사망 상태
    }

    /// <summary>
    /// Role: 몬스터 1마리의 생명주기, 행동 루프(어그로 판정 → 가중치 랜덤 패턴 시전 → 이동), 반응형 회피/기절 인터럽트, 피격·사망 처리.
    /// Dependencies: MonsterDataSO, MonsterPatternSO, MonsterLocomotion, EntityCombat, TileSystem, FogOfWarSystem, CombatFeedbackSystem, PlayerController
    /// Events: GlobalEvents.OnEntityDamaged, GlobalEvents.OnMonsterDied (지역 이벤트: OnDamaged, OnAttackWindup, static OnSpawned / OnRemoved)
    /// </summary>
    [RequireComponent(typeof(EntityCombat))]
    public class MonsterController : MonoBehaviour
    {
        [Header("데이터 및 참조")]
        [SerializeField] private MonsterDataSO _monsterData;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [SerializeField] private AttackWarningUI _warningPrefab;
        [Tooltip("원거리 패턴 SO에 탄환 프리팹이 비어 있을 때 사용할 기본 투사체 프리팹")]
        [SerializeField] private GameObject _defaultProjectilePrefab;

        public MonsterDataSO Data => _monsterData;
        public int CurrentHp { get; private set; }
        public int CurrentShield { get; private set; }
        public int InstanceID => gameObject.GetInstanceID();
        public MonsterAIState CurrentState { get; private set; } = MonsterAIState.Patrol;
        public bool IsAggroed => _isAggroed;
        public bool IsMovingToTile => _isMovingToTile;
        // 지금 건너는 칸의 시작→도착 방향(z=0). 보간 중 위치에는 홉 높이가 섞이므로 따로 기록한다
        public Vector3 CurrentMoveDirection { get; private set; }
        // 패턴(공격)을 시작할 때 플레이어 쪽 방향과 횟수를 기록한다. 외형이 "마지막 행동 방향"을 잡는 기준
        public Vector3 LastActionDirection { get; private set; }
        public int ActionCount { get; private set; }
        // 지금 실행 중인 패턴(폴백 근접·패턴 밖이면 null). 외형이 패턴별 공격 애니를 고르는 기준
        public MonsterPatternSO CurrentPattern { get; private set; }

        public EntityCombat Combat { get; private set; }
        public Grid Grid { get; private set; }
        public PlayerController Player { get; private set; }
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;
        public AttackWarningUI WarningPrefab => _warningPrefab;
        public GameObject DefaultProjectilePrefab => _defaultProjectilePrefab;

        // Stateless PatternSO(플라이웨이트 패턴)의 동시성 오염 및 GC 할당을 완벽 방지하기 위한 인스턴스별 전용 버퍼
        // 5×5 몸 둘레 반경 3 범위(11×11 - 25 = 96칸)까지 재할당 없이 담는다
        public List<Vector3Int> PatternCellsBuffer { get; } = new List<Vector3Int>(128);
        // 돌진처럼 예고 칸과 별도로 이동 경로(몸 좌하단 칸)를 들고 있어야 하는 패턴용
        public List<Vector3Int> PatternAnchorsBuffer { get; } = new List<Vector3Int>(16);
        public List<Vector3> PatternDirectionsBuffer { get; } = new List<Vector3>(8);

        public event Action OnDamaged;
        // 패턴이 예고(telegraph)를 시작했다. 인자 = 예고 시간(초). 외형이 공격 애니를 예고 시간에 맞춰 늘리는 기준
        public event Action<float> OnAttackWindup;

        // 층 클리어 등록부(FloorClearTracker)용 지역 이벤트. 사망 전리품 이벤트(OnMonsterDied)와 분리해
        // 소환수·강제 제거·층 정리 파괴까지 "살아 있는 몬스터 수"를 정확히 맞춘다.
        // OnSpawned는 Awake에서 발행되므로 구독자는 이 시점에 Data·IsSummon을 읽으면 안 된다(아직 주입 전).
        public static event Action<MonsterController> OnSpawned;
        public static event Action<MonsterController> OnRemoved;

        public bool IsSummon { get; private set; }
        public bool IsBoss => _monsterData != null && _monsterData.type == MonsterType.Boss;

        // 층 배율을 적용한 실제 HP 상한·공격력. 방어력은 배율을 받지 않는다 (기획 07 F4)
        private float _hpMultiplier = 1f;
        private float _atkMultiplier = 1f;
        public int MaxHp => _monsterData == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(_monsterData.hp * _hpMultiplier));
        public int Attack => _monsterData == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(_monsterData.atk * _atkMultiplier));

        // 런타임 상태 플래그
        public bool IsExecutingPattern => _isExecutingPattern;
        public bool IsInRecoveryWindow => _isInRecoveryWindow;

        private bool _isAggroed;
        private float _aggroExpireTime;
        private const float AggroHoldDuration = 6.0f;
        private bool _isExecutingPattern;
        private bool _isInRecoveryWindow;
        private bool _isStunned;
        private bool _isEvading;
        private bool _isDead;
        private bool _isRemovedRaised;

        // 쿨타임 타이머 (GC Zero를 위해 사전 할당)
        private float _nextGlobalPatternTime;
        private float _nextEvasiveStepTime;
        private float _nextEscapeOnHitTime;
        private readonly Dictionary<MonsterPatternSO, float> _patternCooldownExpirations = new Dictionary<MonsterPatternSO, float>();
        private readonly List<MonsterPatternSO> _candidatePatterns = new List<MonsterPatternSO>(8);

        // 코루틴 핸들
        private Coroutine _behaviorCoroutine;
        private Coroutine _interruptCoroutine;
        private Coroutine _hitReactionCoroutine;

        // 행동 체인(패턴/이동/회피)에서 파생된 하위 코루틴 핸들.
        // 부모 코루틴을 StopCoroutine 해도 StartCoroutine으로 시작한 자식은 멈추지 않으므로, 인터럽트·사망 시 여기서 일괄 중단한다.
        private readonly List<Coroutine> _actionChildCoroutines = new List<Coroutine>(4);

        // 타일 이동 중 인터럽트 위치 정합성 플래그
        private bool _isMovingToTile;
        private Vector3 _currentDestinationWorldPos;
        // 지금 TileSystem에 점유를 걸어 둔 몸 좌하단 칸
        private Vector3Int _occupiedAnchor;
        private bool _hasOccupiedAnchor;
        // 여러 칸 몸이 이동하는 동안 아직 풀지 않은 옛 몸 좌하단 칸 (도착·중단·제거 때 뒷줄 해제)
        private Vector3Int _pendingReleaseAnchor;
        private bool _hasPendingRelease;
        private bool _isBodyRegistered;

        /// <summary>점유 칸이 바뀔 때(등록·이동 시작·뒷줄 해제·제거) 발행. 발밑 표시(View) 갱신용 지역 이벤트</summary>
        public event Action OnBodyCellsChanged;
        /// <summary>안개 표시 상태가 바뀔 때 발행. 발밑 표시가 몬스터와 함께 숨도록</summary>
        public event Action<bool> OnFogVisibilityChanged;
        public bool IsVisibleByFog { get; private set; } = true;
        public bool IsStationary => _monsterData != null && _monsterData.isStationary;
        private static readonly WaitForSeconds StationaryIdleWait = new WaitForSeconds(0.1f);

        // 비주얼 & 경고 UI 풀링 관리
        private SpriteRenderer[] _renderers;
        private EntityCombat _playerCombat;
        private readonly List<AttackWarningUI> _activeWarnings = new List<AttackWarningUI>(16);
        private readonly List<LineRenderer> _aimLinePool = new List<LineRenderer>(4);
        private static Material _sharedAimLineMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnSpawned = null;
            OnRemoved = null;
        }

        private void Awake()
        {
            Combat = GetComponent<EntityCombat>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            FitColliderToFootprint();
            OnSpawned?.Invoke(this);
        }

        /// <summary>
        /// 소환 패턴이 소환체를 만든 직후 호출한다. 소환수는 전리품을 떨어뜨리지 않는다.
        /// 사망 시 소환체를 낳는 패턴은 소환체 생성(OnSpawned)을 소환자 제거(OnRemoved)보다 먼저 해야
        /// 등록부가 잠깐 비어 층 클리어가 잘못 뜨지 않는다.
        /// </summary>
        public void MarkAsSummon()
        {
            IsSummon = true;
        }

        public void Initialize(MonsterDataSO data)
        {
            _monsterData = data;
            FitColliderToFootprint();
            InitializeStatsFromData();
        }

        /// <summary>층 생성 직후 DungeonGenerator가 호출한다. Start의 초기화도 같은 배율을 쓰므로 호출 순서와 무관하다.</summary>
        public void ApplyFloorScaling(float hpMultiplier, float atkMultiplier)
        {
            _hpMultiplier = hpMultiplier;
            _atkMultiplier = atkMultiplier;
            InitializeStatsFromData();
        }

        private void InitializeStatsFromData()
        {
            if (_monsterData == null) return;
            CurrentHp = MaxHp;
            CurrentShield = _monsterData.HasTrait("SHIELD") ? Mathf.Max(1, MaxHp / 2) : 0;
        }

        private void Start()
        {
            Grid = FindObjectOfType<Grid>();
            Player = FindObjectOfType<PlayerController>();

            InitializeStatsFromData();

            if (Grid != null && TileSystem.Instance != null)
            {
                RegisterBody();
            }

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RegisterMonster(this);
            }

            SubscribePlayerEvents();

            _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
        }

        private void OnDestroy()
        {
            // 층 정리로 파괴될 때의 안전망 (사망·강제 제거에서 이미 발행했으면 무시된다).
            // 씬이 내려가며(탈출·귀환석·사망으로 기지 이동, 플레이 정지) 파괴될 때는 scene.isLoaded가 false다.
            // 이때 발행하면 파괴 순서에 따라 마지막 몬스터가 층 클리어로 판정돼 탈출 포탈이 다음 씬에 남으므로 발행하지 않는다.
            if (gameObject.scene.isLoaded) RaiseRemoved();
            UnsubscribePlayerEvents();
            ClearActiveWarnings();
            HideAimLines();

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UnregisterMonster(this);
            }

            ReleaseBody();
        }

        private void SubscribePlayerEvents()
        {
            if (Player == null) return;
            _playerCombat = Player.GetComponent<EntityCombat>();
            if (_playerCombat != null)
            {
                _playerCombat.OnMeleeAttack += HandlePlayerAttackDetected;
                _playerCombat.OnRangedAttack += HandlePlayerAttackDetected;
            }
        }

        private void UnsubscribePlayerEvents()
        {
            if (_playerCombat != null)
            {
                _playerCombat.OnMeleeAttack -= HandlePlayerAttackDetected;
                _playerCombat.OnRangedAttack -= HandlePlayerAttackDetected;
                _playerCombat = null;
            }
        }

        // =====================================================================
        // 메인 AI 루프: [FSM 어그로 전이] + [이동 템포] + [가중치 랜덤 패턴 시전]의 독립적 분리
        // =====================================================================
        private IEnumerator BehaviorRoutine()
        {
            // 스폰 직후 모든 몬스터가 동시에 패턴을 쓰지 않도록 약간의 초기 지터 부여
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.35f));

            while (!_isDead)
            {
                // 이전 반복의 하위 코루틴은 모두 yield로 완료를 기다렸으므로 종료된 핸들만 남아 있음
                _actionChildCoroutines.Clear();

                if (_isStunned || _isEvading)
                {
                    yield return null;
                    continue;
                }

                if (_monsterData == null || Grid == null)
                {
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }

                int gridDist = Player != null
                    ? GetBodyChebyshevDistance(GetPlayerCell())
                    : 999;

                bool wasAggroed = _isAggroed;
                bool isCurrentlyAggroed = EvaluateAggroState(gridDist);

                // 비인식(Patrol) -> 어그로(Aggro) 최초 진입 시 '!' 팝업과 함께 0.25초 인지 반응 딜레이 부여
                if (!wasAggroed && isCurrentlyAggroed)
                {
                    yield return new WaitForSeconds(0.25f);
                    if (_isDead || _isStunned || _isEvading) continue;
                    gridDist = Player != null
                        ? GetBodyChebyshevDistance(GetPlayerCell())
                        : 999;
                }

                bool patternTriggered = false;

                // 1. 어그로 상태이며 글로벌 패턴 쿨타임이 준비되었을 때 가중치 랜덤 패턴 추첨 시도
                if (isCurrentlyAggroed && Player != null && Time.time >= _nextGlobalPatternTime)
                {
                    if (gridDist <= _monsterData.zocRadius + 1)
                    {
                        if (TrySelectWeightedPattern(gridDist, out MonsterPatternSO selectedPattern))
                        {
                            patternTriggered = true;
                            _isExecutingPattern = true;

                            // 패턴이 인터럽트(돌진 벽 충돌 기절 등)로 끊겨 아래 등록 코드가 실행되지 않아도 쿨타임이 적용되도록 시전 시점에 먼저 등록
                            _patternCooldownExpirations[selectedPattern] = Time.time + selectedPattern.patternCooldown;
                            _nextGlobalPatternTime = Time.time + _monsterData.globalPatternCooldown;

                            CurrentPattern = selectedPattern;
                            MarkActionTowardPlayer();
                            yield return StartActionCoroutine(selectedPattern.ExecuteRoutine(this));

                            _isExecutingPattern = false;
                            CurrentPattern = null;
                            HideAimLines();

                            // 개별 패턴 쿨타임 및 글로벌 행동 쿨타임 등록
                            _patternCooldownExpirations[selectedPattern] = Time.time + selectedPattern.patternCooldown;
                            _nextGlobalPatternTime = Time.time + _monsterData.globalPatternCooldown;

                            // 공정성 보장: 패턴 시전 직후 후딜레이(빈틈/딜타임, Recovery Window) 동안 이동 및 회피 불가 고정
                            if (selectedPattern.recoveryDuration > 0f && !_isStunned && !_isEvading)
                            {
                                _isInRecoveryWindow = true;
                                yield return new WaitForSeconds(selectedPattern.recoveryDuration);
                                _isInRecoveryWindow = false;
                            }
                        }
                        else if ((_monsterData.patterns == null || _monsterData.patterns.Count == 0) && gridDist <= 1)
                        {
                            // 패턴 SO가 하나도 등록되지 않은 에셋을 위한 안전 폴백 기본 근접 공격
                            patternTriggered = true;
                            _isExecutingPattern = true;
                            CurrentPattern = null;
                            MarkActionTowardPlayer();
                            yield return StartActionCoroutine(ExecuteFallbackMeleeRoutine());
                            _isExecutingPattern = false;
                            _nextGlobalPatternTime = Time.time + _monsterData.globalPatternCooldown;
                            _isInRecoveryWindow = true;
                            yield return new WaitForSeconds(0.5f);
                            _isInRecoveryWindow = false;
                        }
                    }
                }

                if (patternTriggered)
                {
                    continue;
                }

                // 2. 다음 행동 패턴 쿨타임이 돌고 있거나 사거리 조건이 맞지 않으면 멈추지 않고 이동(배회 또는 전투 포지셔닝) 수행!
                // 고정형은 이동 대신 짧게 쉬며 다음 패턴 기회를 본다
                if (IsStationary)
                {
                    yield return StationaryIdleWait;
                    continue;
                }
                yield return StartActionCoroutine(MonsterLocomotion.ExecuteMoveStepRoutine(this));

                // 3. 1칸 이동 후 다음 걸음까지의 이동 인터벌 대기 (비인식 배회 중에는 1.4배 여유롭게 이동)
                float waitInterval = isCurrentlyAggroed
                    ? _monsterData.GetEffectiveMoveInterval()
                    : _monsterData.GetEffectiveMoveInterval() * 1.4f;
                yield return new WaitForSeconds(waitInterval);
            }
        }

        /// <summary>
        /// 플레이어와의 거리 및 시야(LOS)를 바탕으로 Patrol <-> Aggro 상태 전이를 판정하고 시각 피드백(!, ?)을 표시합니다.
        /// </summary>
        private bool EvaluateAggroState(int gridDistToPlayer)
        {
            if (Player == null || _monsterData == null)
            {
                _isAggroed = false;
                CurrentState = MonsterAIState.Patrol;
                return false;
            }

            bool inZoc = gridDistToPlayer <= _monsterData.zocRadius;
            bool canSensePlayer = inZoc && (gridDistToPlayer <= 2 || HasLineOfSightToPlayer());

            if (canSensePlayer)
            {
                _aggroExpireTime = Time.time + AggroHoldDuration;
                if (!_isAggroed)
                {
                    _isAggroed = true;
                    CurrentState = MonsterAIState.Aggro;
                    CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                        transform.position, "!", new Color(1f, 0.22f, 0.2f, 1f), 5.6f);
                }
            }
            else if (_isAggroed)
            {
                int leashDist = _monsterData.zocRadius + 4;
                if (Time.time >= _aggroExpireTime || gridDistToPlayer > leashDist)
                {
                    _isAggroed = false;
                    CurrentState = MonsterAIState.Patrol;
                    CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                        transform.position, "?", new Color(1f, 0.9f, 0.25f, 1f), 5.0f);
                }
            }

            if (!_isStunned && !_isEvading && !_isDead)
            {
                CurrentState = _isAggroed ? MonsterAIState.Aggro : MonsterAIState.Patrol;
            }

            return _isAggroed;
        }

        public void ForceAggro()
        {
            if (_isDead) return;
            _aggroExpireTime = Time.time + AggroHoldDuration;
            if (!_isAggroed)
            {
                _isAggroed = true;
                if (!_isStunned && !_isEvading) CurrentState = MonsterAIState.Aggro;
                CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                    transform.position, "!", new Color(1f, 0.22f, 0.2f, 1f), 5.6f);
            }
        }

        public bool HasLineOfSightToPlayer()
        {
            if (Player == null) return false;
            if (_obstacleLayerMask.value == 0) return true;
            RaycastHit2D hit = Physics2D.Linecast(transform.position, Player.transform.position, _obstacleLayerMask);
            return hit.collider == null;
        }

        /// <summary>
        /// 현재 거리 조건과 개별 쿨타임을 만족하는 패턴들 중, weight 비율에 따라 가중치 랜덤(Weighted Random)으로 1개를 채택합니다.
        /// </summary>
        private bool TrySelectWeightedPattern(int gridDistToPlayer, out MonsterPatternSO selectedPattern)
        {
            selectedPattern = null;
            if (_monsterData.patterns == null || _monsterData.patterns.Count == 0)
            {
                return false;
            }

            _candidatePatterns.Clear();
            float totalWeight = 0f;

            for (int i = 0; i < _monsterData.patterns.Count; i++)
            {
                MonsterPatternSO pattern = _monsterData.patterns[i];
                if (pattern == null) continue;

                // 개별 쿨타임 검사
                if (_patternCooldownExpirations.TryGetValue(pattern, out float expireTime) && Time.time < expireTime)
                {
                    continue;
                }

                // 사거리 및 실행 가능 조건 검사
                if (!pattern.CanExecute(this, gridDistToPlayer))
                {
                    continue;
                }

                float w = Mathf.Max(0.01f, pattern.weight);
                _candidatePatterns.Add(pattern);
                totalWeight += w;
            }

            if (_candidatePatterns.Count == 0 || totalWeight <= 0f)
            {
                return false;
            }

            // 가중치 랜덤 주사위 추첨
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < _candidatePatterns.Count; i++)
            {
                MonsterPatternSO candidate = _candidatePatterns[i];
                roll -= Mathf.Max(0.01f, candidate.weight);
                if (roll <= 0f)
                {
                    selectedPattern = candidate;
                    return true;
                }
            }

            selectedPattern = _candidatePatterns[_candidatePatterns.Count - 1];
            return true;
        }

        private void MarkActionTowardPlayer()
        {
            if (Player == null) return;
            Vector3 toPlayer = Player.transform.position - transform.position;
            LastActionDirection = new Vector3(toPlayer.x, toPlayer.y, 0f);
            ActionCount++;
        }

        // 패턴 SO가 예고 시간을 계산한 직후 호출한다. 예고 시간은 패턴마다 계산이 달라 컨트롤러가 미리 알 수 없다
        public void NotifyAttackWindup(float duration)
        {
            OnAttackWindup?.Invoke(duration);
        }

        private IEnumerator ExecuteFallbackMeleeRoutine()
        {
            Vector3Int targetCell = GetPlayerCell();
            Vector3 cellWorldPos = Grid.GetCellCenterWorld(targetCell);
            float telegraph = Mathf.Max(0.2f, _monsterData.telegraphTime * _monsterData.GetEffectiveTelegraphMultiplier());
            NotifyAttackWindup(telegraph);

            SpawnWarning(cellWorldPos, telegraph);
            yield return new WaitForSeconds(telegraph);

            DealDamageToPlayerAtCell(targetCell, Attack, (cellWorldPos - transform.position).normalized, false);
        }

        // =====================================================================
        // 반응형 기동: 2단계(능동 스텝 회피) & 3단계(피격 후방 이탈 및 벽 충돌 기절)
        // =====================================================================

        /// <summary>
        /// 날렵한 근거리(AgileFlank) 전용: 플레이어가 공격을 휘두를 때 쿨타임이 돌지 않았다면
        /// 0.15초 인간적 반응 딜레이 후 90도 측면 빈 타일로 회피합니다.
        /// 단, 패턴 시전 중(Motion Lock)이거나 후딜레이(Recovery Window) 중에는 절대 회피할 수 없습니다!
        /// </summary>
        private void HandlePlayerAttackDetected(Vector3 playerAttackDir)
        {
            if (_isDead || _isStunned || _isEvading || _isExecutingPattern || _isInRecoveryWindow || _monsterData == null || Player == null || Grid == null)
            {
                return;
            }

            // 회피 스텝은 1칸 몸 전용 기동
            if (_monsterData.movementStyle != MonsterMovementStyle.AgileFlank || _monsterData.evasiveStepCooldown <= 0f || Footprint > 1 || IsStationary)
            {
                return;
            }

            if (Time.time < _nextEvasiveStepTime)
            {
                return;
            }

            int gridDist = GetBodyChebyshevDistance(GetPlayerCell());
            if (gridDist > 2)
            {
                return;
            }

            // 플레이어가 몬스터 쪽을 향해 공격했을 때만 반응
            Vector3 dirToMonster = (transform.position - Player.transform.position).normalized;
            if (Vector3.Dot(playerAttackDir.normalized, dirToMonster) < 0.25f)
            {
                return;
            }

            _nextEvasiveStepTime = Time.time + _monsterData.evasiveStepCooldown;
            TriggerInterruptRoutine(AgileEvasiveStepRoutine(playerAttackDir));
        }

        private IEnumerator AgileEvasiveStepRoutine(Vector3 attackDir)
        {
            _isEvading = true;
            CurrentState = MonsterAIState.Evading;

            // 억까 방지: 0.15초 인간적인 반응 지연
            yield return new WaitForSeconds(0.15f);

            if (_isDead || _isStunned || Grid == null || TileSystem.Instance == null)
            {
                _isEvading = false;
                if (!_isDead && !_isStunned) CurrentState = _isAggroed ? MonsterAIState.Aggro : MonsterAIState.Patrol;
                yield break;
            }

            Vector3Int currentCell = GetCurrentCell();
            Vector3 perpA = new Vector3(-attackDir.y, attackDir.x, 0f);
            Vector3 perpB = new Vector3(attackDir.y, -attackDir.x, 0f);

            Vector3Int cellA = currentCell + MonsterLocomotion.SnapTo4WayCell(perpA);
            Vector3Int cellB = currentCell + MonsterLocomotion.SnapTo4WayCell(perpB);

            Vector3Int targetCell = currentCell;
            if (MonsterLocomotion.CanStepToCell(this, cellA) && TileSystem.Instance.GetDangerCost(cellA) <= 0f)
            {
                targetCell = cellA;
            }
            else if (MonsterLocomotion.CanStepToCell(this, cellB) && TileSystem.Instance.GetDangerCost(cellB) <= 0f)
            {
                targetCell = cellB;
            }

            // 코너나 벽에 몰려 양쪽 측면이 모두 막혀 있으면 회피 실패! (플레이어의 포지셔닝 보상)
            if (targetCell != currentCell && TryMoveBody(targetCell))
            {
                CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                    transform.position, "EVADE", new Color(0.25f, 0.95f, 1f, 1f), 4.2f);
                Vector3 endPos = Grid.GetCellCenterWorld(targetCell);
                yield return StartActionCoroutine(MoveToTileRoutine(endPos, 0.14f));
                // 회피 직후 짧은 숨고르기 (0.35초 딜타임)
                yield return new WaitForSeconds(0.35f);
            }

            _isEvading = false;
            if (!_isDead && !_isStunned) CurrentState = _isAggroed ? MonsterAIState.Aggro : MonsterAIState.Patrol;
        }

        private IEnumerator RangedEscapeOnHitRoutine()
        {
            _isEvading = true;
            CurrentState = MonsterAIState.Evading;
            ClearActiveWarnings();
            HideAimLines();

            if (Player != null && Grid != null && TileSystem.Instance != null)
            {
                Vector3Int currentCell = GetCurrentCell();
                Vector3 awayDir = (transform.position - Player.transform.position).normalized;
                Vector3Int stepDir = MonsterLocomotion.SnapTo4WayCell(awayDir);

                // 1칸부터 순차적으로 탐색 (중간에 벽이나 장애물이 있으면 통과하지 못하고 즉시 중단)
                Vector3Int chosenCell = currentCell;
                int maxEscapeDist = 2; // 최대 1~2칸 후퇴
                for (int dist = 1; dist <= maxEscapeDist; dist++)
                {
                    Vector3Int candidate = currentCell + stepDir * dist;
                    if (!MonsterLocomotion.CanStepToCell(this, candidate))
                    {
                        break; // 1칸이 막히면 2칸으로 건너뛸 수 없음!
                    }
                    chosenCell = candidate;
                }

                // 만약 정후방이 바로 벽으로 막혀 있다면, 좌/우 측면 타일 회피 시도
                if (chosenCell == currentCell)
                {
                    Vector3 perpA = new Vector3(-stepDir.y, stepDir.x, 0f);
                    Vector3 perpB = new Vector3(stepDir.y, -stepDir.x, 0f);
                    Vector3Int sideCellA = currentCell + MonsterLocomotion.SnapTo4WayCell(perpA);
                    Vector3Int sideCellB = currentCell + MonsterLocomotion.SnapTo4WayCell(perpB);

                    if (MonsterLocomotion.CanStepToCell(this, sideCellA))
                    {
                        chosenCell = sideCellA;
                    }
                    else if (MonsterLocomotion.CanStepToCell(this, sideCellB))
                    {
                        chosenCell = sideCellB;
                    }
                }

                if (chosenCell != currentCell && TryMoveBody(chosenCell))
                {
                    CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                        transform.position, "ESCAPE", new Color(0.8f, 0.5f, 1f, 1f), 4.2f);
                    Vector3 endPos = Grid.GetCellCenterWorld(chosenCell);
                    int actualDist = MonsterLocomotion.GetManhattanDistance(currentCell, chosenCell);
                    float moveDuration = actualDist > 1 ? 0.22f : 0.16f;
                    yield return StartActionCoroutine(MoveToTileRoutine(endPos, moveDuration));
                }
            }

            // 탈출 직후 자세 복구 딜레이 (공정한 파훼 빈틈 0.35초)
            yield return new WaitForSeconds(0.35f);
            _isEvading = false;
            if (!_isDead && !_isStunned) CurrentState = _isAggroed ? MonsterAIState.Aggro : MonsterAIState.Patrol;
        }

        /// <summary>
        /// 돌진 중 벽 충돌 등으로 인해 몬스터가 기절(Stun) 상태에 빠질 때 호출합니다.
        /// </summary>
        public void ApplyStun(float stunDuration)
        {
            if (_isDead || stunDuration <= 0f) return;
            TriggerInterruptRoutine(StunRoutine(stunDuration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            _isStunned = true;
            CurrentState = MonsterAIState.Stunned;
            _isExecutingPattern = false;
            _isEvading = false;
            ClearActiveWarnings();
            HideAimLines();

            CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                transform.position, "STUN!", new Color(1f, 0.88f, 0.15f, 1f), 4.8f);

            // 기절 시각 피드백 (노란색 점멸)
            SpriteRenderer primary = (_renderers != null && _renderers.Length > 0) ? _renderers[0] : null;
            Color origColor = Color.white;
            if (primary != null)
            {
                primary.color = new Color(1f, 0.9f, 0.2f, 1f);
            }

            yield return new WaitForSeconds(duration);

            if (primary != null)
            {
                primary.color = origColor;
            }
            _isStunned = false;
            if (!_isDead) CurrentState = _isAggroed ? MonsterAIState.Aggro : MonsterAIState.Patrol;
        }

        private void TriggerInterruptRoutine(IEnumerator routine)
        {
            StopActionCoroutines();

            _isExecutingPattern = false;
            ClearActiveWarnings();
            HideAimLines();

            _interruptCoroutine = StartCoroutine(RunInterruptAndResumeBehavior(routine));
        }

        private IEnumerator RunInterruptAndResumeBehavior(IEnumerator interruptRoutine)
        {
            yield return StartActionCoroutine(interruptRoutine);
            _interruptCoroutine = null;
            _actionChildCoroutines.Clear();
            if (!_isDead)
            {
                _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
            }
        }

        /// <summary>
        /// 행동 체인에 속하는 하위 코루틴(패턴, 이동 스텝, 타일 이동)을 시작하고 핸들을 추적합니다.
        /// 패턴 SO와 MonsterLocomotion도 monster.StartCoroutine 대신 이 메서드를 사용해야 인터럽트 시 함께 중단됩니다.
        /// </summary>
        public Coroutine StartActionCoroutine(IEnumerator routine)
        {
            Coroutine handle = StartCoroutine(routine);
            _actionChildCoroutines.Add(handle);
            return handle;
        }

        /// <summary>
        /// 행동 루프, 인터럽트 루틴과 그 하위 코루틴을 모두 중단합니다.
        /// (부모만 멈추면 자식 패턴이 계속 실행되어 기절/사망 후에도 데미지를 주거나, 두 이동 코루틴이 동시에 위치를 갱신하는 문제가 생김)
        /// </summary>
        private void StopActionCoroutines()
        {
            if (_behaviorCoroutine != null)
            {
                StopCoroutine(_behaviorCoroutine);
                _behaviorCoroutine = null;
            }
            if (_interruptCoroutine != null)
            {
                StopCoroutine(_interruptCoroutine);
                _interruptCoroutine = null;
            }
            for (int i = 0; i < _actionChildCoroutines.Count; i++)
            {
                if (_actionChildCoroutines[i] != null)
                {
                    StopCoroutine(_actionChildCoroutines[i]);
                }
            }
            _actionChildCoroutines.Clear();
            CurrentPattern = null;

            // 타일 이동 도중 중단됐다면, 공중에 뜨거나 중간 타일에 걸치지 않도록 이미 점유한 목적지 좌표로 즉시 정렬
            if (_isMovingToTile)
            {
                transform.position = _currentDestinationWorldPos;
                _isMovingToTile = false;
            }
            // TryMoveBody 성공 직후 MoveToTileRoutine 시작 전 틈에 끊겨도 뒷줄이 남지 않게 이동 여부와 무관하게 정리
            FlushPendingRelease();
        }

        // =====================================================================
        // 타일 이동, 전투 판정 및 경고 UI / 레이저 조준선 API
        // =====================================================================

        // 몸의 좌하단 칸(anchor). 점유를 등록한 뒤에는 위치 역산 대신 기억해 둔 칸을 쓴다:
        // 이동 중 transform은 홉 높이·보간이 섞여 있고, 2×2 몸의 중심은 칸 경계라 역산하면 다른 칸이 나와 유령 점유가 남는다
        public Vector3Int GetCurrentCell()
        {
            if (_hasOccupiedAnchor) return _occupiedAnchor;
            if (Grid == null) return Vector3Int.zero;
            return GetAnchorFromCenterWorld(Grid, transform.position, Footprint);
        }

        // --- 여러 칸 몸 (Footprint) ---
        public int Footprint => ClampFootprint(_monsterData);

        private static int ClampFootprint(MonsterDataSO data) => data != null ? Mathf.Clamp(data.footprint, 1, 5) : 1;

        /// <summary>프리팹의 몸 크기. 생성 전 칸 계산(보스 스폰·층 검증)용</summary>
        public static int GetFootprint(GameObject monsterPrefab)
        {
            if (monsterPrefab == null || !monsterPrefab.TryGetComponent(out MonsterController mc)) return 1;
            return ClampFootprint(mc.Data);
        }

        public static Vector3Int GetAnchorFromCenterWorld(Grid grid, Vector3 centerWorld, int size)
        {
            float half = (size - 1) * 0.5f;
            Vector3 cellSize = grid.cellSize;
            Vector3Int cell = grid.WorldToCell(centerWorld - new Vector3(half * cellSize.x, half * cellSize.y, 0f));
            cell.z = 0;
            return cell;
        }

        public static Vector3 GetBodyCenterWorld(Grid grid, Vector3Int anchor, int size)
        {
            float half = (size - 1) * 0.5f;
            Vector3 cellSize = grid.cellSize;
            return grid.GetCellCenterWorld(anchor) + new Vector3(half * cellSize.x, half * cellSize.y, 0f);
        }

        // 보스 마커 칸 → 몸 좌하단 칸. 2×2는 마커가 좌하단, 3×3은 마커가 가운데
        public static Vector3Int GetAnchorFromMarker(Vector3Int markerCell, int size)
        {
            int offset = (size - 1) / 2;
            return new Vector3Int(markerCell.x - offset, markerCell.y - offset, 0);
        }

        public Vector3 GetBodyCenterWorld(Vector3Int anchor) => GetBodyCenterWorld(Grid, anchor, Footprint);

        public bool IsBodyCell(Vector3Int cell)
        {
            Vector3Int a = GetCurrentCell();
            int n = Footprint;
            return cell.x >= a.x && cell.x < a.x + n && cell.y >= a.y && cell.y < a.y + n;
        }

        // 몸 사각형에서 cell까지의 축별 거리(몸 안이면 0)
        private static void GetAxisGap(Vector3Int anchor, int size, Vector3Int cell, out int dx, out int dy)
        {
            dx = cell.x < anchor.x ? anchor.x - cell.x : (cell.x > anchor.x + size - 1 ? cell.x - (anchor.x + size - 1) : 0);
            dy = cell.y < anchor.y ? anchor.y - cell.y : (cell.y > anchor.y + size - 1 ? cell.y - (anchor.y + size - 1) : 0);
        }

        public static int GetBodyChebyshevDistance(Vector3Int anchor, int size, Vector3Int cell)
        {
            GetAxisGap(anchor, size, cell, out int dx, out int dy);
            return Mathf.Max(dx, dy);
        }

        public static int GetBodyManhattanDistance(Vector3Int anchor, int size, Vector3Int cell)
        {
            GetAxisGap(anchor, size, cell, out int dx, out int dy);
            return dx + dy;
        }

        public int GetBodyChebyshevDistance(Vector3Int cell) => GetBodyChebyshevDistance(GetCurrentCell(), Footprint, cell);
        public int GetBodyManhattanDistance(Vector3Int cell) => GetBodyManhattanDistance(GetCurrentCell(), Footprint, cell);

        // 전리품 드랍·폭발 중심 칸. 짝수 몸은 가운데 4칸 중 좌하단
        public Vector3Int GetCenterCell()
        {
            Vector3Int a = GetCurrentCell();
            int offset = (Footprint - 1) / 2;
            return new Vector3Int(a.x + offset, a.y + offset, 0);
        }

        public bool IsAnyBodyCellVisible(FogOfWarSystem fog, Vector3Int anchor)
        {
            int n = Footprint;
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    if (fog.IsCellVisible(new Vector3Int(anchor.x + x, anchor.y + y, 0))) return true;
                }
            }
            return false;
        }

        /// <summary>몸 전체를 newAnchor로 옮겨 점유한다. 하나라도 막히면 아무것도 바꾸지 않고 false. 모든 몬스터 점유 이동은 이 함수를 거친다.</summary>
        public bool TryMoveBody(Vector3Int newAnchor)
        {
            if (TileSystem.Instance == null || IsStationary) return false;
            int size = Footprint;
            Vector3Int oldAnchor = GetCurrentCell();

            if (size <= 1)
            {
                if (!TileSystem.Instance.TryOccupyFootprint(newAnchor, size, gameObject, oldAnchor)) return false;
                _occupiedAnchor = newAnchor;
                _hasOccupiedAnchor = true;
                return true;
            }

            // 여러 칸 몸: 이동하는 동안 옛 칸도 막아 둔다. 그림이 미끄러져 지나가는 칸이 비면 통과·헛스윙이 생긴다 (11 설계안 4.4절)
            FlushPendingRelease();
            if (!TileSystem.Instance.TryOccupyFootprintKeepOld(newAnchor, size, gameObject)) return false;
            if (_hasOccupiedAnchor && oldAnchor != newAnchor)
            {
                _pendingReleaseAnchor = oldAnchor;
                _hasPendingRelease = true;
            }
            _occupiedAnchor = newAnchor;
            _hasOccupiedAnchor = true;
            OnBodyCellsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 이동 중 남겨 둔 옛 몸 칸 중 새 몸 밖의 칸(뒷줄)을 푼다. 대기 중인 해제가 없으면 아무것도 안 하므로 여러 번 불러도 안전하다.
        /// 호출 지점: 이동 도착(MoveToTileRoutine), StopActionCoroutines, ReleaseBody, 다음 TryMoveBody
        /// </summary>
        private void FlushPendingRelease()
        {
            if (!_hasPendingRelease) return;
            _hasPendingRelease = false;
            if (TileSystem.Instance != null)
            {
                TileSystem.Instance.ReleaseFootprintExcept(_pendingReleaseAnchor, _occupiedAnchor, Footprint, gameObject);
            }
            OnBodyCellsChanged?.Invoke();
        }

        /// <summary>지금 실제로 점유한 칸을 감싸는 사각형(이동 중이면 옛 몸 + 새 몸). 점유가 없으면 false</summary>
        public bool TryGetOccupiedCellBounds(out Vector3Int min, out Vector3Int max)
        {
            min = max = Vector3Int.zero;
            if (!_isBodyRegistered || !_hasOccupiedAnchor) return false;
            int n = Footprint;
            min = _occupiedAnchor;
            max = new Vector3Int(_occupiedAnchor.x + n - 1, _occupiedAnchor.y + n - 1, 0);
            if (_hasPendingRelease)
            {
                min = Vector3Int.Min(min, _pendingReleaseAnchor);
                max = Vector3Int.Max(max, new Vector3Int(_pendingReleaseAnchor.x + n - 1, _pendingReleaseAnchor.y + n - 1, 0));
            }
            return true;
        }

        private void RegisterBody()
        {
            if (Grid == null || TileSystem.Instance == null) return;
            _occupiedAnchor = GetAnchorFromCenterWorld(Grid, transform.position, Footprint);
            _hasOccupiedAnchor = true;
            TileSystem.Instance.RegisterFootprint(_occupiedAnchor, Footprint, gameObject);
            _isBodyRegistered = true;
            OnBodyCellsChanged?.Invoke();
        }

        // 투사체 트리거·플레이어 이동 폴백(OverlapPoint)이 몸 칸 어디서든 닿게 콜라이더를 몸 크기로 맞춘다. 1칸은 프리팹 값 그대로
        private void FitColliderToFootprint()
        {
            int n = Footprint;
            if (n <= 1 || !TryGetComponent(out BoxCollider2D box)) return;
            box.offset = Vector2.zero;
            box.size = new Vector2(n - 0.1f, n - 0.1f);
        }

        private void ReleaseBody()
        {
            // 점유 칸을 기억하고 있으면 Grid가 먼저 파괴돼도 해제할 수 있다
            if ((!_hasOccupiedAnchor && Grid == null) || TileSystem.Instance == null) return;
            // 이동 중 제거(사망·자폭·강제 제거·파괴)되면 남겨 둔 뒷줄도 함께 푼다
            if (_hasPendingRelease)
            {
                _hasPendingRelease = false;
                TileSystem.Instance.ReleaseFootprint(_pendingReleaseAnchor, Footprint, gameObject);
            }
            TileSystem.Instance.ReleaseFootprint(GetCurrentCell(), Footprint, gameObject);
            if (_isBodyRegistered)
            {
                _isBodyRegistered = false;
                OnBodyCellsChanged?.Invoke();
            }
        }

        public Vector3Int GetPlayerCell()
        {
            if (Grid == null || Player == null) return Vector3Int.zero;
            Vector3Int cell = Grid.WorldToCell(Player.transform.position);
            cell.z = 0;
            return cell;
        }

        /// <summary>
        /// 몬스터 이동은 바닥 슬라이드가 기본이다(기획 확정, 이동 모션 없이 대기 프레임 재생). hopHeight는 특정 동작만 띄우고 싶을 때를 위해 남겨 둔다.
        /// 칸마다 가감속을 넣으면 여러 칸 돌진·연속 이동이 끊겨 보이므로 직선 보간을 쓴다(칸 경계는 t=0.5에 넘는다).
        /// </summary>
        public IEnumerator MoveToTileRoutine(Vector3 endPos, float customDuration = -1f, float hopHeight = 0f)
        {
            // 도착 몸 칸 중 하나라도 시야 안이면 이동 시작부터 보이게 한다. 점유(TryMoveBody)가 먼저 끝나 있으므로 목적지 = 현재 anchor
            if (FogOfWarSystem.Instance != null && Grid != null)
            {
                Vector3Int destAnchor = _hasOccupiedAnchor ? _occupiedAnchor : GetAnchorFromCenterWorld(Grid, endPos, Footprint);
                if (IsAnyBodyCellVisible(FogOfWarSystem.Instance, destAnchor))
                {
                    SetVisibleByFog(true);
                }
            }

            _isMovingToTile = true;
            _currentDestinationWorldPos = endPos;

            float duration = customDuration > 0f
                ? customDuration
                : (_monsterData != null ? _monsterData.GetEffectiveMoveDuration() : 0.3f);

            float elapsed = 0f;
            Vector3 startPos = transform.position;
            CurrentMoveDirection = new Vector3(endPos.x - startPos.x, endPos.y - startPos.y, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 lerpedPos = Vector3.Lerp(startPos, endPos, t);
                if (hopHeight > 0.001f) lerpedPos.y += Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = lerpedPos;
                yield return null;
            }
            transform.position = endPos;
            _isMovingToTile = false;
            FlushPendingRelease();

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UpdateSingleMonsterVisibility(this);
            }
        }

        public AttackWarningUI SpawnWarning(Vector3 worldPos, float duration)
        {
            if (Grid != null && TileSystem.Instance != null)
            {
                Vector3Int dangerCell = Grid.WorldToCell(worldPos);
                dangerCell.z = 0;
                TileSystem.Instance.SetDanger(dangerCell, duration);
            }

            if (_warningPrefab == null) return null;
            var warning = AttackWarningUI.Show(_warningPrefab, worldPos, duration);
            if (warning != null)
            {
                _activeWarnings.Add(warning);
            }
            return warning;
        }

        public void ClearActiveWarnings()
        {
            for (int i = 0; i < _activeWarnings.Count; i++)
            {
                if (_activeWarnings[i] != null && _activeWarnings[i].gameObject.activeSelf)
                {
                    _activeWarnings[i].CancelAndReturn();
                }
            }
            _activeWarnings.Clear();
        }

        /// <summary>
        /// 원거리 직사 투사체용: 바닥 타일 장판 대신 자유 각도로 회전하는 얇은 레이저 조준선을 표시합니다.
        /// </summary>
        public void ShowAimLine(Vector3 startWorldPos, Vector3 direction, float length)
        {
            LineRenderer lr = GetOrCreateAimLine(0);
            ConfigureAimLine(lr, startWorldPos, direction, length);
            for (int i = 1; i < _aimLinePool.Count; i++)
            {
                if (_aimLinePool[i] != null) _aimLinePool[i].enabled = false;
            }
        }

        public void ShowAimLines(Vector3 startWorldPos, IReadOnlyList<Vector3> directions, float length)
        {
            if (directions == null) return;
            for (int i = 0; i < directions.Count; i++)
            {
                LineRenderer lr = GetOrCreateAimLine(i);
                ConfigureAimLine(lr, startWorldPos, directions[i], length);
            }
            for (int i = directions.Count; i < _aimLinePool.Count; i++)
            {
                if (_aimLinePool[i] != null) _aimLinePool[i].enabled = false;
            }
        }

        public void HideAimLines()
        {
            for (int i = 0; i < _aimLinePool.Count; i++)
            {
                if (_aimLinePool[i] != null)
                {
                    _aimLinePool[i].enabled = false;
                }
            }
        }

        private LineRenderer GetOrCreateAimLine(int index)
        {
            while (_aimLinePool.Count <= index)
            {
                GameObject go = new GameObject($"AimLine_{_aimLinePool.Count}");
                go.transform.SetParent(transform, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.06f;
                lr.endWidth = 0.06f;
                lr.useWorldSpace = true;
                lr.sortingOrder = 9;

                if (_sharedAimLineMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        _sharedAimLineMaterial = new Material(shader);
                    }
                }
                if (_sharedAimLineMaterial != null)
                {
                    lr.sharedMaterial = _sharedAimLineMaterial;
                }

                Color aimColor = new Color(1f, 0.15f, 0.15f, 0.65f);
                lr.startColor = aimColor;
                lr.endColor = new Color(1f, 0.15f, 0.15f, 0.15f);
                lr.enabled = false;
                _aimLinePool.Add(lr);
            }
            return _aimLinePool[index];
        }

        private void ConfigureAimLine(LineRenderer lr, Vector3 startPos, Vector3 dir, float length)
        {
            if (lr == null) return;
            Vector3 normDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.down;
            lr.SetPosition(0, startPos);
            lr.SetPosition(1, startPos + normDir * length);
            lr.enabled = true;
        }

        /// <summary>
        /// 특정 타일에 플레이어가 서 있으면 근접/장판 데미지를 가하고, LIFESTEAL 특성 보유 시 체력을 회복합니다.
        /// </summary>
        public bool DealDamageToPlayerAtCell(Vector3Int targetCell, int damage, Vector3 hitDir, bool isHeavyHit)
        {
            if (Player == null || Grid == null || Combat == null) return false;
            Vector3Int playerCell = GetPlayerCell();
            if (playerCell.x != targetCell.x || playerCell.y != targetCell.y)
            {
                return false;
            }

            int dealt = Combat.ApplyImmediateHit(Player.gameObject, damage, hitDir, isHeavyHit);
            if (dealt <= 0) return false;

            ApplyLifestealIfApplicable(dealt);
            return true;
        }

        public bool DealDamageToPlayerIfInCells(IReadOnlyList<Vector3Int> targetCells, int damage, Vector3 hitDir, bool isHeavyHit)
        {
            if (Player == null || Grid == null || targetCells == null) return false;
            Vector3Int playerCell = GetPlayerCell();
            for (int i = 0; i < targetCells.Count; i++)
            {
                if (targetCells[i].x == playerCell.x && targetCells[i].y == playerCell.y)
                {
                    return DealDamageToPlayerAtCell(playerCell, damage, hitDir, isHeavyHit);
                }
            }
            return false;
        }

        public void SpawnMonsterProjectile(GameObject projectilePrefab, Vector3 direction, int damage)
        {
            GameObject prefabToUse = projectilePrefab != null ? projectilePrefab : _defaultProjectilePrefab;
            Vector3 normDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.down;

            if (prefabToUse != null)
            {
                float angle = Mathf.Atan2(normDir.y, normDir.x) * Mathf.Rad2Deg;
                Projectile.Spawn(prefabToUse, transform.position, Quaternion.Euler(0f, 0f, angle), normDir, damage, gameObject);
            }
            else if (Player != null && Grid != null)
            {
                // 투사체 프리팹이 아직 할당되지 않은 경우를 위한 안전 폴백
                float dist = Vector3.Distance(transform.position, Player.transform.position);
                if (dist <= (_monsterData != null ? _monsterData.zocRadius : 6f))
                {
                    DealDamageToPlayerAtCell(GetPlayerCell(), damage, normDir, false);
                }
            }
        }

        private void ApplyLifestealIfApplicable(int damageDealt)
        {
            if (_monsterData != null && _monsterData.HasTrait("LIFESTEAL") && damageDealt > 0)
            {
                int healAmount = Mathf.Max(1, Mathf.RoundToInt(damageDealt * 0.3f));
                Heal(healAmount);
            }
        }

        // =====================================================================
        // 피격, 보호막(SHIELD), 자폭(EXPLOSIVE), 사망 처리
        // =====================================================================

        /// <returns>보호막 흡수분 + 실제 깎인 체력 (초과 피해 제외). 이미 사망했으면 0</returns>
        public int TakeDamage(int damage, Vector3 hitDirection = default, bool isHeavyHit = false)
        {
            if (_isDead || damage <= 0) return 0;

            // 방어력 경감 → 보호막 흡수 → HP 순서. 경감 후 값이 데미지 텍스트·반환값의 기준
            damage = CombatMath.Mitigate(damage, _monsterData != null ? _monsterData.def : 0);

            int remainingDamage = damage;
            bool hadShield = CurrentShield > 0;

            if (CurrentShield > 0)
            {
                int absorbed = Mathf.Min(CurrentShield, remainingDamage);
                CurrentShield -= absorbed;
                remainingDamage -= absorbed;
            }

            int dealt = damage - remainingDamage + Mathf.Min(remainingDamage, Mathf.Max(0, CurrentHp));
            CurrentHp -= remainingDamage;
            string mName = _monsterData != null ? _monsterData.monsterName : name;
            Debug.Log($"[Monster] {mName} 가 {damage} 피해를 입음! 남은 HP: {CurrentHp} (Shield: {CurrentShield})");

            GlobalEvents.OnEntityDamaged?.Invoke(transform.position, damage, isHeavyHit, false);
            OnDamaged?.Invoke();

            if (CurrentHp <= 0)
            {
                CurrentHp = 0;
                Debug.Log($"[Monster] {mName} 처치됨!");
                Die();
                return dealt;
            }

            if (_hitReactionCoroutine != null) StopCoroutine(_hitReactionCoroutine);
            _hitReactionCoroutine = StartCoroutine(HitReactionRoutine(hitDirection, isHeavyHit));

            // 피격 시 비인식 상태였다면 즉시 어그로 활성화
            ForceAggro();

            // 원거리 몬스터(RangedKiting): 보호막이 없고, 패턴 시전(Motion Lock) 또는 후딜레이(Recovery Window) 중이 아닐 때만 후방 탈출 발동 (공정한 딜타임 보장)
            if (!hadShield &&
                !_isStunned &&
                !_isEvading &&
                !_isExecutingPattern &&
                !_isInRecoveryWindow &&
                _monsterData != null &&
                _monsterData.movementStyle == MonsterMovementStyle.RangedKiting &&
                Footprint == 1 &&
                !IsStationary &&
                _monsterData.escapeOnHitCooldown > 0f &&
                Time.time >= _nextEscapeOnHitTime)
            {
                _nextEscapeOnHitTime = Time.time + _monsterData.escapeOnHitCooldown;
                TriggerInterruptRoutine(RangedEscapeOnHitRoutine());
            }
            return dealt;
        }

        private IEnumerator HitReactionRoutine(Vector3 hitDirection, bool isHeavyHit)
        {
            SpriteRenderer primaryRenderer = (_renderers != null && _renderers.Length > 0) ? _renderers[0] : GetComponentInChildren<SpriteRenderer>();
            if (primaryRenderer == null) yield break;

            Transform visualTr = primaryRenderer.transform;
            Color originalColor = _isStunned ? new Color(1f, 0.9f, 0.2f, 1f) : Color.white;
            primaryRenderer.color = isHeavyHit ? new Color(1f, 0.35f, 0.1f, 1f) : Color.red;

            bool canOffsetLocal = visualTr != transform;
            Vector3 origLocalPos = visualTr.localPosition;
            Vector3 origScale = visualTr.localScale;

            Vector3 recoilDir = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector3.up;
            float recoilDist = isHeavyHit ? 0.18f : 0.09f;
            Vector3 squashScale = new Vector3(origScale.x * (isHeavyHit ? 1.25f : 1.12f), origScale.y * (isHeavyHit ? 0.78f : 0.88f), origScale.z);

            float duration = isHeavyHit ? 0.16f : 0.11f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float punch = Mathf.Sin(t * Mathf.PI);

                if (canOffsetLocal)
                {
                    visualTr.localPosition = origLocalPos + recoilDir * (recoilDist * punch);
                }
                visualTr.localScale = Vector3.Lerp(origScale, squashScale, punch);
                yield return null;
            }

            if (canOffsetLocal) visualTr.localPosition = origLocalPos;
            visualTr.localScale = origScale;
            primaryRenderer.color = _isStunned ? new Color(1f, 0.9f, 0.2f, 1f) : Color.white;
            _hitReactionCoroutine = null;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            CurrentState = MonsterAIState.Dead;

            StopActionCoroutines();
            ClearActiveWarnings();
            HideAimLines();

            if (_monsterData != null && _monsterData.HasTrait("EXPLOSIVE") && Grid != null)
            {
                StartCoroutine(ExplosiveDeathRoutine());
                return;
            }

            FinalizeDeath();
        }

        private IEnumerator ExplosiveDeathRoutine()
        {
            // 폭발 범위 = 몸과 그 둘레 1칸 (1칸 몸이면 기존 3×3)
            Vector3Int anchor = GetCurrentCell();
            int size = Footprint;
            float fuseTime = 0.75f;

            // 퓨즈 동안 시체가 길을 막지 않도록 타일 점유를 먼저 해제 (OnDestroy의 재해제는 무해)
            ReleaseBody();
            // 행동 코루틴은 Die에서 모두 멈췄으므로 패턴 버퍼를 재사용한다 (GC 없음)
            List<Vector3Int> blastCells = PatternCellsBuffer;
            blastCells.Clear();

            for (int x = -1; x <= size; x++)
            {
                for (int y = -1; y <= size; y++)
                {
                    Vector3Int c = anchor + new Vector3Int(x, y, 0);
                    blastCells.Add(c);
                    SpawnWarning(Grid.GetCellCenterWorld(c), fuseTime);
                }
            }

            yield return new WaitForSeconds(fuseTime);

            int explosionDmg = _monsterData != null ? Attack * 2 : 15;
            DealDamageToPlayerIfInCells(blastCells, explosionDmg, Vector3.up, true);
            FinalizeDeath();
        }

        private void FinalizeDeath()
        {
            // 전리품은 몬스터 몸 칸을 피해 놓이므로(LootSystem) 자기 몸 칸이 비어 있어야 몸 중심 주변에 떨어진다. OnDestroy의 재해제는 무해
            ReleaseBody();
            if (!IsSummon)
            {
                int dropPoolId = _monsterData != null ? _monsterData.dropPoolID : 0;
                // 2×2 몸의 중심은 칸 경계라 그대로 넘기면 드랍 칸이 흔들린다. 중심 칸의 가운데를 넘긴다
                Vector3 dropPos = Grid != null ? Grid.GetCellCenterWorld(GetCenterCell()) : transform.position;
                GlobalEvents.OnMonsterDied?.Invoke(dropPos, dropPoolId);
            }
            RaiseRemoved();
            Destroy(gameObject);
        }

        /// <summary>
        /// 보스 처치 정리용 강제 제거. 전리품(OnMonsterDied)과 자폭 없이 즉시 사라진다.
        /// HP 0으로 자폭 퓨즈 중인 개체도 아직 등록부에 남아 있으므로 _isDead가 아니라 제거 발행 여부로 중복을 막고,
        /// 퓨즈 코루틴은 행동 코루틴 목록 밖에서 돌기 때문에 StopAllCoroutines로 함께 멈춘다.
        /// </summary>
        public void DespawnWithoutLoot()
        {
            if (_isRemovedRaised) return;
            _isDead = true;
            CurrentState = MonsterAIState.Dead;

            StopAllCoroutines();
            CurrentPattern = null;
            ClearActiveWarnings();
            HideAimLines();

            ReleaseBody();

            RaiseRemoved();
            Destroy(gameObject);
        }

        private void RaiseRemoved()
        {
            if (_isRemovedRaised) return;
            _isRemovedRaised = true;
            OnRemoved?.Invoke(this);
        }

        public void Heal(int amount)
        {
            if (_monsterData == null || _isDead) return;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        }

        public void SetVisibleByFog(bool isVisible)
        {
            if (IsVisibleByFog != isVisible)
            {
                IsVisibleByFog = isVisible;
                OnFogVisibilityChanged?.Invoke(isVisible);
            }
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = isVisible;
                }
            }
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
