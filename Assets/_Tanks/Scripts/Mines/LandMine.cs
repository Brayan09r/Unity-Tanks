using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Mina terrestre. Ciclo de vida (pequeña máquina de estados):
    ///   Armando (1 s, parpadeo amarillo) -> Armada (pulso rojo) -> Explotó.
    ///
    /// Cuando está armada y OTRO tanque la pisa, explota con daño radial: más daño
    /// cuanto más cerca del centro (la misma fórmula que usa ShellExplosion).
    /// Usa solo métodos públicos que ya existían: TankHealth.TakeDamage,
    /// TankMovement.AddExplosionForce y DestructibleBox.TakeDamage.
    ///
    /// Si no se asigna un prefab, la mina construye su propio modelo con primitivas.
    /// </summary>
    public class LandMine : MonoBehaviour
    {
        private enum MineState { Arming, Armed, Exploded }

        [Header("Activación")]
        [Tooltip("Segundos desde que se suelta hasta que queda activa.")]
        [SerializeField] private float m_ArmDelay = 1f;
        [Tooltip("¿El dueño puede activar su propia mina al pisarla?")]
        [SerializeField] private bool m_OwnerCanTrigger = false;
        [Tooltip("Tamaño del área que detecta a un tanque encima (ancho, alto, largo).")]
        [SerializeField] private Vector3 m_TriggerSize = new Vector3(1.6f, 1.2f, 1.6f);

        [Header("Explosión")]
        [Tooltip("Daño si el tanque está justo en el centro. Disminuye con la distancia.")]
        [SerializeField] private float m_MaxDamage = 60f;
        [Tooltip("Radio de la explosión en unidades de Unity.")]
        [SerializeField] private float m_ExplosionRadius = 4f;
        [Tooltip("Fuerza de empuje aplicada a los tanques (mantener en 50 o menos).")]
        [SerializeField] private float m_ExplosionForce = 40f;
        [Tooltip("Si está activo, la explosión no daña al tanque que puso la mina.")]
        [SerializeField] private bool m_OwnerImmune = true;
        [Tooltip("Capa de los tanques (normalmente 'Players').")]
        [SerializeField] private LayerMask m_TankMask;
        [Tooltip("También daña las cajas destructibles del escenario.")]
        [SerializeField] private bool m_DamageDestructibles = true;
        [Tooltip("La mina explota si un proyectil o una mina cercana explota a su lado (reacción en cadena).")]
        [SerializeField] private bool m_ChainReaction = true;

        [Header("Vida útil")]
        [Tooltip("Segundos antes de desactivarse sola. 0 = no expira.")]
        [SerializeField] private float m_LifeTime = 45f;
        [Tooltip("Se elimina al empezar una nueva ronda.")]
        [SerializeField] private bool m_ClearOnNewRound = true;

        [Header("Efectos (opcionales)")]
        [Tooltip("Partículas de la explosión. Si está vacío, TankMineDropper usa las del proyectil del tanque.")]
        [SerializeField] private ParticleSystem m_ExplosionFX;
        [SerializeField] private Color m_ArmingColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color m_ArmedColor = new Color(1f, 0.15f, 0.1f);

        public GameObject Owner { get; private set; }
        public bool IsArmed { get { return m_State == MineState.Armed; } }

        private MineState m_State = MineState.Arming;
        private float m_Timer;
        private Material m_LightMaterial;
        private Material m_BodyMaterial;
        private Light m_Light;

        // ------------------------------------------------------------------ Configuración

        /// <summary>Lo llama TankMineDropper justo después de crear la mina.</summary>
        public void Setup(GameObject owner, MineDropperSettings settings, ParticleSystem fallbackFX, LayerMask tankMask)
        {
            Owner = owner;

            if (settings != null)
            {
                m_ArmDelay = settings.m_ArmDelay;
                m_MaxDamage = settings.m_MaxDamage;
                m_ExplosionRadius = settings.m_ExplosionRadius;
                m_ExplosionForce = settings.m_ExplosionForce;
                m_OwnerImmune = settings.m_OwnerImmune;
                m_LifeTime = settings.m_MineLifeTime;
            }

            if (m_ExplosionFX == null) m_ExplosionFX = fallbackFX;
            if (m_TankMask.value == 0) m_TankMask = tankMask;
        }

        private void Awake()
        {
            if (m_TankMask.value == 0)
                m_TankMask = LayerMask.GetMask("Players");

            // Área de detección (trigger). No bloquea el paso de los tanques.
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = m_TriggerSize;
            trigger.center = new Vector3(0f, m_TriggerSize.y * 0.5f, 0f);

            // Sin prefab: construimos un modelo simple.
            if (GetComponentInChildren<Renderer>() == null)
                BuildDefaultVisual();

            m_Light = GetComponentInChildren<Light>();
        }

        private void OnEnable()
        {
            GameManager.OnRoundStarted += HandleRoundStarted;
            ShellExplosion.OnShellExplosion += HandleNearbyShellExplosion;
            CombatEvents.OnMineExploded += HandleNearbyMineExploded;
        }

        private void OnDisable()
        {
            GameManager.OnRoundStarted -= HandleRoundStarted;
            ShellExplosion.OnShellExplosion -= HandleNearbyShellExplosion;
            CombatEvents.OnMineExploded -= HandleNearbyMineExploded;
        }

        private void OnDestroy()
        {
            // Liberamos los materiales creados por código.
            if (m_LightMaterial != null) Destroy(m_LightMaterial);
            if (m_BodyMaterial != null) Destroy(m_BodyMaterial);
        }

        // ------------------------------------------------------------------ Estados

        private void Update()
        {
            m_Timer += Time.deltaTime;

            if (m_State == MineState.Arming)
            {
                // Parpadeo amarillo rápido mientras se arma.
                bool on = Mathf.Repeat(m_Timer * 8f, 1f) < 0.5f;
                SetLight(m_ArmingColor, on ? 1f : 0.15f);

                if (m_Timer >= m_ArmDelay)
                {
                    m_State = MineState.Armed;
                    m_Timer = 0f;
                }
            }
            else if (m_State == MineState.Armed)
            {
                // Pulso rojo lento: "estoy activa".
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(m_Timer * 5f)), 6f);
                SetLight(m_ArmedColor, 0.25f + pulse);

                if (m_LifeTime > 0f && m_Timer >= m_LifeTime)
                    Destroy(gameObject); // expira sin explotar
            }
        }

        private void OnTriggerEnter(Collider other) { TryTrigger(other); }
        private void OnTriggerStay(Collider other) { TryTrigger(other); }   // por si el tanque ya estaba encima al armarse

        private void TryTrigger(Collider other)
        {
            if (m_State != MineState.Armed) return;

            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.GetComponent<TankHealth>() == null) return;
            if (!m_OwnerCanTrigger && body.gameObject == Owner) return;

            Detonate();
        }

        // ------------------------------------------------------------------ Explosión

        public void Detonate()
        {
            if (m_State == MineState.Exploded) return;
            m_State = MineState.Exploded;

            Vector3 center = transform.position;

            DamageTanks(center);
            if (m_DamageDestructibles)
                DamageDestructibles(center);

            PlayExplosionFX(center);
            CameraControl.TriggerShake(0.55f, 0.3f);

            CombatEvents.RaiseMineExploded(this, center, m_ExplosionRadius);
            Destroy(gameObject);
        }

        private void DamageTanks(Vector3 center)
        {
            Collider[] colliders = Physics.OverlapSphere(center, m_ExplosionRadius, m_TankMask, QueryTriggerInteraction.Ignore);
            HashSet<Rigidbody> processed = new HashSet<Rigidbody>();

            for (int i = 0; i < colliders.Length; i++)
            {
                Rigidbody body = colliders[i].attachedRigidbody;
                if (body == null || !processed.Add(body)) continue;

                TankHealth health = body.GetComponent<TankHealth>();
                if (health == null) continue;
                if (m_OwnerImmune && body.gameObject == Owner) continue;

                // Empuje (mismo método público que usa ShellExplosion).
                TankMovement movement = body.GetComponent<TankMovement>();
                if (movement != null)
                    movement.AddExplosionForce(m_ExplosionForce, center, m_ExplosionRadius);

                // Daño con caída por distancia.
                float damage = CalculateDamage(center, body.position);
                if (damage <= 0f) continue;

                health.TakeDamage(damage);

                // Avisamos al observador para que el daño se atribuya al dueño de la mina.
                TankDamageObserver observer = body.GetComponent<TankDamageObserver>();
                if (observer != null)
                    observer.FlushDamage(Owner, DamageSource.Mine);
            }
        }

        private void DamageDestructibles(Vector3 center)
        {
            Collider[] colliders = Physics.OverlapSphere(center, m_ExplosionRadius, ~0, QueryTriggerInteraction.Ignore);
            HashSet<DestructibleBox> processed = new HashSet<DestructibleBox>();

            for (int i = 0; i < colliders.Length; i++)
            {
                DestructibleBox box = colliders[i].GetComponentInParent<DestructibleBox>();
                if (box == null || !processed.Add(box)) continue;

                float damage = CalculateDamage(center, box.transform.position);
                if (damage > 0f)
                    box.TakeDamage(damage);
            }
        }

        private float CalculateDamage(Vector3 center, Vector3 target)
        {
            float distance = Vector3.Distance(center, target);
            float relative = (m_ExplosionRadius - distance) / m_ExplosionRadius;
            return Mathf.Max(0f, relative * m_MaxDamage);
        }

        private void PlayExplosionFX(Vector3 center)
        {
            if (m_ExplosionFX == null) return;

            ParticleSystem fx = Instantiate(m_ExplosionFX, center + Vector3.up * 0.3f, Quaternion.identity);
            fx.gameObject.SetActive(true);
            fx.Play(true);

            AudioSource audio = fx.GetComponent<AudioSource>();
            if (audio != null) audio.Play();

            Destroy(fx.gameObject, Mathf.Max(2f, fx.main.duration + 0.5f));
        }

        // ------------------------------------------------------------------ Reacciones externas

        private void HandleRoundStarted(int round)
        {
            if (m_ClearOnNewRound)
                Destroy(gameObject);
        }

        private void HandleNearbyShellExplosion(Vector3 position, float radius, float maxDamage)
        {
            if (!m_ChainReaction || m_State != MineState.Armed) return;
            if (Vector3.Distance(position, transform.position) <= radius * 0.5f)
                StartCoroutine(DetonateAfter(0.1f));
        }

        private void HandleNearbyMineExploded(LandMine other, Vector3 position, float radius)
        {
            if (!m_ChainReaction || other == this || m_State != MineState.Armed) return;
            if (Vector3.Distance(position, transform.position) <= radius * 0.6f)
                StartCoroutine(DetonateAfter(0.15f));
        }

        private IEnumerator DetonateAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Detonate();
        }

        // ------------------------------------------------------------------ Visual

        private void SetLight(Color color, float intensity)
        {
            if (m_LightMaterial != null)
            {
                m_LightMaterial.color = color * Mathf.Clamp01(0.4f + intensity);
                m_LightMaterial.SetColor("_EmissionColor", color * intensity * 2f);
            }
            if (m_Light != null)
            {
                m_Light.color = color;
                m_Light.intensity = intensity * 2f;
            }
        }

        private void BuildDefaultVisual()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Cuerpo: disco metálico oscuro.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "MineBody";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            body.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
            m_BodyMaterial = new Material(shader);
            m_BodyMaterial.color = new Color(0.18f, 0.2f, 0.17f);
            body.GetComponent<Renderer>().sharedMaterial = m_BodyMaterial;

            // Luz indicadora en el centro.
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "MineLamp";
            Destroy(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            lamp.transform.localScale = Vector3.one * 0.22f;
            m_LightMaterial = new Material(shader);
            m_LightMaterial.EnableKeyword("_EMISSION");
            lamp.GetComponent<Renderer>().sharedMaterial = m_LightMaterial;

            // Pequeña luz puntual para que se vea en el suelo.
            GameObject lightGO = new GameObject("MineGlow");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            Light pointLight = lightGO.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 2.2f;
            pointLight.shadows = LightShadows.None;
        }
    }
}
