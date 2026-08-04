// File: Assets/Scripts/Gameplay/Combat/MonsterHealth.cs
using System;
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.UI; // TooltipUI 접근용
using DimensionLooter.Managers;

namespace DimensionLooter.Gameplay.Combat
{
    /// <summary>
    /// 몬스터의 체력을 관리하며, IDamageable과 IInteractable을 구현합니다.
    /// ZOC(Zone of Control) 및 스텔스 어그로 로직이 추가되었습니다.
    /// </summary>
    public class MonsterHealth : MonoBehaviour, IDamageable, IInteractable
    {
        [Header("Stats")]
        [SerializeField] private DimensionLooter.Data.MonsterData _data;

        [Header("ZOC Settings")]
        public float zocRadius = 2.5f;
        public float warnRadius = 4.5f;

        private int _currentHealth;
        private LineRenderer _zocLine;
        private TextMesh _stealthText;
        private PlayerController _player;

        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _data != null ? _data.MaxHealth : 20;
        public int AttackPower => _data != null ? _data.AttackPower : 5;
        public float Speed => _data != null ? _data.AttackSpeed : 40f;
        public DimensionLooter.Data.MonsterData Data => _data;

        public event Action<int, int> OnHealthChanged;
        public event Action<GameObject> OnDied;

        private void Start()
        {
            if (_currentHealth <= 0 && _data != null) Init(_data);
            else if (_currentHealth <= 0)
                Init(new DimensionLooter.Data.MonsterData { MaxHealth = 20, AttackPower = 5, AttackSpeed = 40f, Name = "더미 몬스터" });
            
            InitZOCUI();
        }

        private void Update()
        {
            if (!IsAlive)
            {
                if (_stealthText != null) _stealthText.text = "";
                return;
            }

            if (_player == null) _player = FindObjectOfType<PlayerController>();
            if (_player == null) return;

            float dist = Vector2.Distance(transform.position, _player.transform.position);
            if (dist <= zocRadius)
            {
                _stealthText.text = "!";
                _stealthText.color = Color.red;
            }
            else if (dist <= warnRadius)
            {
                _stealthText.text = "?";
                _stealthText.color = Color.yellow;
            }
            else
            {
                _stealthText.text = "";
            }
        }

        private void InitZOCUI()
        {
            // ZOC 원형 라인 렌더러
            GameObject lrObj = new GameObject("ZOCLine");
            lrObj.transform.SetParent(transform);
            lrObj.transform.localPosition = Vector3.zero;
            _zocLine = lrObj.AddComponent<LineRenderer>();
            _zocLine.positionCount = 51;
            _zocLine.startWidth = 0.05f;
            _zocLine.endWidth = 0.05f;
            _zocLine.material = new Material(Shader.Find("Sprites/Default"));
            _zocLine.startColor = new Color(1, 0, 0, 0.5f);
            _zocLine.endColor = new Color(1, 0, 0, 0.5f);
            _zocLine.useWorldSpace = false;
            
            float angle = 0f;
            for (int i = 0; i < 51; i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * zocRadius;
                float y = Mathf.Cos(Mathf.Deg2Rad * angle) * zocRadius;
                _zocLine.SetPosition(i, new Vector3(x, y, 0));
                angle += (360f / 50f);
            }
            _zocLine.enabled = false;

            // 스텔스 어그로 텍스트 (? / !)
            GameObject txtObj = new GameObject("StealthText");
            txtObj.transform.SetParent(transform);
            txtObj.transform.localPosition = new Vector3(0, 1.2f, 0);
            _stealthText = txtObj.AddComponent<TextMesh>();
            _stealthText.characterSize = 0.25f;
            _stealthText.anchor = TextAnchor.MiddleCenter;
            _stealthText.alignment = TextAlignment.Center;
            _stealthText.text = "";
            
            // TextMesh sorting layer 보정
            var mr = txtObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 50;
        }

        public void Init(DimensionLooter.Data.MonsterData data)
        {
            _data = data;
            _currentHealth = MaxHealth;
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        public void Init(int maxHp)
        {
            if (_data == null) _data = new DimensionLooter.Data.MonsterData { MaxHealth = maxHp, AttackPower = 5, AttackSpeed = 40f };
            else _data.MaxHealth = maxHp;
            _currentHealth = maxHp;
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Clamp(_currentHealth - amount, 0, MaxHealth);
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

            if (!IsAlive) OnDied?.Invoke(gameObject);
        }

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[MonsterHealth] 전투 팝업 오픈! ZOC 충돌: {_data?.Name}");
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.BeginCombat(this);
            }
        }

        private void OnMouseEnter()
        {
            if (_zocLine != null) _zocLine.enabled = true;
            if (TooltipUI.Instance != null && IsAlive)
            {
                string monsterName = _data != null && !string.IsNullOrEmpty(_data.Name) ? _data.Name : "미지의 몬스터";
                string stats = $"[{monsterName}]\n체력: {_currentHealth} / {MaxHealth}";
                TooltipUI.Instance.Show(stats);
            }
        }

        private void OnMouseExit()
        {
            if (_zocLine != null) _zocLine.enabled = false;
            if (TooltipUI.Instance != null)
            {
                TooltipUI.Instance.Hide();
            }
        }
    }
}
