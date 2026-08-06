using System;
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Dungeon.Data
{
    [Serializable]
    public class ChunkSlot
    {
        [Tooltip("해당 슬롯 위치에서 렌덤으로 선택되어 나타날 청크 프리팹 리스트 (1~3개)")]
        public List<GameObject> possibleChunkPrefabs = new List<GameObject>();
    }

    [CreateAssetMenu(fileName = "NewDungeonData", menuName = "DimensionLooter/DungeonData")]
    public class DungeonData : ScriptableObject
    {
        [Header("Chunk Layout Settings")]
        [Tooltip("청크가 배치될 x, y 간격 (청크의 가로세로 길이)")]
        public Vector2 chunkOffset = new Vector2(20f, 20f);

        [Tooltip("1~9번째(총 9개) 슬롯에 들어갈 청크 프리팹 풀 목록")]
        public ChunkSlot[] chunkSlots = new ChunkSlot[9];

        [Header("Spawn Settings")]
        [Tooltip("각 청크별로 무작위 스폰할 몬스터 개수")]
        public int monsterCountPerChunk = 2;

        [Tooltip("각 청크별로 무작위 스폰할 함정 개수")]
        public int trapCountPerChunk = 1;

        [Header("Prefabs (User Assignment)")]
        [Tooltip("스폰 시킬 몬스터 프리팹 (임시 1종)")]
        public GameObject monsterPrefab;
        
        [Tooltip("스폰 시킬 함정 프리팹 (임시 1종)")]
        public GameObject trapPrefab;
    }
}
