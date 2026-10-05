using UnityEngine;
using System.Collections;

namespace BagBack100.Combat
{
    public class AttackWarningUI : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _warningColor = new Color(1, 0, 0, 0.5f);
        
        public void ShowWarning(Vector3 position, float duration)
        {
            transform.position = position;
            if (_renderer != null)
                _renderer.color = _warningColor;
            gameObject.SetActive(true);
            StartCoroutine(FlashRoutine(duration));
        }

        private IEnumerator FlashRoutine(float duration)
        {
            float elapsed = 0f;
            while(elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_renderer != null)
                {
                    float alpha = Mathf.PingPong(elapsed * 4f, 0.5f) + 0.2f; // blinking effect
                    _renderer.color = new Color(_warningColor.r, _warningColor.g, _warningColor.b, alpha);
                }
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
