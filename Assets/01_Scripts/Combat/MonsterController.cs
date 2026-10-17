using UnityEngine;
using System.Collections;
using BagBack100.Data.Models;
using BagBack100.Player;
using BagBack100.Core;
using BagBack100.Dungeon;

namespace BagBack100.Combat
{
    [RequireComponent(typeof(EntityCombat))]
    public class MonsterController : MonoBehaviour
    {
        [SerializeField] private MonsterDataSO _monsterData;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [SerializeField] private AttackWarningUI _warningPrefab;

        public MonsterDataSO Data => _monsterData;
        public int CurrentHp { get; private set; }
        public int InstanceID => gameObject.GetInstanceID();

        public EntityCombat Combat { get; private set; }
        public Grid Grid { get; private set; }
        public PlayerController Player { get; private set; }
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;
        public AttackWarningUI WarningPrefab => _warningPrefab;

        private IMonsterState _currentState;
        private Coroutine _behaviorCoroutine;
        private SpriteRenderer[] _renderers;
        private readonly System.Collections.Generic.List<AttackWarningUI> _activeWarnings = new System.Collections.Generic.List<AttackWarningUI>();

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        public AttackWarningUI SpawnWarning(Vector3 worldPos, float duration)
        {
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

        public void Initialize(MonsterDataSO data)
        {
            _monsterData = data;
            CurrentHp = _monsterData.hp;
        }

        private void Start()
        {
            Combat = GetComponent<EntityCombat>();
            Grid = FindObjectOfType<Grid>();
            Player = FindObjectOfType<PlayerController>();

            if (_monsterData != null)
            {
                CurrentHp = _monsterData.hp;
            }
            
            if (Grid != null && TileSystem.Instance != null)
            {
                TileSystem.Instance.RegisterInitialTile(Grid.WorldToCell(transform.position), gameObject);
            }

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RegisterMonster(this);
            }
            
            ChangeState(new MeleeIdleState());
            _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
        }
        
        private void OnDestroy()
        {
            ClearActiveWarnings();

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UnregisterMonster(this);
            }

            if (Grid != null && TileSystem.Instance != null)
            {
                TileSystem.Instance.ReleaseTile(Grid.WorldToCell(transform.position), gameObject);
            }
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

        public void ChangeState(IMonsterState newState)
        {
            if (_currentState != null)
            {
                _currentState.Exit(this);
            }
            _currentState = newState;
            if (_currentState != null)
            {
                _currentState.Enter(this);
            }
        }

        private IEnumerator BehaviorRoutine()
        {
            while (true)
            {
                if (_currentState != null)
                {
                    yield return StartCoroutine(_currentState.ExecuteRoutine(this));
                }
                else
                {
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }

        public IEnumerator MoveToTileRoutine(Vector3 endPos)
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

            if (FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.UpdateSingleMonsterVisibility(this);
            }
        }

        public event System.Action OnDamaged;
        private Coroutine _hitReactionCoroutine;

        public void TakeDamage(int damage, Vector3 hitDirection = default, bool isHeavyHit = false)
        {
            CurrentHp -= damage;
            Debug.Log($"[Monster] {_monsterData.monsterName} 가 {damage} 피해를 입음! 남은 HP: {CurrentHp}");
            
            GlobalEvents.OnEntityDamaged?.Invoke(transform.position, damage, isHeavyHit, false);
            OnDamaged?.Invoke();
            
            if (CurrentHp <= 0) 
            {
                CurrentHp = 0;
                Debug.Log($"[Monster] {_monsterData.monsterName} 처치됨!");
                Die();
                return;
            }

            if (_hitReactionCoroutine != null) StopCoroutine(_hitReactionCoroutine);
            _hitReactionCoroutine = StartCoroutine(HitReactionRoutine(hitDirection, isHeavyHit));
        }

        private IEnumerator HitReactionRoutine(Vector3 hitDirection, bool isHeavyHit)
        {
            SpriteRenderer primaryRenderer = (_renderers != null && _renderers.Length > 0) ? _renderers[0] : GetComponentInChildren<SpriteRenderer>();
            if (primaryRenderer == null) yield break;

            Transform visualTr = primaryRenderer.transform;
            Color originalColor = Color.white;
            primaryRenderer.color = isHeavyHit ? new Color(1f, 0.35f, 0.1f, 1f) : Color.red;

            // 스프라이트가 루트 자신이 아닐 경우 로컬 오프셋 펀치, 루트 자신일 경우 스케일 스쿼시만 수행하여 그리드 좌표 보호
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
            primaryRenderer.color = originalColor;
            _hitReactionCoroutine = null;
        }

        private void Die()
        {
            ClearActiveWarnings();
            if (_behaviorCoroutine != null) StopCoroutine(_behaviorCoroutine);
            GlobalEvents.OnMonsterDied?.Invoke(transform.position, _monsterData.dropPoolID);
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            CurrentHp += amount;
            if (CurrentHp > _monsterData.hp) CurrentHp = _monsterData.hp;
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
