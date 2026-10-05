using UnityEngine;
using System.Collections;
using BagBack100.Data.Models;
using BagBack100.Player;
using BagBack100.Core;

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
            
            ChangeState(new MeleeIdleState());
            _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
        }
        
        private void OnDestroy()
        {
            if (Grid != null && TileSystem.Instance != null)
            {
                TileSystem.Instance.ReleaseTile(Grid.WorldToCell(transform.position), gameObject);
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
        }

        public event System.Action OnDamaged;

        public void TakeDamage(int damage)
        {
            CurrentHp -= damage;
            Debug.Log($"[Monster] {_monsterData.monsterName} 가 {damage} 피해를 입음! 남은 HP: {CurrentHp}");
            
            OnDamaged?.Invoke();
            
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
