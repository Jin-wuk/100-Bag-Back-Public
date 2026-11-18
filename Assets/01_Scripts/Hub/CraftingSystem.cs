using System;
using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Data.Models;
using BagBack100.Player;

namespace BagBack100.Hub
{
    public enum TechNodeState
    {
        Hidden,             // 노드의 지역이 아직 잠김
        PrerequisiteLocked, // 같은 줄 이전 단계 미해금
        Unlockable,         // 해금 가능 (비용 충족 여부와 무관)
        Unlocked            // 해금 완료, 재제작 가능
    }

    /// <summary>
    /// Role: 제작소 처리(2계층 System). 테크트리 노드 상태 판정, 해금(재료 + 골드 → 장비 1개, 해금 기록), 재제작(골드 → 장비).
    ///       재료는 창고 → 가방 → 보안 파우치 순으로 차감하고, 장비는 가방 → 창고 순으로 넣는다.
    ///       매니저 3개의 변경과 골드 변경을 OnTechTreeChanged 하나로 중계해 UI가 이 시스템만 구독하게 한다.
    /// Dependencies: TechTreeSO, PlayerSessionData(지갑·해금 상태), InventoryManager, StorageManager, SecurePouchManager
    /// Events: OnCraftFailed(reason), OnCraftCompleted(nodeId), OnTechTreeChanged (시스템 지역 이벤트, GlobalEvents 아님)
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        public event Action<string> OnCraftFailed;
        public event Action<string> OnCraftCompleted;
        public event Action OnTechTreeChanged;

        [SerializeField] private TechTreeSO _techTree;

        public TechTreeSO TechTree => _techTree;

        private InventoryManager _inventory;
        private StorageManager _storage;
        private SecurePouchManager _pouch;

        // 차감 순서(창고 → 가방 → 파우치)대로 담는 재사용 배열. 호출마다 채워 할당을 없앤다
        private readonly ISlotItemContainer[] _materialSources = new ISlotItemContainer[3];

        private void Awake()
        {
            if (_techTree == null) Debug.LogError("[CraftingSystem] TechTreeSO가 연결되지 않았습니다.", this);
        }

        private void OnEnable()
        {
            InventoryManager.OnManagerCreated += BindInventory;
            StorageManager.OnManagerCreated += BindStorage;
            SecurePouchManager.OnManagerCreated += BindPouch;
            PlayerSessionData.OnGoldChanged += HandleGoldChanged;
        }

        private void Start()
        {
            // HubSystem.Awake가 매니저를 동적으로 붙인 뒤(Start 시점)에 한 번 찾는다. 이후 늦게 생긴 매니저는 OnManagerCreated로 받는다
            if (_inventory == null) BindInventory(FindObjectOfType<InventoryManager>());
            if (_storage == null) BindStorage(FindObjectOfType<StorageManager>());
            if (_pouch == null) BindPouch(FindObjectOfType<SecurePouchManager>());
        }

        private void OnDisable()
        {
            InventoryManager.OnManagerCreated -= BindInventory;
            StorageManager.OnManagerCreated -= BindStorage;
            SecurePouchManager.OnManagerCreated -= BindPouch;
            PlayerSessionData.OnGoldChanged -= HandleGoldChanged;
            BindInventory(null);
            BindStorage(null);
            BindPouch(null);
        }

        // 참조가 바뀌면 이전 인스턴스의 구독을 먼저 끊는다. 파괴된 인스턴스도 C# 객체로는 남아 있으므로 ReferenceEquals로 비교한다
        private void BindInventory(InventoryManager manager)
        {
            if (ReferenceEquals(_inventory, manager)) return;
            if (!ReferenceEquals(_inventory, null)) _inventory.OnInventoryChanged -= RelayChanged;
            _inventory = manager;
            if (!ReferenceEquals(_inventory, null)) _inventory.OnInventoryChanged += RelayChanged;
            RelayChanged();
        }

        private void BindStorage(StorageManager manager)
        {
            if (ReferenceEquals(_storage, manager)) return;
            if (!ReferenceEquals(_storage, null)) _storage.OnStorageChanged -= RelayChanged;
            _storage = manager;
            if (!ReferenceEquals(_storage, null)) _storage.OnStorageChanged += RelayChanged;
            RelayChanged();
        }

