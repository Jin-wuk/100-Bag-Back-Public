// File: Assets/Scripts/Managers/HeatManager.cs
using UnityEngine;
using System.Collections.Generic;

namespace DimensionLooter.Managers
{
    public class HeatManager : MonoBehaviour
    {
        private static HeatManager _instance;
        public static HeatManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<HeatManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("HeatManager");
                        _instance = go.AddComponent<HeatManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        public bool IsEnemyHpBoostActive { get; set; } = false;
        public bool IsEnemyAtkBoostActive { get; set; } = false;
        public bool IsNoHealingActive { get; set; } = false;

        public int CurrentHeatScore { get; private set; }
        public float RewardMultiplier { get; private set; }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void CalculateHeat()
        {
            CurrentHeatScore = 0;
            RewardMultiplier = 0f;

            if (IsEnemyHpBoostActive)
            {
                CurrentHeatScore += 10;
                RewardMultiplier += 0.10f;
            }

            if (IsEnemyAtkBoostActive)
            {
                CurrentHeatScore += 10;
                RewardMultiplier += 0.10f;
            }

            if (IsNoHealingActive)
            {
                CurrentHeatScore += 20;
                RewardMultiplier += 0.25f;
            }

            Debug.Log($"[HeatManager] 자율 위험도 산출 완료. 현재 Heat 점수: {CurrentHeatScore}, 보상 배율: x{1f + RewardMultiplier}");
        }

        // 로비에서 던전 진입(EnterDungeon) 시 호출될 메서드
        public void ApplyHeatMode()
        {
            CalculateHeat();
        }
    }
}
