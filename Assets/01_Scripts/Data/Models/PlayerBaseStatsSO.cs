using UnityEngine;

namespace BagBack100.Data.Models
{
    /// <summary>
    /// Role: 플레이어 기본 스탯(장비 보너스를 더하기 전 값) 정적 데이터. 던전 Player.prefab과 허브 Manager의 PlayerStatManager가 같은 에셋을 본다.
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerBaseStats", menuName = "BagBack100/Data/PlayerBaseStats")]
    public class PlayerBaseStatsSO : ScriptableObject
    {
        [SerializeField, Min(1)] private int _hp = 100;
        [SerializeField, Min(0)] private int _mp = 50;
        [SerializeField] private int _attack = 10;
        [SerializeField] private int _defense = 5;
        [Tooltip("이동 속도. 타일 이동 시간을 정한다")]
        [SerializeField, Min(0.1f)] private float _speed = 8f;
        [Tooltip("초당 공격 횟수. 무기에 고유 공속이 있으면 그 값을 쓴다")]
        [SerializeField, Min(0.1f)] private float _attackSpeed = 1.82f;
        [SerializeField, Min(0)] private int _range = 1;

        public int Hp => _hp;
        public int Mp => _mp;
        public int Attack => _attack;
        public int Defense => _defense;
        public float Speed => _speed;
        public float AttackSpeed => _attackSpeed;
        public int Range => _range;
    }
}
