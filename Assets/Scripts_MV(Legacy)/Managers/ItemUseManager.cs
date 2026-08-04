// File: Assets/Scripts/Managers/ItemUseManager.cs
using UnityEngine;
using System.Collections;
using DimensionLooter.Core;
using DimensionLooter.Data;
using DimensionLooter.Gameplay.Combat;

namespace DimensionLooter.Managers
{
    public class ItemUseManager : MonoBehaviour
    {
        private static ItemUseManager _instance;
        public static ItemUseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ItemUseManager");
                    _instance = go.AddComponent<ItemUseManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private bool _isPotionOnCooldown = false;
        public float PotionCooldown { get; private set; } = 5f;
        
        public bool IsReturning { get; private set; } = false;
        private float _returnTimer = 0f;

        public bool TryUseItem(ItemInstance itemInst)
        {
            var data = itemInst.Data;
            if (data.Type != Define.ItemType.Consumable) return false;

            // 귀환석 (ID 하드코딩)
            if (data.Id == "ITM_001")
            {
                if (IsReturning) return false;
                Debug.Log("[ItemUseManager] 귀환석 사용! 카운트다운 시작.");
                StartCoroutine(ReturnCountdownRoutine());
                return true;
            }

            if (data.HealHpAmount > 0)
            {
                return TryUsePotion(data.HealHpAmount, true);
            }
            else if (data.HealMpAmount > 0)
            {
                return TryUsePotion(data.HealMpAmount, false);
            }

            return false;
        }

        private bool TryUsePotion(int amount, bool isHp)
        {
            bool inCombat = CombatManager.Instance != null && CombatManager.Instance.IsInCombat;
            
            if (inCombat)
            {
                if (_isPotionOnCooldown)
                {
                    Debug.Log("[ItemUseManager] 포션 쿨타임 중!");
                    return false;
                }
                
                StartCoroutine(PotionCooldownRoutine());
                StartCoroutine(HealOverTimeRoutine(amount, 3f, isHp)); // 3초에 걸쳐 회복
                return true;
            }
            else
            {
                // 즉시 회복
                var player = FindObjectOfType<PlayerStats>();
                if (player != null)
                {
                    if (isHp) player.Heal(amount);
                    else player.RestoreMana(amount);
                    return true;
                }
            }
            return false;
        }

        private IEnumerator PotionCooldownRoutine()
        {
            _isPotionOnCooldown = true;
            yield return new WaitForSeconds(PotionCooldown);
            _isPotionOnCooldown = false;
        }

        private IEnumerator HealOverTimeRoutine(int totalAmount, float duration, bool isHp)
        {
            var player = FindObjectOfType<PlayerStats>();
            if (player == null) yield break;

            int ticks = 3; // 3틱으로 분할
            float interval = duration / ticks;
            int amountPerTick = totalAmount / ticks;

            for (int i = 0; i < ticks; i++)
            {
                yield return new WaitForSeconds(interval);
                if (player != null && player.IsAlive)
                {
                    if (isHp) player.Heal(amountPerTick);
                    else player.RestoreMana(amountPerTick);
                }
            }
        }

        private IEnumerator ReturnCountdownRoutine()
        {
            IsReturning = true;
            _returnTimer = 10f;
            
            while (_returnTimer > 0)
            {
                yield return null;
                _returnTimer -= Time.deltaTime;
            }
            
            IsReturning = false;
            
            ReturnSafely();
        }

        private void ReturnSafely()
        {
            Debug.Log("[ItemUseManager] 무사 귀환 탈출 성공!");

            // 가방 유지, 로비로 변경
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(Define.GameState.Lobby);
            }

            var player = FindObjectOfType<DimensionLooter.Gameplay.PlayerController>();
            if (player != null)
            {
                player.transform.position = new Vector3(0, 0, 0);
                var stats = player.GetComponent<DimensionLooter.Gameplay.Combat.PlayerStats>();
                if (stats != null)
                {
                    stats.Heal(9999);
                }
            }
            
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.ClosePanel();
            }
        }

        private void OnGUI()
        {
            if (IsReturning)
            {
                GUIStyle style = new GUIStyle();
                style.fontSize = 50;
                style.normal.textColor = Color.red;
                style.alignment = TextAnchor.UpperCenter;

                GUI.Label(new Rect(0, 50, Screen.width, 100), $"탈출까지: {_returnTimer:F1}초 남음", style);
            }
        }
    }
}
