using System;
using UnityEngine;
using DimensionLooter.Player;

namespace DimensionLooter.Player
{
    [RequireComponent(typeof(EquipmentManager))]
    public class PlayerStatManager : MonoBehaviour
    {
        public static event Action<PlayerStatManager> OnManagerCreated;
        public event Action<int, int, int, int, float, int> OnStatsChanged;

        [Header("Base Stats")]
        [SerializeField] private int _baseHp = 100;
        [SerializeField] private int _baseMp = 50;
        [SerializeField] private int _baseAttack = 10;
        [SerializeField] private int _baseDefense = 5;
        [SerializeField] private float _baseSpeed = 10f;
        [SerializeField] private int _baseRange = 1;

        public int CurrentHp { get; private set; }
        public int CurrentMp { get; private set; }
        public int CurrentAttack { get; private set; }
        public int CurrentDefense { get; private set; }
        public float CurrentSpeed { get; private set; }
        public int CurrentRange { get; private set; }

        private EquipmentManager _equipmentManager;

        private void Awake()
        {
            _equipmentManager = GetComponent<EquipmentManager>();
            OnManagerCreated?.Invoke(this);
        }

        private void OnEnable()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged += CalculateStats;
            }
        }

        private void OnDisable()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= CalculateStats;
            }
        }

        private void Start()
        {
            CalculateStats();
        }

        private void CalculateStats()
        {
            var eqStats = _equipmentManager.GetTotalStats();

            CurrentHp = _baseHp + eqStats.hp;
            CurrentMp = _baseMp + eqStats.mp;
            CurrentAttack = _baseAttack + eqStats.atk;
            CurrentDefense = _baseDefense + eqStats.def;
            CurrentSpeed = _baseSpeed + eqStats.speed;
            CurrentRange = _baseRange + eqStats.range;

            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }

        public void TakeDamage(int damage)
        {
            CurrentHp -= damage;
            if (CurrentHp < 0) CurrentHp = 0;
            Debug.Log($"[Player] {damage} 피해를 입음! 남은 HP: {CurrentHp}");
            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
            
            var renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                StartCoroutine(FlashRedRoutine(renderer));
            }
        }

        private System.Collections.IEnumerator FlashRedRoutine(SpriteRenderer renderer)
        {
            Color original = renderer.color;
            renderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            renderer.color = original;
        }

        public void Heal(int amount)
        {
            var eqStats = _equipmentManager != null ? _equipmentManager.GetTotalStats() : (hp:0, mp:0, atk:0, def:0, speed:0f, range:0);
            int maxHp = _baseHp + eqStats.hp;
            CurrentHp += amount;
            if (CurrentHp > maxHp) CurrentHp = maxHp;
            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }
    }
}