        private void BindPouch(SecurePouchManager manager)
        {
            if (ReferenceEquals(_pouch, manager)) return;
            if (!ReferenceEquals(_pouch, null)) _pouch.OnPouchChanged -= RelayChanged;
            _pouch = manager;
            if (!ReferenceEquals(_pouch, null)) _pouch.OnPouchChanged += RelayChanged;
            RelayChanged();
        }

        // 지연 캐싱: Start 전에 호출되거나 매니저가 다시 만들어졌어도 쓸 때 다시 찾는다
        private InventoryManager Inventory
        {
            get
            {
                if (_inventory == null) BindInventory(FindObjectOfType<InventoryManager>());
                return _inventory;
            }
        }

        private StorageManager Storage
        {
            get
            {
                if (_storage == null) BindStorage(FindObjectOfType<StorageManager>());
                return _storage;
            }
        }

        private SecurePouchManager Pouch
        {
            get
            {
                if (_pouch == null) BindPouch(FindObjectOfType<SecurePouchManager>());
                return _pouch;
            }
        }

        private void HandleGoldChanged(int gold) => RelayChanged();

        private void RelayChanged() => OnTechTreeChanged?.Invoke();

        public TechNodeState GetState(TechNodeSO node)
        {
            if (node == null || !PlayerSessionData.IsRegionUnlocked(node.RegionId)) return TechNodeState.Hidden;
            if (PlayerSessionData.IsNodeUnlocked(node.NodeId)) return TechNodeState.Unlocked;
            if (node.Prerequisite != null && !PlayerSessionData.IsNodeUnlocked(node.Prerequisite.NodeId)) return TechNodeState.PrerequisiteLocked;
            return TechNodeState.Unlockable;
        }

        /// <summary>창고 + 가방 + 파우치에 있는 재료 합계</summary>
        public int GetOwnedAmount(ItemDataSO item)
        {
            if (item == null) return 0;
            FillMaterialSources();
            int total = 0;
            for (int i = 0; i < _materialSources.Length; i++)
            {
                if (_materialSources[i] != null) total += _materialSources[i].CountItemId(item.itemID);
            }
            return total;
        }

        /// <summary>해금 비용(골드·재료)이 충분한지. 공간 검사는 하지 않는다 (UI 버튼 활성화용, 할당 없음)</summary>
        public bool CanAffordUnlock(TechNodeSO node)
        {
            return node != null && PlayerSessionData.Gold >= node.UnlockGold && HasMaterials(node);
        }

        public bool CanAffordRecraft(TechNodeSO node)
        {
            return node != null && PlayerSessionData.Gold >= node.RecraftGold;
        }

        public bool TryUnlock(TechNodeSO node)
        {
            if (node == null) return false;

            TechNodeState state = GetState(node);
            if (state == TechNodeState.Unlocked) return Fail("Already unlocked.");
            if (state == TechNodeState.Hidden) return Fail("This region is not unlocked yet.");
            if (state == TechNodeState.PrerequisiteLocked) return Fail("Unlock the previous tier first.");
            if (!IsCraftable(node)) return Fail("This item cannot be crafted.");
            if (PlayerSessionData.Gold < node.UnlockGold) return Fail("Not enough gold.");
            if (!HasMaterials(node)) return Fail("Not enough materials.");

            var crafted = new RuntimeItem(node.ResultItem);
            if (!HasRoomFor(crafted)) return Fail("No space in bag or storage.");

            // 알림을 보류한 채 추가 → 차감 → 해금 기록을 끝내 UI가 중간 상태를 보지 않게 한다.
            // 위 검사를 통과하면 추가·차감은 실패하지 않으며(단일 스레드, 결과 장비와 재료는 itemID가 다름), 추가가 실패하면 아무것도 차감하지 않는다
            FillMaterialSources();
            for (int i = 0; i < _materialSources.Length; i++) _materialSources[i]?.BeginBulkChange();
            bool added;
            try
            {
                added = TryAddCrafted(crafted);
                if (added)
                {
                    ConsumeMaterials(node);
                    PlayerSessionData.MarkNodeUnlocked(node.NodeId);
                }
            }
            finally
            {
                for (int i = 0; i < _materialSources.Length; i++) _materialSources[i]?.EndBulkChange();
            }

            if (!added) return Fail("No space in bag or storage.");

            PlayerSessionData.TrySpendGold(node.UnlockGold);
            OnCraftCompleted?.Invoke(node.NodeId);
            return true;
        }

