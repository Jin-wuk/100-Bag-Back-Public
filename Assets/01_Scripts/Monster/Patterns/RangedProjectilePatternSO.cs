using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Player;

namespace BagBack100.Monster
{
    /// <summary>
    /// 원거리 직사 투사체 패턴 레고 블록 (단발 사격, 이동 예측 사격 Aim Leading, 부채꼴 다중 확산탄 모두 조립 가능).
    /// 바닥 타일 장판 대신, 자유 각도로 회전 가능한 얇은 레이저 조준선(LineRenderer)으로 전조를 표시합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_RangedProjectile", menuName = "BagBack100/Monster/Patterns/Ranged Projectile")]
    public class RangedProjectilePatternSO : MonsterPatternSO
    {
        [Header("투사체 및 탄막 설정")]
        [Tooltip("발사할 투사체 프리팹 (비워두면 MonsterController의 기본 투사체 프리팹 사용)")]
        public GameObject projectilePrefab;
        [Tooltip("동시에 발사할 탄환 개수 (1 = 단발 조준샷, 3 = 3갈래 부채꼴 확산탄)")]
        [Range(1, 7)]
        public int projectileCount = 1;
        [Tooltip("다중 탄환 발사 시 부채꼴 전체 확산 각도(도)")]
        [Range(0f, 120f)]
        public float spreadAngle = 35f;

        [Header("예측 사격 및 조준선 전조 설정")]
        [Tooltip("체크 시 플레이어의 현재 이동 방향 앞쪽 타일을 예측하여 조준(Aim Leading)합니다.")]
        public bool useAimLeading = false;
        [Tooltip("예측 사격 시 플레이어 진행 방향으로 몇 칸 앞을 노릴지 설정")]
        [Range(0.5f, 3f)]
        public float leadPredictTiles = 1.2f;
        [Tooltip("발사 전 telegraphDuration 동안 얇은 레이저 조준선을 표시할지 여부")]
        public bool showLaserAimLine = true;

        private readonly List<Vector3> _aimDirectionsBuffer = new List<Vector3>(8);

        public override IEnumerator ExecuteRoutine(MonsterController monster)
        {
            if (monster == null || monster.Player == null)
            {
                yield break;
            }

            // 1. 목표 지점 계산 (일반 조준 vs 이동 예측 조준)
            Vector3 targetWorldPos = monster.Player.transform.position;
            if (useAimLeading)
            {
                PlayerMovement playerMovement = monster.Player.GetComponent<PlayerMovement>();
                if (playerMovement != null && playerMovement.IsMoving)
                {
                    Vector3 moveDir = (playerMovement.CurrentTargetTileWorldPos - monster.Player.transform.position);
                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                        float cellSize = monster.Grid != null ? monster.Grid.cellSize.x : 1f;
                        targetWorldPos += moveDir.normalized * (cellSize * leadPredictTiles);
                    }
                }
            }

            Vector3 baseAimDir = (targetWorldPos - monster.transform.position);
            baseAimDir = baseAimDir.sqrMagnitude > 0.001f ? baseAimDir.normalized : Vector3.down;

            // 2. 탄환 개수 및 확산 각도에 따른 발사 방향 벡터 산출 (45도가 아닌 임의 각도도 완벽 지원)
            _aimDirectionsBuffer.Clear();
            float baseAngle = Mathf.Atan2(baseAimDir.y, baseAimDir.x) * Mathf.Rad2Deg;

            if (projectileCount <= 1)
            {
                _aimDirectionsBuffer.Add(baseAimDir);
            }
            else
            {
                float halfSpread = spreadAngle * 0.5f;
                float stepAngle = spreadAngle / (projectileCount - 1);
                for (int i = 0; i < projectileCount; i++)
                {
                    float angleDeg = (baseAngle - halfSpread) + (stepAngle * i);
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    _aimDirectionsBuffer.Add(new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f).normalized);
                }
            }

            // 3. 전조(Telegraph): 바닥 타일 장판 대신 얇은 레이저 조준선 표시
            float telegraph = GetEffectiveTelegraphDuration(monster);
            if (showLaserAimLine)
            {
                float lineLength = Mathf.Max(4f, maxRange + 1f);
                monster.ShowAimLines(monster.transform.position, _aimDirectionsBuffer, lineLength);
            }

            yield return new WaitForSeconds(telegraph);

            monster.HideAimLines();

            // 4. 투사체 일제 발사
            int damage = CalculateDamage(monster);
            for (int i = 0; i < _aimDirectionsBuffer.Count; i++)
            {
                monster.SpawnMonsterProjectile(projectilePrefab, _aimDirectionsBuffer[i], damage);
            }
        }
    }
}
