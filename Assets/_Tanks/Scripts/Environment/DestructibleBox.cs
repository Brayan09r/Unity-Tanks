using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Tanks.Complete
{
    /// <summary>
    /// Destructible environment obstacle that can take radial damage from Shell explosions,
    /// blocks paths via NavMeshObstacle, provides juice on hit, and has a chance to drop PowerUps upon destruction.
    /// Supports automatic respawn on new rounds.
    /// </summary>
    public class DestructibleBox : MonoBehaviour
    {
        [Header("Health & Damage")]
        [Tooltip("Maximum health of this destructible object.")]
        [SerializeField] private float m_MaxHealth = 50f;
        [Tooltip("Respawn this obstacle automatically at the beginning of each new round.")]
        [SerializeField] private bool m_RespawnOnNewRound = true;

        [Header("Drop Settings")]
        [Tooltip("Probability [0, 1] of dropping a PowerUp when destroyed.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_DropChance = 0.7f;
        [Tooltip("PowerUp prefabs that can be dropped. If left empty, automatically discovers power-ups from scene spawners.")]
        [SerializeField] private GameObject[] m_PowerUpPrefabs;

        [Header("Feedback & FX")]
        [Tooltip("Particle system spawned upon destruction.")]
        [SerializeField] private ParticleSystem m_DestroyFX;
        [Tooltip("Sound played upon destruction.")]
        [SerializeField] private AudioClip m_DestroySound;

        private float m_CurrentHealth;
        private Vector3 m_InitialScale;
        private Vector3 m_InitialPosition;
        private Quaternion m_InitialRotation;
        private Coroutine m_HitReactionCoroutine;
        private NavMeshObstacle m_NavObstacle;
        private Collider m_Collider;
        private Renderer[] m_Renderers;

        private void Awake()
        {
            m_InitialScale = transform.localScale;
            m_InitialPosition = transform.position;
            m_InitialRotation = transform.rotation;

            m_Collider = GetComponent<Collider>();
            m_Renderers = GetComponentsInChildren<Renderer>();

            // Setup or configure NavMeshObstacle so AI routes around this obstacle
            m_NavObstacle = GetComponent<NavMeshObstacle>();
            if (m_NavObstacle == null)
            {
                m_NavObstacle = gameObject.AddComponent<NavMeshObstacle>();
                m_NavObstacle.carving = true;
                m_NavObstacle.carveOnlyStationary = false;
            }

            // Auto-populate drop prefabs if empty by inspecting existing PowerUpSpawner in the scene
            if (m_PowerUpPrefabs == null || m_PowerUpPrefabs.Length == 0)
            {
                var spawner = FindAnyObjectByType<PowerUpSpawner>();
                if (spawner != null && spawner.m_PowerUps != null && spawner.m_PowerUps.Length > 0)
                {
                    List<GameObject> discovered = new List<GameObject>();
                    foreach (var pu in spawner.m_PowerUps)
                    {
                        if (pu != null)
                            discovered.Add(pu.gameObject);
                    }
                    m_PowerUpPrefabs = discovered.ToArray();
                }
            }
        }

        private void OnEnable()
        {
            m_CurrentHealth = m_MaxHealth;
            transform.position = m_InitialPosition;
            transform.rotation = m_InitialRotation;
            transform.localScale = m_InitialScale;

            SetVisualAndCollisionActive(true);

            // Subscribe to decoupled events (Observer Pattern)
            ShellExplosion.OnShellExplosion += HandleShellExplosion;
            GameManager.OnRoundStarted += HandleRoundStarted;
        }

        private void OnDisable()
        {
            ShellExplosion.OnShellExplosion -= HandleShellExplosion;
            GameManager.OnRoundStarted -= HandleRoundStarted;
        }

        private void HandleRoundStarted(int roundNumber)
        {
            if (m_RespawnOnNewRound)
            {
                m_CurrentHealth = m_MaxHealth;
                transform.position = m_InitialPosition;
                transform.rotation = m_InitialRotation;
                transform.localScale = m_InitialScale;
                SetVisualAndCollisionActive(true);
            }
        }

        private void HandleShellExplosion(Vector3 explosionPos, float explosionRadius, float maxDamage)
        {
            // Calculate distance to explosion center
            float dist = Vector3.Distance(transform.position, explosionPos);
            if (dist <= explosionRadius)
            {
                // Falloff damage identical to tank damage calculation
                float relativeDistance = (explosionRadius - dist) / explosionRadius;
                float damage = Mathf.Max(0f, relativeDistance * maxDamage);
                TakeDamage(damage);
            }
        }

        public void TakeDamage(float amount)
        {
            if (m_CurrentHealth <= 0f) return;

            m_CurrentHealth -= amount;

            // Trigger visual hit reaction (Juice)
            if (gameObject.activeInHierarchy)
            {
                if (m_HitReactionCoroutine != null)
                    StopCoroutine(m_HitReactionCoroutine);
                m_HitReactionCoroutine = StartCoroutine(HitBounceEffect());
            }

            if (m_CurrentHealth <= 0f)
            {
                Die();
            }
        }

        private IEnumerator HitBounceEffect()
        {
            Vector3 punchScale = m_InitialScale * 1.15f;
            float elapsed = 0f;
            float duration = 0.12f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(punchScale, m_InitialScale, t);
                yield return null;
            }

            transform.localScale = m_InitialScale;
        }

        private void Die()
        {
            // Screen shake
            CameraControl.TriggerShake(0.35f, 0.2f);

            // Play destroy sound at point if assigned
            if (m_DestroySound != null)
            {
                AudioSource.PlayClipAtPoint(m_DestroySound, transform.position);
            }

            // Spawn particles
            if (m_DestroyFX != null)
            {
                Instantiate(m_DestroyFX, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            // Drop PowerUp by chance
            TryDropPowerUp();

            if (m_RespawnOnNewRound)
            {
                // Hide and disable collision instead of destroying so it can respawn on next round
                SetVisualAndCollisionActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetVisualAndCollisionActive(bool active)
        {
            if (m_Collider != null)
                m_Collider.enabled = active;

            if (m_NavObstacle != null)
                m_NavObstacle.enabled = active;

            if (m_Renderers != null)
            {
                foreach (var r in m_Renderers)
                {
                    if (r != null)
                        r.enabled = active;
                }
            }
        }

        private void TryDropPowerUp()
        {
            if (m_PowerUpPrefabs == null || m_PowerUpPrefabs.Length == 0) return;

            if (Random.value <= m_DropChance)
            {
                int randomIndex = Random.Range(0, m_PowerUpPrefabs.Length);
                GameObject prefabToSpawn = m_PowerUpPrefabs[randomIndex];
                if (prefabToSpawn != null)
                {
                    Vector3 spawnPos = transform.position;
                    spawnPos.y = 1.09f; // Consistent height for power-up visibility
                    Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                }
            }
        }
    }
}
