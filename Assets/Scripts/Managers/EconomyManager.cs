// File: Assets/Scripts/Managers/EconomyManager.cs
using UnityEngine;

namespace DimensionLooter.Managers
{
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        public int CurrentGold { get; private set; }

        public event System.Action<int> OnGoldChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            CurrentGold += amount;
            OnGoldChanged?.Invoke(CurrentGold);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0 || CurrentGold < amount) return false;
            CurrentGold -= amount;
            OnGoldChanged?.Invoke(CurrentGold);
            return true;
        }
    }
}
