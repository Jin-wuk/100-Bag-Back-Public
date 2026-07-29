using UnityEngine;
using System.Collections.Generic;
using DimensionLooter.Data;
using DimensionLooter.Gameplay.Inventory;
using DimensionLooter.UI.Inventory;

namespace DimensionLooter.Managers
{
    public class LootManager : MonoBehaviour
    {
        private static LootManager _instance;
        public static LootManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<LootManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("LootManager");
                        _instance = go.AddComponent<LootManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void SpawnPouch(List<ItemData> items, Vector3 position)
        {
            if (items == null || items.Count == 0) return;

            // 1. 반경 1.0f 내에 이미 PouchInteractable이 있는지 탐색
            Collider2D[] colliders = Physics2D.OverlapCircleAll(position, 1.0f);
            PouchInteractable targetPouch = null;

            foreach (var col in colliders)
            {
                var pouch = col.GetComponent<PouchInteractable>();
                if (pouch != null)
                {
                    targetPouch = pouch;
                    break;
                }
            }

            if (targetPouch != null)
            {
                // 2. 종속 (Merge)
                targetPouch.AppendItems(items);
                Debug.Log($"[LootManager] 기존 파우치에 {items.Count}개의 아이템을 병합했습니다.");
            }
            else
            {
                // 3. 신규 스폰
                GameObject pouchPrefab = Resources.Load<GameObject>("Prefabs/Pouch");
                if (pouchPrefab != null)
                {
                    GameObject newPouch = Instantiate(pouchPrefab, position, Quaternion.identity);
                    var interactable = newPouch.GetComponent<PouchInteractable>();
                    if (interactable != null)
                    {
                        interactable.Init(items);
                    }
                    Debug.Log($"[LootManager] 새로운 파우치를 스폰했습니다. ({items.Count}개 아이템)");
                }
                else
                {
                    Debug.LogError("[LootManager] Pouch 프리팹을 찾을 수 없습니다!");
                }
            }
        }
    }
}
