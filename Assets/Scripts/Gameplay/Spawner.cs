// File: Assets/Scripts/Gameplay/Spawner.cs
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Gameplay
{
    public enum SpawnerType
    {
        Monster,
        Gimmick,
        PlayerSpawn,
        PortalSpawn
    }

    [System.Serializable]
    public class SpawnWeight
    {
        public string entityId;
        public int weight;
    }

    /// <summary>
    /// 기획자가 청크 내부에 수작업으로 배치할 수 있는 스폰 포인트 컴포넌트입니다.
    /// DungeonManager에 의해 확률 및 가중치 계산을 거쳐 실제 오브젝트를 생성합니다.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        public SpawnerType type = SpawnerType.Monster;
        
        [Tooltip("이 스폰 포인트에서 무언가가 스폰될 확률 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float spawnProbability = 0.5f;

        [Tooltip("스폰될 후보군 및 가중치")]
        public List<SpawnWeight> spawnPool = new List<SpawnWeight>();

        // 에디터에서 식별하기 쉽도록 기즈모 표시
        private void OnDrawGizmos()
        {
            switch (type)
            {
                case SpawnerType.Monster: Gizmos.color = Color.red; break;
                case SpawnerType.Gimmick: Gizmos.color = Color.green; break;
                case SpawnerType.PlayerSpawn: Gizmos.color = Color.blue; break;
                case SpawnerType.PortalSpawn: Gizmos.color = Color.cyan; break;
            }
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        /// <summary>
        /// DungeonManager가 호출하여 스폰 로직을 실행합니다.
        /// </summary>
        public GameObject ExecuteSpawn()
        {
            if (Random.value > spawnProbability)
                return null; // 스폰 실패

            if (spawnPool == null || spawnPool.Count == 0)
                return null;

            int totalWeight = 0;
            foreach (var w in spawnPool) totalWeight += w.weight;

            if (totalWeight <= 0) return null;

            int randValue = Random.Range(0, totalWeight);
            string selectedId = spawnPool[0].entityId;

            int currentWeight = 0;
            foreach (var w in spawnPool)
            {
                currentWeight += w.weight;
                if (randValue < currentWeight)
                {
                    selectedId = w.entityId;
                    break;
                }
            }

            // 선택된 ID를 기반으로 프리팹 로드 및 스폰
            GameObject prefab = null;
            if (type == SpawnerType.Monster)
            {
                prefab = Resources.Load<GameObject>("Prefabs/Monster_Dummy");
            }
            else if (type == SpawnerType.Gimmick)
            {
                // 기믹은 selectedId와 동일한 이름의 프리팹을 찾아 로드 시도
                prefab = Resources.Load<GameObject>($"Prefabs/{selectedId}");
                if (prefab == null)
                {
                    Debug.LogWarning($"[Spawner] '{selectedId}' 프리팹을 찾을 수 없어 기본 ReturnPortal로 대체합니다.");
                    prefab = Resources.Load<GameObject>("Prefabs/ReturnPortal");
                }
            }
            else
            {
                prefab = Resources.Load<GameObject>("Prefabs/ReturnPortal"); 
            }

            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab, transform.position, Quaternion.identity);
                
                // 몬스터일 경우 DataManager에서 스탯을 찾아 주입
                if (type == SpawnerType.Monster)
                {
                    var monsterHealth = obj.GetComponent<DimensionLooter.Gameplay.Combat.MonsterHealth>();
                    if (monsterHealth != null && DimensionLooter.Managers.DataManager.Instance != null)
                    {
                        if (DimensionLooter.Managers.DataManager.Instance.MonsterDict.TryGetValue(selectedId, out var mData))
                        {
                            monsterHealth.Init(mData);
                        }
                    }
                }
                return obj;
            }
            else if (type == SpawnerType.Gimmick)
            {
                // 프리팹이 없을 때를 대비한 임시 더미 오브젝트 런타임 생성 로직
                Debug.Log($"[Spawner] '{selectedId}' 프리팹이 없어 임시 더미 오브젝트를 생성합니다.");
                
                // 화면에 보이도록 Quad로 생성
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Quad);
                dummy.name = selectedId;
                dummy.transform.position = transform.position;
                
                // 3D 콜라이더 제거 후 2D 콜라이더 부착
                Destroy(dummy.GetComponent<MeshCollider>());
                var col = dummy.AddComponent<BoxCollider2D>();
                
                var renderer = dummy.GetComponent<MeshRenderer>();
                
                if (selectedId.StartsWith("Hazard_"))
                {
                    col.isTrigger = true;
                    var hazard = dummy.AddComponent<DimensionLooter.Gameplay.MapObjects.Hazard>();
                    hazard.type = selectedId.Contains("Spike") ? DimensionLooter.Gameplay.MapObjects.HazardType.SpikeTrap : DimensionLooter.Gameplay.MapObjects.HazardType.PoisonBog;
                    renderer.material.color = hazard.type == DimensionLooter.Gameplay.MapObjects.HazardType.SpikeTrap ? Color.red : Color.green;
                }
                else if (selectedId.StartsWith("Chest_"))
                {
                    var chest = dummy.AddComponent<DimensionLooter.Gameplay.MapObjects.Chest>();
                    chest.rarity = DimensionLooter.Gameplay.MapObjects.ChestRarity.Normal;
                    renderer.material.color = Color.yellow;
                }
                else if (selectedId.StartsWith("Shrine_"))
                {
                    var shrine = dummy.AddComponent<DimensionLooter.Gameplay.MapObjects.Shrine>();
                    shrine.type = DimensionLooter.Gameplay.MapObjects.ShrineType.Healing;
                    renderer.material.color = Color.cyan;
                }
                
                return dummy;
            }

            return null;
        }
    }
}