        public bool TryRecraft(TechNodeSO node)
        {
            if (node == null) return false;
            if (GetState(node) != TechNodeState.Unlocked) return Fail("Unlock this item first.");
            if (!IsCraftable(node)) return Fail("This item cannot be crafted.");
            if (PlayerSessionData.Gold < node.RecraftGold) return Fail("Not enough gold.");

            var crafted = new RuntimeItem(node.ResultItem);
            if (!HasRoomFor(crafted) || !TryAddCrafted(crafted)) return Fail("No space in bag or storage.");

            PlayerSessionData.TrySpendGold(node.RecraftGold);
            OnCraftCompleted?.Invoke(node.NodeId);
            return true;
        }

        private bool Fail(string reason)
        {
            OnCraftFailed?.Invoke(reason);
            return false;
        }

        private bool IsCraftable(TechNodeSO node)
        {
            if (node.ResultItem != null) return true;
            Debug.LogError($"[CraftingSystem] {node.name}에 결과 장비가 없습니다.", node);
            return false;
        }

        private void FillMaterialSources()
        {
            _materialSources[0] = Storage;
            _materialSources[1] = Inventory;
            _materialSources[2] = Pouch;
            // 파괴된 매니저(Unity null)를 인터페이스로 들고 있으면 ?. 검사를 통과하므로 여기서 걸러 낸다
            for (int i = 0; i < _materialSources.Length; i++)
            {
                if (_materialSources[i] is UnityEngine.Object obj && obj == null) _materialSources[i] = null;
            }
        }

        private bool HasMaterials(TechNodeSO node)
        {
            var materials = node.Materials;
            for (int i = 0; i < materials.Count; i++)
            {
                ItemDataSO item = materials[i].Item;
                if (item == null)
                {
                    Debug.LogError($"[CraftingSystem] {node.name}의 재료 {i}번 칸이 비어 있습니다.", node);
                    return false;
                }
                if (GetOwnedAmount(item) < materials[i].Amount) return false;
            }
            return true;
        }

        // 재료별로 창고 → 가방 → 파우치 순서로 덜어내고, 모자란 수량은 다음 컨테이너로 넘긴다. HasMaterials 통과 뒤에만 호출
        private void ConsumeMaterials(TechNodeSO node)
        {
            var materials = node.Materials;
            for (int m = 0; m < materials.Count; m++)
            {
                int itemID = materials[m].Item.itemID;
                int remaining = materials[m].Amount;
                for (int i = 0; i < _materialSources.Length && remaining > 0; i++)
                {
                    ISlotItemContainer source = _materialSources[i];
                    if (source == null) continue;
                    int take = Mathf.Min(remaining, source.CountItemId(itemID));
                    if (take > 0 && source.RemoveItemIdAmount(itemID, take)) remaining -= take;
                }
                if (remaining > 0) Debug.LogError($"[CraftingSystem] {node.name}의 재료 {materials[m].Item.name}를 {remaining}개 덜 차감했습니다.", node);
            }
        }

        private bool HasRoomFor(RuntimeItem crafted)
        {
            InventoryManager inventory = Inventory;
            StorageManager storage = Storage;
            return (inventory != null && inventory.CanAddItem(crafted)) || (storage != null && storage.CanAddItem(crafted));
        }

        private bool TryAddCrafted(RuntimeItem crafted)
        {
            InventoryManager inventory = Inventory;
            StorageManager storage = Storage;
            return (inventory != null && inventory.TryAddNextAvailableSlot(crafted)) ||
                   (storage != null && storage.TryAddNextAvailableSlot(crafted));
        }
    }
}
