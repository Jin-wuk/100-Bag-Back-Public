using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "BagBack100/Data/Items/Weapon")]
    public class WeaponDataSO : EquipmentDataSO
    {
        [Header("Weapon Specific")]
        public float baseAttackSpeed = 1f; 
        public GameObject weaponPrefab; // 기본 무기 공격 이펙트 (1개만 연결 시 1~3타/차징에 자동 변형 재사용)

        [Header("Grid Hitbox & Impact Feel")]
        [Tooltip("무기 고유 그리드 피격 판정 형태 (SingleTile: 1칸, ForwardLine2: 전방 2칸 관통, WideSweep3: 전방 3칸 횡베기)")]
        public WeaponHitboxPattern hitboxPattern = WeaponHitboxPattern.SingleTile;
        [Tooltip("타격 시 카메라 흔들림(Screen Shake) 배율 (단검: 0.7, 기본검: 1.0, 대검: 1.45)")]
        [Range(0.3f, 2.5f)] public float shakeMultiplier = 1.0f;
        [Tooltip("타격 시 마이크로 역경직(Hitstop) 배율 (단검: 0.5, 기본검: 1.0, 대검: 1.5)")]
        [Range(0.2f, 2.5f)] public float hitStopMultiplier = 1.0f;

        [Header("Optional Combo & Charge Effects")]
        [Tooltip("타수별(1타, 2타, 3타) 개별 이펙트 프리팹 (비워두면 weaponPrefab을 반전/확대하여 자동 사용)")]
        public GameObject[] comboEffectPrefabs;
        [Tooltip("우클릭 차징 강공격 전용 이펙트 프리팹 (비워두면 weaponPrefab을 확대하여 사용)")]
        public GameObject chargedEffectPrefab;

        public GameObject GetComboEffectPrefab(int comboStep)
        {
            if (comboEffectPrefabs != null && comboStep >= 0 && comboStep < comboEffectPrefabs.Length && comboEffectPrefabs[comboStep] != null)
            {
                return comboEffectPrefabs[comboStep];
            }
            return weaponPrefab;
        }

        public GameObject GetChargedEffectPrefab()
        {
            return chargedEffectPrefab != null ? chargedEffectPrefab : weaponPrefab;
        }

        /// <summary>
        /// Zero-GC 기반으로 플레이어 셀과 조준 방향, 클릭한 타겟 셀을 기준으로 무기 고유 히트박스 타일 좌표들을 버퍼에 채웁니다.
        /// </summary>
        public void PopulateHitboxCells(Vector3Int originCell, Vector3Int primaryTargetCell, Vector3 facingDir, List<Vector3Int> outCells)
        {
            outCells.Clear();
            outCells.Add(primaryTargetCell);

            int dx = Mathf.RoundToInt(facingDir.x);
            int dy = Mathf.RoundToInt(facingDir.y);
            if (dx == 0 && dy == 0)
            {
                dx = Mathf.Clamp(primaryTargetCell.x - originCell.x, -1, 1);
                dy = Mathf.Clamp(primaryTargetCell.y - originCell.y, -1, 1);
                if (dx == 0 && dy == 0) dy = -1;
            }

            switch (hitboxPattern)
            {
                case WeaponHitboxPattern.ForwardLine2:
                    Vector3Int secondCell = new Vector3Int(primaryTargetCell.x + dx, primaryTargetCell.y + dy, 0);
                    if (!outCells.Contains(secondCell)) outCells.Add(secondCell);
                    break;

                case WeaponHitboxPattern.WideSweep3:
                    // 조준 벡터의 좌/우 수직 타일(횡베기 3칸) 산출
                    Vector3Int leftWing = new Vector3Int(primaryTargetCell.x - dy, primaryTargetCell.y + dx, 0);
                    Vector3Int rightWing = new Vector3Int(primaryTargetCell.x + dy, primaryTargetCell.y - dx, 0);
                    if (!outCells.Contains(leftWing)) outCells.Add(leftWing);
                    if (!outCells.Contains(rightWing)) outCells.Add(rightWing);
                    break;
            }
        }
    }
}
