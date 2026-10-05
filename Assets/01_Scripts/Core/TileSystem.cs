using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Core
{
    public class TileSystem : MonoBehaviour
    {
        public static TileSystem Instance { get; private set; }

        private Dictionary<Vector3Int, GameObject> _occupiedTiles = new Dictionary<Vector3Int, GameObject>();
        private Dictionary<Vector3Int, float> _dangerExpirations = new Dictionary<Vector3Int, float>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // --- Danger Map ---
        public void SetDanger(Vector3Int cellPos, float duration)
        {
            _dangerExpirations[cellPos] = Time.time + duration;
        }

        public float GetDangerCost(Vector3Int cellPos)
        {
            if (_dangerExpirations.TryGetValue(cellPos, out float exp))
            {
                if (Time.time < exp) return 50f; // High penalty for dangerous tiles
                else _dangerExpirations.Remove(cellPos);
            }
            return 0f;
        }

        // --- Occupation ---
        public bool IsTileOccupied(Vector3Int cellPos)
        {
            if (_occupiedTiles.TryGetValue(cellPos, out GameObject entity))
            {
                if (entity == null)
                {
                    _occupiedTiles.Remove(cellPos);
                    return false;
                }
                return true;
            }
            return false;
        }

        public GameObject GetEntityAtTile(Vector3Int cellPos)
        {
            if (_occupiedTiles.TryGetValue(cellPos, out GameObject entity))
            {
                if (entity == null)
                {
                    _occupiedTiles.Remove(cellPos);
                    return null;
                }
                return entity;
            }
            return null;
        }

        public bool TryOccupyTile(Vector3Int targetCell, GameObject entity, Vector3Int currentCell)
        {
            if (IsTileOccupied(targetCell) && _occupiedTiles[targetCell] != entity)
            {
                return false; 
            }

            if (_occupiedTiles.ContainsKey(currentCell) && _occupiedTiles[currentCell] == entity)
            {
                _occupiedTiles.Remove(currentCell);
            }

            _occupiedTiles[targetCell] = entity;
            return true;
        }

        public void RegisterInitialTile(Vector3Int cellPos, GameObject entity)
        {
            _occupiedTiles[cellPos] = entity;
        }

        public void ReleaseTile(Vector3Int cellPos, GameObject entity)
        {
            if (_occupiedTiles.ContainsKey(cellPos) && _occupiedTiles[cellPos] == entity)
            {
                _occupiedTiles.Remove(cellPos);
            }
        }

        // --- Items ---
        private Dictionary<Vector3Int, Map.LootableItem> _itemTiles = new Dictionary<Vector3Int, Map.LootableItem>();

        public Map.LootableItem GetItemAtTile(Vector3Int cellPos)
        {
            if (_itemTiles.TryGetValue(cellPos, out Map.LootableItem item))
            {
                if (item == null)
                {
                    _itemTiles.Remove(cellPos);
                    return null;
                }
                return item;
            }
            return null;
        }

        public void RegisterItemTile(Vector3Int cellPos, Map.LootableItem item)
        {
            _itemTiles[cellPos] = item;
        }

        public void RemoveItemTile(Vector3Int cellPos)
        {
            _itemTiles.Remove(cellPos);
        }
    }
}
