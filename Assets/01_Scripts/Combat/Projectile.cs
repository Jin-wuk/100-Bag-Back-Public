using UnityEngine;
using BagBack100.Player;

namespace BagBack100.Combat
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private float lifeTime = 2f;
        
        private Vector3 _direction;
        private int _damage;
        
        public void Initialize(Vector3 direction, int damage)
        {
            _direction = direction.normalized;
            _damage = damage;
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            transform.position += _direction * speed * Time.deltaTime;
        }

        private void OnTriggerEnter2D(Collider2D col)
        {
            bool hitSomething = false;
            var monster = col.GetComponent<MonsterController>();
            if (monster != null)
            {
                monster.TakeDamage(_damage);
                hitSomething = true;
            }
            else
            {
                var player = col.GetComponent<PlayerStatManager>();
                if (player != null)
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
                Destroy(gameObject);
            }
        }
    }
}
