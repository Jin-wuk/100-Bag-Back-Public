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

        /// <summary>
        /// 몬스터 패턴 타격처럼 시전자 자신의 공격 쿨다운과 무관하게 즉시 적용되어야 하는 피해.
        /// 패턴은 MonsterController가 자체 쿨다운을 관리하므로, 여기서 EntityCombat 쿨다운에 막히면 데미지가 조용히 누락된다.
        /// </summary>
        /// <returns>대상에게 실제로 적용된 피해량 (대상이 없거나 이미 사망했으면 0)</returns>
        public int ApplyImmediateHit(GameObject target, int damage, Vector3 direction, bool isHeavyHit)
        {
            Vector3 normDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            int dealt = ApplyDamageToTargetAndGetDealt(target, damage, normDir, isHeavyHit);
            if (dealt > 0)
            {
                TriggerImpactFeedback(isHeavyHit);
            }
            return dealt;
        }

        private bool ApplyDamageToTarget(GameObject target, int damage, Vector3 hitDir, bool isHeavyHit)
        {
            return ApplyDamageToTargetAndGetDealt(target, damage, hitDir, isHeavyHit) > 0;
        }

        private int ApplyDamageToTargetAndGetDealt(GameObject target, int damage, Vector3 hitDir, bool isHeavyHit)
        {
            if (target == null) return 0;

            if (target.TryGetComponent<MonsterController>(out var targetMonster))
            {
                return targetMonster.TakeDamage(damage, hitDir, isHeavyHit);
            }

            if (target.TryGetComponent<PlayerStatManager>(out var targetPlayer))
            {
                return targetPlayer.TakeDamage(damage);
            }

            return 0;
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
                // 히트스톱 중에는 흐르되, 가방을 열어 일시정지한 동안에는 멈춘다 (몬스터만 멈춘 채 쿨다운을 소진하는 꼼수 차단)
                if (!GamePauseService.IsPaused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            IsAttackOnCooldown = false;
            OnAttackFinished?.Invoke();
        }
    }
}
