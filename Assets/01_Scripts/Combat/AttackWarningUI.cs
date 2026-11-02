using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Combat
{
    /// <summary>
    /// Role: 풀링되는 공격 예고 장판. 지정 시간 점멸 후 풀로 반납. ReleaseAll로 표시 중인 장판을 일괄 회수(층 전환, 보스 처치 정리).
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    public class AttackWarningUI : MonoBehaviour
    {
        private static readonly Dictionary<int, Queue<AttackWarningUI>> Pools = new Dictionary<int, Queue<AttackWarningUI>>();
        // 층 전환·보스 처치 정리 때 깔려 있는 예고 장판을 한 번에 회수하려고 활성 인스턴스를 따로 추적한다
        private static readonly List<AttackWarningUI> ActiveInstances = new List<AttackWarningUI>();
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
            ActiveInstances.Remove(this);
            // 씬 전환 등으로 루트가 파괴될 때 정적 캐시 정리
            if (_poolRoot == null)
            {
                Pools.Clear();
            }
        }

        /// <summary>표시 중인 예고 장판을 전부 풀로 회수한다 (층 전환, 보스 처치 정리).</summary>
        public static void ReleaseAll()
        {
            for (int i = ActiveInstances.Count - 1; i >= 0; i--)
            {
                AttackWarningUI warning = ActiveInstances[i];
                if (warning == null)
                {
                    ActiveInstances.RemoveAt(i);
                    continue;
                }
                warning.CancelAndReturn();
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
            if (!ActiveInstances.Contains(instance)) ActiveInstances.Add(instance);
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
            ActiveInstances.Remove(this);
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
