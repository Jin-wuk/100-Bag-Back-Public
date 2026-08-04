// File: Assets/Scripts/Managers/PoolManager.cs
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 빈번하게 생성/파괴되는 오브젝트(UI 타일, 데미지 텍스트 등)를 재사용하기 위한 오브젝트 풀 매니저입니다.
    /// [Why] 런타임 중 Instantiate와 Destroy의 잦은 호출로 인한 GC 스파이크를 방지하기 위함입니다.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        // 풀 키워드(string)를 기반으로 큐(Queue) 형태의 풀을 관리합니다.
        private Dictionary<string, Queue<GameObject>> _poolDictionary = new Dictionary<string, Queue<GameObject>>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 새로운 오브젝트 풀을 초기화합니다.
        /// </summary>
        public void CreatePool(string poolKey, GameObject prefab, int poolSize)
        {
            if (_poolDictionary.ContainsKey(poolKey)) return;

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }
            _poolDictionary.Add(poolKey, objectPool);
        }

        /// <summary>
        /// 풀에서 오브젝트를 꺼내 활성화합니다.
        /// </summary>
        public GameObject SpawnFromPool(string poolKey, Vector3 position, Quaternion rotation)
        {
            if (!_poolDictionary.ContainsKey(poolKey) || _poolDictionary[poolKey].Count == 0)
            {
                Debug.LogWarning($"[PoolManager] '{poolKey}' 풀이 비어있거나 존재하지 않습니다. 확장 로직이 필요합니다.", this);
                return null;
            }

            GameObject objectToSpawn = _poolDictionary[poolKey].Dequeue();
            objectToSpawn.SetActive(true);
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;

            return objectToSpawn;
        }

        /// <summary>
        /// 사용이 끝난 오브젝트를 풀로 반환합니다.
        /// </summary>
        public void ReturnToPool(string poolKey, GameObject obj)
        {
            obj.SetActive(false);
            if (_poolDictionary.ContainsKey(poolKey))
            {
                _poolDictionary[poolKey].Enqueue(obj);
            }
            else
            {
                Debug.LogWarning($"[PoolManager] 존재하지 않는 풀('{poolKey}')에 반환하려 시도했습니다. 파괴합니다.", this);
                Destroy(obj);
            }
        }
    }
}
