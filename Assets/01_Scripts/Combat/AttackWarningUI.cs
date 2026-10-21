using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Combat
{
    public class AttackWarningUI : MonoBehaviour
    {
        private static readonly Dictionary<int, Queue<AttackWarningUI>> Pools = new Dictionary<int, Queue<AttackWarningUI>>();
        private static Transform _poolRoot;

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _warningColor = new Color(1f, 0f, 0f, 0.5f);

        private int _prefabId;
        private bool _isInPool;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnDestroy()
        {
            // 씬 전환 등으로 루트가 파괴될 때 정적 캐시 정리
            if (_poolRoot == null)
            {
                Pools.Clear();
            }
        }

        /// <summary>
        /// 오브젝트 풀에서 AttackWarningUI를 꺼내 지정된 위치에 duration 동안 표시한 뒤 자동으로 풀에 반납합니다.
        /// </summary>
        public static AttackWarningUI Show(AttackWarningUI prefab, Vector3 position, float duration)
        {
            if (prefab == null) return null;

            EnsurePoolRoot();
            int prefabId = prefab.GetInstanceID();

            if (!Pools.TryGetValue(prefabId, out Queue<AttackWarningUI> queue))
            {
                queue = new Queue<AttackWarningUI>();
                Pools[prefabId] = queue;
            }

            AttackWarningUI instance = null;
            while (queue.Count > 0 && instance == null)
            {
                instance = queue.Dequeue();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, _poolRoot);
                instance._prefabId = prefabId;
            }

            instance._isInPool = false;
            instance.ShowWarning(position, duration);
            return instance;
        }

        private static void EnsurePoolRoot()
        {
            if (_poolRoot != null) return;
            GameObject rootGo = new GameObject("@AttackWarningPool");
            _poolRoot = rootGo.transform;
        }

        public void ShowWarning(Vector3 position, float duration)
        {
            transform.position = position;
            if (_renderer != null)
            {
                _renderer.color = _warningColor;
            }

            gameObject.SetActive(true);

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }
            _flashCoroutine = StartCoroutine(FlashRoutine(duration));
        }

        /// <summary>
        /// 몬스터 사망이나 피격 캔슬 시 즉시 장판을 끄고 풀로 회수할 때 호출합니다.
        /// </summary>
        public void CancelAndReturn()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }
            ReturnToPool();
        }

        private IEnumerator FlashRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_renderer != null)
                {
                    float alpha = Mathf.PingPong(elapsed * 4f, 0.5f) + 0.2f;
                    _renderer.color = new Color(_warningColor.r, _warningColor.g, _warningColor.b, alpha);
                }
                yield return null;
            }

            _flashCoroutine = null;
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);

            if (_prefabId == 0 || _isInPool) return;

            EnsurePoolRoot();
            transform.SetParent(_poolRoot, false);

            if (!Pools.TryGetValue(_prefabId, out Queue<AttackWarningUI> queue))
            {
                queue = new Queue<AttackWarningUI>();
                Pools[_prefabId] = queue;
            }

            _isInPool = true;
            queue.Enqueue(this);
        }
    }
}
