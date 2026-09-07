using UnityEngine;
using System;
using System.Collections;
using DimensionLooter.Player;

namespace DimensionLooter.Combat
{
    public class EntityCombat : MonoBehaviour
    {
        public event Action<Vector3> OnMeleeAttack;
        public event Action<Vector3> OnRangedAttack;

        public bool IsAttackOnCooldown { get; private set; } = false;

        public void PerformMeleeAttack(GameObject target, float attackSpeed, int damage, Vector3 direction)
        {
            if (IsAttackOnCooldown) return;
            
            OnMeleeAttack?.Invoke(direction);
            
            var targetMonster = target.GetComponent<MonsterController>();
            if (targetMonster != null)
            {
                targetMonster.TakeDamage(damage);
            }
            else
            {
                var targetPlayer = target.GetComponent<PlayerStatManager>();
                if (targetPlayer != null)
                {
                    targetPlayer.TakeDamage(damage);
                }
            }
            
            StartCoroutine(CooldownRoutine(1f / attackSpeed));
        }

        public void PerformRangedAttack(GameObject projectilePrefab, Transform spawnPoint, float attackSpeed, int damage, Vector3 direction)
        {
            if (IsAttackOnCooldown) return;
            
            OnRangedAttack?.Invoke(direction);
            
            if (projectilePrefab != null && spawnPoint != null)
            {
                // 2D 회전
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                var proj = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.Euler(0, 0, angle));
                
                var projComponent = proj.GetComponent<Projectile>();
                if (projComponent != null)
                {
                    projComponent.Initialize(direction, damage);
                }
            }
            
            StartCoroutine(CooldownRoutine(1f / attackSpeed));
        }

        private IEnumerator CooldownRoutine(float duration)
        {
            IsAttackOnCooldown = true;
            yield return new WaitForSeconds(duration);
            IsAttackOnCooldown = false;
        }
    }
}
