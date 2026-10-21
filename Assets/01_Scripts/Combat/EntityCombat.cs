using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using BagBack100.Player;
using BagBack100.Core;
using BagBack100.Monster;

namespace BagBack100.Combat
{
    public class EntityCombat : MonoBehaviour
    {
        public event Action<Vector3> OnMeleeAttack;
        public event Action<Vector3> OnRangedAttack;
        public event Action OnAttackFinished;

        public bool IsAttackOnCooldown { get; private set; } = false;

        public void PerformMeleeAttack(
            GameObject target,
            float attackSpeed,
            int damage,
            Vector3 direction,
            GameObject meleePrefab = null,
            Transform spawnPoint = null,
            float offset = 0.5f,
            Vector3? scale = null,
            bool isHeavyHit = false,
            float shakeMultiplier = 1.0f,
            float hitStopMultiplier = 1.0f)
        {
            if (IsAttackOnCooldown) return;
            
            float safeAtkSpeed = Mathf.Max(0.1f, attackSpeed);
            Vector3 normDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            OnMeleeAttack?.Invoke(normDir);
            
            // 무기 이펙트 풀링 생성 및 회전
            if (spawnPoint != null)
            {
                float angle = Mathf.Atan2(normDir.y, normDir.x) * Mathf.Rad2Deg;
                Vector3 spawnPos = spawnPoint.position + normDir * offset;
                Vector3 finalScale = scale ?? Vector3.one;
                float effectDuration = 1f / safeAtkSpeed;

                CombatFeedbackSystem.EnsureInstance().SpawnPooledEffect(
                    meleePrefab,
                    spawnPos,
                    Quaternion.Euler(0f, 0f, angle),
                    finalScale,
                    effectDuration
                );
            }
            
            bool hitSuccess = ApplyDamageToTarget(target, damage, normDir, isHeavyHit);
            if (hitSuccess)
            {
                TriggerImpactFeedback(isHeavyHit, shakeMultiplier, hitStopMultiplier);
            }
            
            StartCoroutine(CooldownRoutine(1f / safeAtkSpeed));
        }

        /// <summary>
        /// 우클릭 차징 강공격 또는 다중 타일 히트박스 무기(ForwardLine2, WideSweep3)로 대상들을 동시에 타격합니다.
        /// </summary>
        public void PerformMultiTargetMeleeAttack(
            List<GameObject> targets,
            float attackSpeed,
            int damage,
            Vector3 direction,
            GameObject meleePrefab,
            Transform spawnPoint,
            float offset,
            Vector3 scale,
            bool isHeavyHit = true,
            float shakeMultiplier = 1.0f,
            float hitStopMultiplier = 1.0f)
        {
            if (IsAttackOnCooldown) return;

            float safeAtkSpeed = Mathf.Max(0.1f, attackSpeed);
            Vector3 normDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            OnMeleeAttack?.Invoke(normDir);

            if (spawnPoint != null)
            {
                float angle = Mathf.Atan2(normDir.y, normDir.x) * Mathf.Rad2Deg;
                Vector3 spawnPos = spawnPoint.position + normDir * offset;
                float effectDuration = 1f / safeAtkSpeed;

                CombatFeedbackSystem.EnsureInstance().SpawnPooledEffect(
                    meleePrefab,
                    spawnPos,
                    Quaternion.Euler(0f, 0f, angle),
                    scale,
                    effectDuration
                );
            }

            bool anyHit = false;
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (ApplyDamageToTarget(targets[i], damage, normDir, isHeavyHit))
                    {
                        anyHit = true;
                    }
                }
            }

            if (anyHit)
            {
                TriggerImpactFeedback(isHeavyHit, shakeMultiplier, hitStopMultiplier);
            }
            else if (isHeavyHit)
            {
                // 강공격은 허공에 휘둘러도 묵직한 미세 카메라 흔들림 부여
                GlobalEvents.OnCombatImpact?.Invoke(0.08f * shakeMultiplier, 0.1f, 0f);
            }

            StartCoroutine(CooldownRoutine(1f / safeAtkSpeed));
        }

        private bool ApplyDamageToTarget(GameObject target, int damage, Vector3 hitDir, bool isHeavyHit)
        {
            if (target == null) return false;

            var targetMonster = target.GetComponent<MonsterController>();
            if (targetMonster != null)
            {
                targetMonster.TakeDamage(damage, hitDir, isHeavyHit);
                return true;
            }

            var targetPlayer = target.GetComponent<PlayerStatManager>();
            if (targetPlayer != null)
            {
                targetPlayer.TakeDamage(damage);
                return true;
            }

            return false;
        }

        private void TriggerImpactFeedback(bool isHeavyHit, float shakeMult = 1.0f, float hitStopMult = 1.0f)
        {
            if (isHeavyHit)
            {
                GlobalEvents.OnCombatImpact?.Invoke(0.22f * shakeMult, 0.18f, 0.045f * hitStopMult);
            }
            else
            {
                GlobalEvents.OnCombatImpact?.Invoke(0.1f * shakeMult, 0.1f, 0.015f * hitStopMult);
            }
        }

        public void PerformRangedAttack(GameObject projectilePrefab, Transform spawnPoint, float attackSpeed, int damage, Vector3 direction)
        {
            if (IsAttackOnCooldown) return;
            
            float safeAtkSpeed = Mathf.Max(0.1f, attackSpeed);
            Vector3 normDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            OnRangedAttack?.Invoke(normDir);
            
            if (projectilePrefab != null && spawnPoint != null)
            {
                float angle = Mathf.Atan2(normDir.y, normDir.x) * Mathf.Rad2Deg;
                Projectile.Spawn(projectilePrefab, spawnPoint.position, Quaternion.Euler(0f, 0f, angle), normDir, damage, gameObject);
            }
            
            StartCoroutine(CooldownRoutine(1f / safeAtkSpeed));
        }

        private IEnumerator CooldownRoutine(float duration)
        {
            IsAttackOnCooldown = true;
            float elapsed = 0f;
            // Frame-Independent Combat: 히트스톱(Time.timeScale 변동)이나 프레임 드랍에 의해 모션 락 시간이 왜곡되지 않도록 unscaledDeltaTime 누적 및 Yield객체 GC Zero 달성
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            IsAttackOnCooldown = false;
            OnAttackFinished?.Invoke();
        }
    }
}
