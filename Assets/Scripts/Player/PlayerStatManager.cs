using System;
using UnityEngine;
using DimensionLooter.Player;

namespace DimensionLooter.Player
{
    [RequireComponent(typeof(EquipmentManager))]
    public class PlayerStatManager : MonoBehaviour
    {
        public static event Action<PlayerStatManager> OnManagerCreated;
        public event Action<int, int, int, int> OnStatsChanged;

        [Header("Base Stats")]
        [SerializeField] private int _baseHp = 100;
        [SerializeField] private int _baseMp = 50;
        [SerializeField] private int _baseAttack = 10;
        [SerializeField] private int _baseDefense = 5;

        public int CurrentHp { get; private set; }
        public int CurrentMp { get; private set; }
        public int CurrentAttack { get; private set; }
        public int CurrentDefense { get; private set; }

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

            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense);
        }
    }
}
