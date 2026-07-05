using UnityEngine;

namespace EarthOnline.NPC
{
    /// <summary>
    /// NPC巡逻行为 —— 在指定区域内随机走动，停在原地片刻再走。
    /// </summary>
    [RequireComponent(typeof(NPCBase))]
    public class NPCWander : MonoBehaviour
    {
        [Header("巡逻设置")]
        public float wanderRadius = 8f;
        public float moveSpeed = 1.5f;
        public float minWaitTime = 2f;
        public float maxWaitTime = 5f;
        public float arrivalThreshold = 0.5f;

        private NPCBase _npc;
        private Vector3 _homePosition;
        private Vector3 _targetPosition;
        private float _waitTimer;
        private bool _isWaiting;
        private CharacterController _cc;

        void Start()
        {
            _npc = GetComponent<NPCBase>();
            _homePosition = transform.position;
            _cc = GetComponent<CharacterController>();
            if (_cc == null) _cc = gameObject.AddComponent<CharacterController>();
            if (_cc != null)
            {
                _cc.center = new Vector3(0, 1, 0);
                _cc.height = 2f;
                _cc.radius = 0.5f;
            }
            PickNewTarget();
        }

        void Update()
        {
            if (_npc != null && _npc.IsInteracting) return; // 暂停巡逻

            if (_isWaiting)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0)
                {
                    _isWaiting = false;
                    PickNewTarget();
                }
                return;
            }

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(_targetPosition.x, 0, _targetPosition.z));

            if (dist <= arrivalThreshold)
            {
                _isWaiting = true;
                _waitTimer = Random.Range(minWaitTime, maxWaitTime);
                return;
            }

            // 走向目标
            Vector3 dir = (_targetPosition - transform.position).normalized;
            dir.y = 0;
            transform.forward = Vector3.Lerp(transform.forward, dir, 3f * Time.deltaTime);
            if (_cc != null)
                _cc.SimpleMove(dir * moveSpeed);
        }

        void PickNewTarget()
        {
            Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
            _targetPosition = new Vector3(
                _homePosition.x + randomCircle.x,
                _homePosition.y,
                _homePosition.z + randomCircle.y);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(Application.isPlaying ? _homePosition : transform.position, wanderRadius);
        }
    }
}
