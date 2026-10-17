using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using BagBack100.Core;

namespace BagBack100.Combat
{
    /// <summary>
    /// 공격 이펙트 프리팹, 절차적 타격 충격파(Hit Burst), 데미지 플로팅 텍스트를
    /// Instantiate/Destroy 없이 100% 오브젝트 풀링으로 관리하는 전투 시각 피드백 시스템입니다.
    /// </summary>
    public class CombatFeedbackSystem : MonoBehaviour
    {
        public static CombatFeedbackSystem Instance { get; private set; }

        private static readonly Dictionary<int, Queue<GameObject>> PrefabPools = new Dictionary<int, Queue<GameObject>>();
        private static readonly Queue<TextMeshPro> DamageTextPool = new Queue<TextMeshPro>();
        private static readonly Queue<SpriteRenderer> HitFlashPool = new Queue<SpriteRenderer>();
        private static Sprite _proceduralCircleSprite;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            GlobalEvents.OnEntityDamaged += HandleEntityDamaged;
        }

        private void OnDisable()
        {
            GlobalEvents.OnEntityDamaged -= HandleEntityDamaged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                PrefabPools.Clear();
                DamageTextPool.Clear();
                HitFlashPool.Clear();
                Instance = null;
            }
        }

        public static CombatFeedbackSystem EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<CombatFeedbackSystem>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }
            GameObject go = new GameObject("@CombatFeedbackSystem");
            Instance = go.AddComponent<CombatFeedbackSystem>();
            return Instance;
        }

        #region 1. Weapon / Skill Effect Prefab Pooling

        public GameObject SpawnPooledEffect(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float duration, float animSpeed = 1f)
        {
            if (prefab == null)
            {
                // 프리팹이 없을 경우 절차적 베기/충격파 이펙트로 폴백
                SpawnProceduralHitFlash(position, scale.x * 0.9f, new Color(1f, 0.95f, 0.7f, 0.85f), Mathf.Min(0.25f, duration));
                return null;
            }

            int prefabId = prefab.GetInstanceID();
            if (!PrefabPools.TryGetValue(prefabId, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                PrefabPools[prefabId] = queue;
            }

            GameObject instance = null;
            while (queue.Count > 0 && instance == null)
            {
                instance = queue.Dequeue();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, transform);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            instance.SetActive(true);

            // 애니메이터 초기화 및 속도 동기화
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.speed = animSpeed;
                animator.Play(0, -1, 0f);
            }

            // 파티클 시스템이 있다면 재시작
            var particles = instance.GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Clear();
                particles[i].Play();
            }

            StartCoroutine(ReturnPrefabRoutine(prefabId, instance, duration));
            return instance;
        }

        private IEnumerator ReturnPrefabRoutine(int prefabId, GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (instance == null) yield break;

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);

            if (!PrefabPools.TryGetValue(prefabId, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                PrefabPools[prefabId] = queue;
            }
            queue.Enqueue(instance);
        }

        #endregion

        #region 2. Procedural Hit Flash & Floating Damage Text

        private void HandleEntityDamaged(Vector3 worldPos, int damage, bool isHeavyHit, bool isPlayerDamaged)
        {
            // 1. 타격 지점 절차적 충격파 섬광 (방법 2)
            Color flashColor = isPlayerDamaged
                ? new Color(1f, 0.2f, 0.2f, 0.85f)
                : (isHeavyHit ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(1f, 1f, 1f, 0.8f));
            float targetScale = isHeavyHit ? 1.45f : 0.9f;
            SpawnProceduralHitFlash(worldPos, targetScale, flashColor, isHeavyHit ? 0.22f : 0.14f);

            // 2. 플로팅 데미지 숫자 팝업
            SpawnFloatingDamageText(worldPos, damage, isHeavyHit, isPlayerDamaged);
        }

        public void SpawnProceduralHitFlash(Vector3 position, float maxScale, Color color, float duration)
        {
            EnsureProceduralSprite();

            SpriteRenderer sr = null;
            while (HitFlashPool.Count > 0 && sr == null)
            {
                sr = HitFlashPool.Dequeue();
            }

            if (sr == null)
            {
                GameObject go = new GameObject("PooledHitFlash");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _proceduralCircleSprite;
                sr.sortingOrder = 45;
            }

            sr.transform.position = position;
            sr.transform.localScale = Vector3.one * (maxScale * 0.25f);
            sr.color = color;
            sr.gameObject.SetActive(true);

            StartCoroutine(AnimateHitFlashRoutine(sr, maxScale, color, duration));
        }

        private IEnumerator AnimateHitFlashRoutine(SpriteRenderer sr, float maxScale, Color startColor, float duration)
        {
            float elapsed = 0f;
            float startScale = maxScale * 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - (1f - t) * (1f - t);

                sr.transform.localScale = Vector3.one * Mathf.Lerp(startScale, maxScale, easeOut);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                yield return null;
            }

            sr.gameObject.SetActive(false);
            HitFlashPool.Enqueue(sr);
        }

        public void SpawnFloatingDamageText(Vector3 worldPos, int damage, bool isHeavyHit, bool isPlayerDamaged)
        {
            TextMeshPro tmp = null;
            while (DamageTextPool.Count > 0 && tmp == null)
            {
                tmp = DamageTextPool.Dequeue();
            }

            if (tmp == null)
            {
                GameObject go = new GameObject("PooledDamageText");
                go.transform.SetParent(transform, false);
                tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.sortingOrder = 60;
            }

            Vector2 randJitter = Random.insideUnitCircle * 0.15f;
            Vector3 startPos = worldPos + new Vector3(randJitter.x, 0.35f + Mathf.Abs(randJitter.y), 0f);
            tmp.transform.position = startPos;
            tmp.text = isHeavyHit ? $"{damage}!" : damage.ToString();
            tmp.fontSize = isHeavyHit ? 5.5f : 4.2f;

            Color baseColor = isPlayerDamaged
                ? new Color(1f, 0.25f, 0.25f, 1f)
                : (isHeavyHit ? new Color(1f, 0.85f, 0.15f, 1f) : Color.white);
            tmp.color = baseColor;
            tmp.transform.localScale = Vector3.one;
            tmp.gameObject.SetActive(true);

            StartCoroutine(AnimateDamageTextRoutine(tmp, startPos, baseColor, isHeavyHit));
        }

        private IEnumerator AnimateDamageTextRoutine(TextMeshPro tmp, Vector3 startPos, Color baseColor, bool isHeavyHit)
        {
            float duration = isHeavyHit ? 0.65f : 0.5f;
            float elapsed = 0f;
            float riseHeight = isHeavyHit ? 0.75f : 0.55f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 초반 팝업 스케일 펀치
                float scale = t < 0.2f
                    ? Mathf.Lerp(0.5f, isHeavyHit ? 1.35f : 1.15f, t / 0.2f)
                    : Mathf.Lerp(isHeavyHit ? 1.35f : 1.15f, 1f, (t - 0.2f) / 0.8f);

                tmp.transform.localScale = Vector3.one * scale;
                tmp.transform.position = startPos + new Vector3(0f, Mathf.Sin(t * Mathf.PI * 0.5f) * riseHeight, 0f);

                float alpha = t > 0.6f ? 1f - ((t - 0.6f) / 0.4f) : 1f;
                tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                yield return null;
            }

            tmp.gameObject.SetActive(false);
            DamageTextPool.Enqueue(tmp);
        }

        private static void EnsureProceduralSprite()
        {
            if (_proceduralCircleSprite != null) return;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = (size - 1) * 0.5f;
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / maxR;
                    // 외곽 링 + 중심 코어 형태의 타격 스파크 텍스처
                    float alpha = 0f;
                    if (dist <= 1f)
                    {
                        alpha = dist > 0.65f ? 1f : (1f - dist * 0.5f);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            _proceduralCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        #endregion
    }
}
