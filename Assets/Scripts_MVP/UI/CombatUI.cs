// File: Assets/Scripts/UI/CombatUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Gameplay.Combat;

namespace DimensionLooter.UI
{
    /// <summary>
    /// 1대1 전투 시 화면 중앙을 덮는 팝업 UI 패널입니다. (탐험 뷰 클릭 차단)
    /// </summary>
    public class CombatUI : MonoBehaviour
    {
        [Header("Popup Canvas & Panels")]
        [Tooltip("전투 팝업 패널 전체 (평소엔 SetActive(false))")]
        [SerializeField] private GameObject _popupPanel;

        [Header("Player HUD (임시 연결)")]
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private Slider _playerHpBar;
        [SerializeField] private Slider _playerMpBar;
        
        [Header("Monster Info")]
        [SerializeField] private Slider _monsterHpBar;
        [SerializeField] private Slider _monsterCastBar;
        [SerializeField] private Text _monsterNameText;
        [SerializeField] private Text _monsterCastWarningText;

        [Header("ATB & Distance (MVP UI)")]
        [SerializeField] private Text _distanceText;
        [SerializeField] private Slider _playerAtbBar;
        [SerializeField] private Slider _monsterAtbBar;

        [Header("Skill UI Sync")]
        [SerializeField] private Button _weaponSkillBtn;
        [SerializeField] private Text _weaponSkillText;
        [SerializeField] private Button _armorSkillBtn;
        [SerializeField] private Text _armorSkillText;

        private MonsterHealth _currentMonster;

        private void Awake()
        {
            HideCombat(); // 시작 시 숨김

            // 인스펙터 onClick 연결 누락 방지용 강제 할당
            if (_weaponSkillBtn != null)
            {
                _weaponSkillBtn.onClick.RemoveAllListeners();
                _weaponSkillBtn.onClick.AddListener(OnWeaponSkillClicked);
            }
            if (_armorSkillBtn != null)
            {
                _armorSkillBtn.onClick.RemoveAllListeners();
                _armorSkillBtn.onClick.AddListener(OnArmorSkillClicked);
            }
        }

        private void OnEnable()
        {
            if (_playerStats != null)
            {
                _playerStats.OnHealthChanged += UpdatePlayerHealthBar;
                _playerStats.OnManaChanged += UpdatePlayerManaBar;
            }
        }

        private void Start()
        {
            if (DimensionLooter.Managers.CombatManager.Instance != null)
            {
                var cm = DimensionLooter.Managers.CombatManager.Instance;
                cm.OnCombatBegun += ShowCombat;
                cm.OnCombatEnded += HideCombat;
                cm.OnCombatStateUpdated += UpdateCombatState;
                cm.OnMonsterCastUpdated += UpdateMonsterCastBar;
            }
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
            {
                _playerStats.OnHealthChanged -= UpdatePlayerHealthBar;
                _playerStats.OnManaChanged -= UpdatePlayerManaBar;
            }
            if (_currentMonster != null) _currentMonster.OnHealthChanged -= UpdateMonsterHealthBar;

            if (DimensionLooter.Managers.CombatManager.Instance != null)
            {
                var cm = DimensionLooter.Managers.CombatManager.Instance;
                cm.OnCombatBegun -= ShowCombat;
                cm.OnCombatEnded -= HideCombat;
                cm.OnCombatStateUpdated -= UpdateCombatState;
                cm.OnMonsterCastUpdated -= UpdateMonsterCastBar;
            }
        }

