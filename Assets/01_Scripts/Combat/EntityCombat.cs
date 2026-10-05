using UnityEngine;
using System;
using System.Collections;
using BagBack100.Player;

namespace BagBack100.Combat
{
    public class EntityCombat : MonoBehaviour
    {
        public event Action<Vector3> OnMeleeAttack;
        public event Action<Vector3> OnRangedAttack;

        public bool IsAttackOnCooldown { get; private set; } = false;

        public void PerformMeleeAttack(GameObject target, float attackSpeed, int damage, Vector3 direction, GameObject meleePrefab = null, Transform spawnPoint = null, float offset = 0.5f, Vector3? scale = null)
        {
            if (IsAttackOnCooldown) return;
            
            OnMeleeAttack?.Invoke(direction);
            
            // 검(근접 무기) 애니메이션 프리팹 생성 및 회전
            if (meleePrefab != null && spawnPoint != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Vector3 spawnPos = spawnPoint.position + direction.normalized * offset;
                var slashObj = Instantiate(meleePrefab, spawnPos, Quaternion.Euler(0, 0, angle));
                
                // 스케일 조정 (기본값 설정)
                if (scale.HasValue)
                {
                    slashObj.transform.localScale = scale.Value;
                }

                // 애니메이터 속도 동기화 (기본 클립 길이가 1초라고 가정)
                var animator = slashObj.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.speed = attackSpeed;
                }

                Destroy(slashObj, 1f / attackSpeed);
            }
            
            if (target != null)
            {
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

        public event Action OnAttackFinished;

        private IEnumerator CooldownRoutine(float duration)
        {
            IsAttackOnCooldown = true;
            yield return new WaitForSeconds(duration);
            IsAttackOnCooldown = false;
            OnAttackFinished?.Invoke();
        }
    }
}
