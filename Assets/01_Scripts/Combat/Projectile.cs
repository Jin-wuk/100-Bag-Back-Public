using System.Collections.Generic;
using UnityEngine;
using BagBack100.Player;
using BagBack100.Monster;

namespace BagBack100.Combat
{
    public class Projectile : MonoBehaviour
    {
        private static readonly Dictionary<int, Queue<Projectile>> Pools = new Dictionary<int, Queue<Projectile>>();
        private static Transform _poolRoot;

        [SerializeField] private float speed = 10f;
        [SerializeField] private float lifeTime = 2f;
        
        private Vector3 _direction;
        private int _damage;
        private GameObject _owner;
        private int _prefabId;
        private float _aliveTimer;
        private bool _isReturned;

        private void OnDestroy()
        {
            if (_poolRoot == null)
            {
                Pools.Clear();
            }
        }

        public static Projectile Spawn(GameObject prefabObj, Vector3 position, Quaternion rotation, Vector3 direction, int damage, GameObject owner = null)
        {
            if (prefabObj == null) return null;

            EnsurePoolRoot();
            int prefabId = prefabObj.GetInstanceID();

            if (!Pools.TryGetValue(prefabId, out Queue<Projectile> queue))
            {
                queue = new Queue<Projectile>();
                Pools[prefabId] = queue;
            }

            Projectile instance = null;
            while (queue.Count > 0 && instance == null)
            {
                instance = queue.Dequeue();
            }

            if (instance == null)
            {
                var go = Instantiate(prefabObj, position, rotation, _poolRoot);
                instance = go.GetComponent<Projectile>();
                if (instance == null)
                {
                    instance = go.AddComponent<Projectile>();
                }
                instance._prefabId = prefabId;
            }
            else
            {
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.gameObject.SetActive(true);
            }

            instance.Initialize(direction, damage, owner);
            return instance;
        }

        private static void EnsurePoolRoot()
        {
            if (_poolRoot != null) return;
            GameObject rootGo = new GameObject("@ProjectilePool");
            _poolRoot = rootGo.transform;
        }

        public void Initialize(Vector3 direction, int damage, GameObject owner = null)
        {
            _direction = direction.normalized;
            _damage = damage;
            _owner = owner;
            _aliveTimer = 0f;
            _isReturned = false;
        }

        private void Update()
        {
            transform.position += _direction * speed * Time.deltaTime;
            _aliveTimer += Time.deltaTime;
            if (_aliveTimer >= lifeTime)
            {
                ReturnToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D col)
        {
            if (_isReturned || col == null) return;
            if (_owner != null && col.gameObject == _owner) return;

            bool ownerIsMonster = _owner != null && _owner.GetComponent<MonsterController>() != null;
            bool ownerIsPlayer = _owner != null && _owner.GetComponent<PlayerStatManager>() != null;

            bool hitSomething = false;
            var monster = col.GetComponent<MonsterController>();
            if (monster != null && !ownerIsMonster)
            {
                monster.TakeDamage(_damage, _direction, false);
                hitSomething = true;
            }
            else
            {
                var player = col.GetComponent<PlayerStatManager>();
                if (player != null && !ownerIsPlayer)
                {
                    player.TakeDamage(_damage);
                    hitSomething = true;
                }
            }
            
            // 장애물 레이어 충돌 처리
            if (!hitSomething && col.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
            {
                hitSomething = true;
            }

            if (hitSomething)
            {
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (_isReturned) return;
            _isReturned = true;
            gameObject.SetActive(false);

            if (_prefabId == 0)
            {
                Destroy(gameObject);
                return;
            }

            EnsurePoolRoot();
            transform.SetParent(_poolRoot, false);

            if (!Pools.TryGetValue(_prefabId, out Queue<Projectile> queue))
            {
                queue = new Queue<Projectile>();
                Pools[_prefabId] = queue;
            }
            queue.Enqueue(this);
        }
    }
}
