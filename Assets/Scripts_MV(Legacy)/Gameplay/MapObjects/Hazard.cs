using System.Collections;
using UnityEngine;
using DimensionLooter.Gameplay.Combat;

namespace DimensionLooter.Gameplay.MapObjects
{
    public enum HazardType
    {
        SpikeTrap,
        PoisonBog
    }

    /// <summary>
    /// 플레이어가 밟으면 데미지를 입는 함정입니다. (Trigger 방식)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        public HazardType type = HazardType.SpikeTrap;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<DimensionLooter.Gameplay.Combat.PlayerStats>() != null)
            {
                var playerStats = other.GetComponent<DimensionLooter.Gameplay.Combat.PlayerStats>();
                if (playerStats == null) playerStats = FindObjectOfType<DimensionLooter.Gameplay.Combat.PlayerStats>();
                if (playerStats == null) return;

                if (type == HazardType.SpikeTrap)
                {
                    // 즉발 데미지: 최대 체력의 10%
                    int damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.MaxHealth * 0.1f));
                    playerStats.TakeDamage(damage);
                    Debug.Log($"[Hazard] 가시 함정 발동! 플레이어에게 {damage} 데미지.");
                    Destroy(gameObject); // 일회성 적용 후 삭제
                }
                else if (type == HazardType.PoisonBog)
                {
                    // 초당 2데미지, 5초간 지속 (기획 수치 적용)
                    playerStats.ApplyPoison(2, 5);
                    Debug.Log($"[Hazard] 독 함정 밟음! 플레이어에게 중독 효과 적용.");
                    Destroy(gameObject); // 일회성 적용 후 삭제
                }
            }
        }
    }
}
