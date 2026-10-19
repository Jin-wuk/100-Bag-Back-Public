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

        public EntityCombat Combat { get; private set; }
        public Grid Grid { get; private set; }
        public PlayerController Player { get; private set; }
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;
        public AttackWarningUI WarningPrefab => _warningPrefab;
        public GameObject DefaultProjectilePrefab => _defaultProjectilePrefab;

        // Stateless PatternSO(플라이웨이트 패턴)의 동시성 오염 및 GC 할당을 완벽 방지하기 위한 인스턴스별 전용 버퍼
        public List<Vector3Int> PatternCellsBuffer { get; } = new List<Vector3Int>(25);
        public List<Vector3> PatternDirectionsBuffer { get; } = new List<Vector3>(8);

        public event Action OnDamaged;

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

        // 타일 이동 중 인터럽트 위치 정합성 플래그
        private bool _isMovingToTile;
        private Vector3 _currentDestinationWorldPos;

        // 비주얼 & 경고 UI 풀링 관리
        private SpriteRenderer[] _renderers;
        private EntityCombat _playerCombat;
        private readonly List<AttackWarningUI> _activeWarnings = new List<AttackWarningUI>(16);
        private readonly List<LineRenderer> _aimLinePool = new List<LineRenderer>(4);
        private static Material _sharedAimLineMaterial;

        private void Awake()
        {
            Combat = GetComponent<EntityCombat>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        public void Initialize(MonsterDataSO data)
        {
            _monsterData = data;
            InitializeStatsFromData();
        }

        private void InitializeStatsFromData()
        {
            if (_monsterData == null) return;
            CurrentHp = _monsterData.hp;
            CurrentShield = _monsterData.HasTrait("SHIELD") ? Mathf.Max(1, _monsterData.hp / 2) : 0;
        }

        private void Start()
        {
            Grid = FindObjectOfType<Grid>();
            Player = FindObjectOfType<PlayerController>();

            InitializeStatsFromData();

            if (Grid != null && TileSystem.Instance != null)
            {
                Vector3Int startCell = GetCurrentCell();
                TileSystem.Instance.RegisterInitialTile(startCell, gameObject);
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
            UnsubscribePlayerEvents();
            ClearActiveWarnings();
            HideAimLines();

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UnregisterMonster(this);
            }

            if (Grid != null && TileSystem.Instance != null)
            {
                TileSystem.Instance.ReleaseTile(GetCurrentCell(), gameObject);
            }
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
                    ? MonsterLocomotion.GetChebyshevDistance(GetCurrentCell(), GetPlayerCell())
                    : 999;

                bool wasAggroed = _isAggroed;
                bool isCurrentlyAggroed = EvaluateAggroState(gridDist);

                // 비인식(Patrol) -> 어그로(Aggro) 최초 진입 시 '!' 팝업과 함께 0.25초 인지 반응 딜레이 부여
                if (!wasAggroed && isCurrentlyAggroed)
                {
                    yield return new WaitForSeconds(0.25f);
                    if (_isDead || _isStunned || _isEvading) continue;
                    gridDist = Player != null
                        ? MonsterLocomotion.GetChebyshevDistance(GetCurrentCell(), GetPlayerCell())
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

                            yield return StartCoroutine(selectedPattern.ExecuteRoutine(this));

                            _isExecutingPattern = false;
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
                            yield return StartCoroutine(ExecuteFallbackMeleeRoutine());
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
                yield return StartCoroutine(MonsterLocomotion.ExecuteMoveStepRoutine(this));

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

        private IEnumerator ExecuteFallbackMeleeRoutine()
        {
            Vector3Int targetCell = GetPlayerCell();
            Vector3 cellWorldPos = Grid.GetCellCenterWorld(targetCell);
            float telegraph = Mathf.Max(0.2f, _monsterData.telegraphTime * _monsterData.GetEffectiveTelegraphMultiplier());

            SpawnWarning(cellWorldPos, telegraph);
            yield return new WaitForSeconds(telegraph);

            DealDamageToPlayerAtCell(targetCell, _monsterData.atk, (cellWorldPos - transform.position).normalized, false);
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

            if (_monsterData.movementStyle != MonsterMovementStyle.AgileFlank || _monsterData.evasiveStepCooldown <= 0f)
            {
                return;
            }

            if (Time.time < _nextEvasiveStepTime)
            {
                return;
            }

            int gridDist = MonsterLocomotion.GetChebyshevDistance(GetCurrentCell(), GetPlayerCell());
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
            if (targetCell != currentCell && TileSystem.Instance.TryOccupyTile(targetCell, gameObject, currentCell))
            {
                CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                    transform.position, "EVADE", new Color(0.25f, 0.95f, 1f, 1f), 4.2f);
                Vector3 endPos = Grid.GetCellCenterWorld(targetCell);
                yield return StartCoroutine(MoveToTileRoutine(endPos, 0.14f, 0.35f));
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

                if (chosenCell != currentCell && TileSystem.Instance.TryOccupyTile(chosenCell, gameObject, currentCell))
                {
                    CombatFeedbackSystem.EnsureInstance().SpawnFloatingStatusText(
                        transform.position, "ESCAPE", new Color(0.8f, 0.5f, 1f, 1f), 4.2f);
                    Vector3 endPos = Grid.GetCellCenterWorld(chosenCell);
                    int actualDist = MonsterLocomotion.GetManhattanDistance(currentCell, chosenCell);
                    float moveDuration = actualDist > 1 ? 0.22f : 0.16f;
                    yield return StartCoroutine(MoveToTileRoutine(endPos, moveDuration, 0.4f));
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

            // 이동 도중 피격/기절 등으로 인터럽트가 발생했다면, 공중에 뜨거나 중간 타일에 걸치지 않도록 목적지 좌표로 즉시 정렬
            if (_isMovingToTile)
            {
                transform.position = _currentDestinationWorldPos;
                _isMovingToTile = false;
            }

            _isExecutingPattern = false;
            ClearActiveWarnings();
            HideAimLines();

            _interruptCoroutine = StartCoroutine(RunInterruptAndResumeBehavior(routine));
        }

        private IEnumerator RunInterruptAndResumeBehavior(IEnumerator interruptRoutine)
        {
            yield return StartCoroutine(interruptRoutine);
            _interruptCoroutine = null;
            if (!_isDead)
            {
                _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
            }
        }

        // =====================================================================
        // 타일 이동, 전투 판정 및 경고 UI / 레이저 조준선 API
        // =====================================================================

        public Vector3Int GetCurrentCell()
        {
            if (Grid == null) return Vector3Int.zero;
            Vector3Int cell = Grid.WorldToCell(transform.position);
            cell.z = 0;
            return cell;
        }

        public Vector3Int GetPlayerCell()
        {
            if (Grid == null || Player == null) return Vector3Int.zero;
            Vector3Int cell = Grid.WorldToCell(Player.transform.position);
            cell.z = 0;
            return cell;
        }

        public IEnumerator MoveToTileRoutine(Vector3 endPos, float customDuration = -1f, float hopHeight = 0.5f)
        {
            if (FogOfWarSystem.Instance != null && Grid != null)
            {
                Vector3Int destCell = Grid.WorldToCell(endPos);
                destCell.z = 0;
                if (FogOfWarSystem.Instance.IsCellVisible(destCell))
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

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 lerpedPos = Vector3.Lerp(startPos, endPos, t);
                lerpedPos.y += Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = lerpedPos;
                yield return null;
            }
            transform.position = endPos;
            _isMovingToTile = false;

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

            Combat.PerformMeleeAttack(
                Player.gameObject,
                _monsterData != null ? _monsterData.speed : 2f,
                damage,
                hitDir,
                null,
                null,
                0.5f,
                null,
                isHeavyHit
            );

            ApplyLifestealIfApplicable(damage);
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

        public void TakeDamage(int damage, Vector3 hitDirection = default, bool isHeavyHit = false)
        {
            if (_isDead) return;

            int remainingDamage = damage;
            bool hadShield = CurrentShield > 0;

            if (CurrentShield > 0)
            {
                int absorbed = Mathf.Min(CurrentShield, remainingDamage);
                CurrentShield -= absorbed;
                remainingDamage -= absorbed;
            }

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
                return;
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
                _monsterData.escapeOnHitCooldown > 0f &&
                Time.time >= _nextEscapeOnHitTime)
            {
                _nextEscapeOnHitTime = Time.time + _monsterData.escapeOnHitCooldown;
                TriggerInterruptRoutine(RangedEscapeOnHitRoutine());
            }
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

            ClearActiveWarnings();
            HideAimLines();
            if (_behaviorCoroutine != null) StopCoroutine(_behaviorCoroutine);
            if (_interruptCoroutine != null) StopCoroutine(_interruptCoroutine);

            if (_monsterData != null && _monsterData.HasTrait("EXPLOSIVE") && Grid != null)
            {
                StartCoroutine(ExplosiveDeathRoutine());
                return;
            }

            FinalizeDeath();
        }

        private IEnumerator ExplosiveDeathRoutine()
        {
            Vector3Int centerCell = GetCurrentCell();
            float fuseTime = 0.75f;
            List<Vector3Int> blastCells = new List<Vector3Int>(9);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector3Int c = centerCell + new Vector3Int(x, y, 0);
                    blastCells.Add(c);
                    SpawnWarning(Grid.GetCellCenterWorld(c), fuseTime);
                }
            }

            yield return new WaitForSeconds(fuseTime);

            int explosionDmg = _monsterData != null ? _monsterData.atk * 2 : 15;
            DealDamageToPlayerIfInCells(blastCells, explosionDmg, Vector3.up, true);
            FinalizeDeath();
        }

        private void FinalizeDeath()
        {
            int dropPoolId = _monsterData != null ? _monsterData.dropPoolID : 0;
            GlobalEvents.OnMonsterDied?.Invoke(transform.position, dropPoolId);
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            if (_monsterData == null || _isDead) return;
            CurrentHp = Mathf.Min(_monsterData.hp, CurrentHp + amount);
        }

        public void SetVisibleByFog(bool isVisible)
        {
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
