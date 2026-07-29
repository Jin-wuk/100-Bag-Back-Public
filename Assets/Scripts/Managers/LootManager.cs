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

        private Dictionary<string, PouchInteractable> _activePouches = new Dictionary<string, PouchInteractable>();

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

        public void RegisterPouch(PouchInteractable pouch)
        {
            if (pouch != null && !string.IsNullOrEmpty(pouch.Id))
            {
                _activePouches[pouch.Id] = pouch;
            }
        }

        public void DeregisterPouch(string pouchId)
        {
            if (!string.IsNullOrEmpty(pouchId))
            {
                _activePouches.Remove(pouchId);
            }
        }

        public void SpawnPouch(List<ItemData> items, Vector3 position)
        {
            if (items == null || items.Count == 0) return;
            // 물리 갱신을 강제하여 같은 프레임에 스폰된 콜라이더도 감지할 수 있도록 함
            Physics2D.SyncTransforms();
            // 1. 반경 1.0f 내에 이미 PouchInteractable이 있는지 탐색
            Collider2D[] colliders = Physics2D.OverlapCircleAll(position, 1.0f);
            PouchInteractable targetPouch = null;
            float minDistance = float.MaxValue;

            foreach (var col in colliders)
            {
                var pouch = col.GetComponent<PouchInteractable>();
                if (pouch != null)
                {
                    float dist = Vector2.Distance(position, pouch.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        targetPouch = pouch;
                    }
                }
            }

            if (targetPouch != null)
            {
                // 2. 종속 (Merge)
                targetPouch.AppendItems(items);
                Debug.Log($"[LootManager] 기존 파우치({targetPouch.Id})에 {items.Count}개의 아이템을 병합했습니다. (거리: {minDistance:F2})");
                
                // UI 연동: 보상 패널이 열려 있고, 현재 파우치를 보고 있거나 비어 있다면 갱신
                if (DualUIPanel.Instance != null && DualUIPanel.Instance.IsPanelOpen)
                {
                    if (DualUIPanel.Instance.ActivePouch == targetPouch || DualUIPanel.Instance.ActivePouch == null)
                    {
                        DualUIPanel.Instance.Show(targetPouch);
                    }
                }
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
                        string newId = System.Guid.NewGuid().ToString();
                        interactable.Init(newId, items);
                        RegisterPouch(interactable);
                        
                        // UI 연동: 보상 패널이 열려 있고 보고 있는 파우치가 없다면 새 파우치를 바로 보여줌
                        if (DualUIPanel.Instance != null && DualUIPanel.Instance.IsPanelOpen && DualUIPanel.Instance.ActivePouch == null)
                        {
                            DualUIPanel.Instance.Show(interactable);
                        }
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