        public void ShowCombat(MonsterHealth monster)
        {
            _currentMonster = monster;
            _currentMonster.OnHealthChanged += UpdateMonsterHealthBar;

            // UI 초기화
            if (_monsterNameText != null) _monsterNameText.text = monster.name;
            UpdateMonsterHealthBar(monster.CurrentHealth, monster.MaxHealth);
            UpdateMonsterCastBar(0f); // 초기화

            if (_playerStats != null)
            {
                UpdatePlayerHealthBar(_playerStats.CurrentHealth, _playerStats.MaxHealth);
                UpdatePlayerManaBar(_playerStats.CurrentMana, _playerStats.MaxMana);
            }

            // 팝업 열기 (Raycast Target이 켜져있어야 뒷면의 맵 클릭 이동이 차단됨)
            if (_popupPanel != null) _popupPanel.SetActive(true);

            RefreshSkillUI();
        }

        public void HideCombat()
        {
            if (_currentMonster != null)
            {
                _currentMonster.OnHealthChanged -= UpdateMonsterHealthBar;
                _currentMonster = null;
            }

            if (_popupPanel != null) _popupPanel.SetActive(false);
        }

        private void UpdatePlayerHealthBar(int currentHealth, int maxHealth)
        {
            if (_playerHpBar != null) _playerHpBar.value = (float)currentHealth / maxHealth;
        }

        private void UpdatePlayerManaBar(int currentMana, int maxMana)
        {
            if (_playerMpBar != null) _playerMpBar.value = (float)currentMana / maxMana;
        }

        private void UpdateMonsterHealthBar(int currentHealth, int maxHealth)
        {
            if (_monsterHpBar != null) _monsterHpBar.value = (float)currentHealth / maxHealth;
        }

        public void UpdateMonsterCastBar(float fillAmount)
        {
            if (_monsterCastBar != null)
            {
                bool isCasting = fillAmount > 0f;
                _monsterCastBar.gameObject.SetActive(isCasting);
                _monsterCastBar.value = fillAmount;
            }

            if (_monsterCastWarningText != null)
            {
                _monsterCastWarningText.gameObject.SetActive(fillAmount > 0f);
            }
        }

        public void UpdateCombatState(int distance, float playerGaugePercent, float monsterGaugePercent)
        {
            if (_distanceText != null)
            {
                _distanceText.text = $"Distance: {distance}";
            }

            if (_playerAtbBar != null)
            {
                _playerAtbBar.value = playerGaugePercent;
            }

            if (_monsterAtbBar != null)
            {
                _monsterAtbBar.value = monsterGaugePercent;
            }
        }

        public void RefreshSkillUI()
        {
            UpdateSkillButton(DimensionLooter.Core.Define.ItemType.Weapon, _weaponSkillBtn, _weaponSkillText);
            UpdateSkillButton(DimensionLooter.Core.Define.ItemType.Armor, _armorSkillBtn, _armorSkillText);
        }

        private void UpdateSkillButton(DimensionLooter.Core.Define.ItemType type, Button btn, Text txt)
        {
            if (btn == null || txt == null) return;
            
            var item = DimensionLooter.Managers.GearManager.Instance.GetEquippedItem(type);
            if (item != null && !string.IsNullOrEmpty(item.Instance.AssignedSkillId) && DimensionLooter.Managers.DataManager.Instance.SkillDict.TryGetValue(item.Instance.AssignedSkillId, out DimensionLooter.Data.SkillData skill))
            {
                btn.interactable = true;
                txt.text = $"{skill.Name}\n(MP {skill.MpCost})";
            }
            else
            {
                btn.interactable = false;
                txt.text = "No Skill";
            }
        }

        // 유니티 에디터 버튼 이벤트 할당용 헬퍼 메서드
        public void OnWeaponSkillClicked()
        {
            DimensionLooter.Managers.CombatManager.Instance.UseEquippedSkill(DimensionLooter.Core.Define.ItemType.Weapon);
        }

        public void OnArmorSkillClicked()
        {
            DimensionLooter.Managers.CombatManager.Instance.UseEquippedSkill(DimensionLooter.Core.Define.ItemType.Armor);
        }

        public void OnHelmetSkillClicked()
        {
            DimensionLooter.Managers.CombatManager.Instance.UseEquippedSkill(DimensionLooter.Core.Define.ItemType.Helmet);
        }
    }
}
