using System.Collections.Generic;
using UnityEngine;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "The Midnight Bramble" — cavern enemy spawner.
    ///
    /// Sits inside an underground hollow and periodically spawns an enemy prefab up to a live cap.
    /// Optionally only activates when the player is within an activation radius (so off-screen caverns
    /// stay quiet). Tracks its spawned children and reclaims dead/destroyed slots so the cap is honoured.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float spawnInterval = 4f;
        [Tooltip("Maximum simultaneously-alive enemies from this spawner.")]
        [SerializeField] private int maxAlive = 3;
        [Tooltip("Local offset where enemies appear (e.g. the cavern floor).")]
        [SerializeField] private Vector2 spawnOffset = Vector2.zero;

        [Header("Activation")]
        [SerializeField] private bool requirePlayerNearby = true;
        [SerializeField] private float activationRadius = 12f;
        [Tooltip("Optional explicit player reference; auto-resolved by 'Player' tag if empty.")]
        [SerializeField] private Transform player;

        private readonly List<GameObject> _alive = new List<GameObject>();
        private float _timer;

        private void Start()
        {
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
            _timer = spawnInterval;
        }

        private void Update()
        {
            // Reclaim destroyed/inactive slots.
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] == null || !_alive[i].activeInHierarchy)
                    _alive.RemoveAt(i);

            if (_alive.Count >= maxAlive) return;
            if (requirePlayerNearby && !PlayerInRange()) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = spawnInterval;
                SpawnOne();
            }
        }

        private bool PlayerInRange()
        {
            if (player == null) return false;
            return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude
                   <= activationRadius * activationRadius;
        }

        private void SpawnOne()
        {
            if (enemyPrefab == null) return;
            Vector3 pos = transform.position + (Vector3)spawnOffset;
            GameObject e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            e.SetActive(true);
            _alive.Add(e);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + (Vector3)spawnOffset, 0.3f);
        }
    }
}
