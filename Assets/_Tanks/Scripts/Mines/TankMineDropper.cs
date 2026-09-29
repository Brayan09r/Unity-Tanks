using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tanks.Complete
{
    /// <summary>
    /// Ajustes de la mecánica de minas. Es una clase aparte para que CombatTelemetry
    /// pueda copiarlos a los tanques cuando agrega el componente automáticamente.
    /// </summary>
    [System.Serializable]
    public class MineDropperSettings
    {
        [Tooltip("Prefab opcional con el componente LandMine. Si está vacío se crea una mina simple por código.")]
        public GameObject m_MinePrefab;
        [Tooltip("Partículas opcionales para la explosión. Si está vacío se usan las del proyectil del tanque.")]
        public ParticleSystem m_ExplosionFX;

        [Header("Uso")]
        [Tooltip("Segundos de recarga entre una mina y la siguiente.")]
        public float m_Cooldown = 6f;
        [Tooltip("Máximo de minas activas por tanque al mismo tiempo.")]
        public int m_MaxActiveMines = 3;
        [Tooltip("Distancia detrás del tanque donde se deja la mina.")]
        public float m_DropDistanceBehind = 1.8f;

        [Header("Mina")]
        [Tooltip("Segundos que tarda en activarse.")]
        public float m_ArmDelay = 1f;
        public float m_MaxDamage = 60f;
        public float m_ExplosionRadius = 4f;
        public float m_ExplosionForce = 40f;
        [Tooltip("La explosión no daña a quien puso la mina.")]
        public bool m_OwnerImmune = true;
        [Tooltip("Segundos antes de que la mina expire sola (0 = nunca).")]
        public float m_MineLifeTime = 45f;

        [Header("Inteligencia artificial")]
        [Tooltip("Los tanques controlados por la IA también usan minas.")]
        public bool m_AIUsesMines = true;
        [Tooltip("La IA suelta una mina si un enemigo está a esta distancia y detrás o a un costado.")]
        public float m_AIDropRange = 7f;
        [Tooltip("Probabilidad [0-1] de soltarla en cada revisión (cada 0.5 s).")]
        [Range(0f, 1f)] public float m_AIDropChance = 0.35f;

        public MineDropperSettings Clone()
        {
            return (MineDropperSettings)MemberwiseClone();
        }
    }

    /// <summary>
    /// Permite a un tanque soltar minas.
    ///   Jugador 1: Clic Derecho o Q (o botón LB del mando).
    ///   Jugador 2: P.
    ///   IA: heurística simple (enemigo cerca y detrás).
    ///
    /// Solo funciona cuando el GameManager tiene habilitado el control del tanque
    /// (usamos TankShooting.enabled como señal, sin modificar TankShooting).
    /// </summary>
    [DisallowMultipleComponent]
    public class TankMineDropper : MonoBehaviour
    {
        [SerializeField] private MineDropperSettings m_Settings = new MineDropperSettings();

        private const float k_AIDecisionInterval = 0.5f;

        private TankShooting m_Shooting;
        private readonly List<LandMine> m_ActiveMines = new List<LandMine>();
        private float m_CooldownTimer;
        private float m_AITimer;
        private LayerMask m_TankMask;
        private LayerMask m_GroundMask;

        /// <summary>Segundos que faltan para poder soltar otra mina.</summary>
        public float CooldownRemaining { get { return Mathf.Max(0f, m_CooldownTimer); } }
        /// <summary>0 = lista, 1 = recién usada. Útil para una barra de recarga.</summary>
        public float CooldownRatio { get { return m_Settings.m_Cooldown > 0f ? CooldownRemaining / m_Settings.m_Cooldown : 0f; } }
        public int ActiveMineCount { get { PruneMines(); return m_ActiveMines.Count; } }

        /// <summary>Copia ajustes (la usa CombatTelemetry al agregar el componente por código).</summary>
        public void ApplySettings(MineDropperSettings settings)
        {
            if (settings != null)
                m_Settings = settings.Clone();
        }

        private void Awake()
        {
            m_Shooting = GetComponent<TankShooting>();
            m_TankMask = LayerMask.GetMask("Players");
            m_GroundMask = ~m_TankMask;
        }

        private void OnEnable()
        {
            // Cada ronda empieza con la mina disponible (el tanque se reactiva en TankManager.Reset).
            m_CooldownTimer = 0f;
            m_AITimer = 0f;
        }

        private void Update()
        {
            // Si el GameManager desactivó el disparo (inicio/fin de ronda), no se pueden soltar minas.
            if (m_Shooting == null || !m_Shooting.enabled) return;

            if (m_CooldownTimer > 0f)
                m_CooldownTimer -= Time.deltaTime;

            bool wantsToDrop = m_Shooting.m_IsComputerControlled ? AIWantsToDrop() : ReadHumanInput();
            if (wantsToDrop)
                TryDropMine();
        }

        // ------------------------------------------------------------------ Entrada

        private bool ReadHumanInput()
        {
            int player = m_Shooting.ControlIndex > 0 ? m_Shooting.ControlIndex : m_Shooting.m_PlayerNumber;

            if (player == 1)
            {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) return true;
                if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) return true;
                if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame) return true;
            }
            else if (player == 2)
            {
                if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) return true;
            }
            return false;
        }

        private bool AIWantsToDrop()
        {
            if (!m_Settings.m_AIUsesMines) return false;

            m_AITimer -= Time.deltaTime;
            if (m_AITimer > 0f) return false;
            m_AITimer = k_AIDecisionInterval;

            if (m_CooldownTimer > 0f) return false;

            GameManager manager = GameManager.Instance;
            if (manager == null || manager.m_SpawnPoints == null) return false;

            for (int i = 0; i < manager.m_SpawnPoints.Length; i++)
            {
                TankManager slot = manager.m_SpawnPoints[i];
                if (slot == null || slot.m_Instance == null) continue;

                GameObject enemy = slot.m_Instance;
                if (enemy == gameObject || !enemy.activeInHierarchy) continue;

                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.magnitude > m_Settings.m_AIDropRange) continue;

                // Enemigo detrás o a un costado: probablemente nos persigue y pasará por encima.
                float facing = Vector3.Dot(transform.forward, toEnemy.normalized);
                if (facing < 0.2f && Random.value < m_Settings.m_AIDropChance)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Soltar mina

        /// <summary>Intenta soltar una mina. Devuelve false si está en recarga o se alcanzó el máximo.</summary>
        public bool TryDropMine()
        {
            if (m_CooldownTimer > 0f) return false;

            PruneMines();
            if (m_ActiveMines.Count >= m_Settings.m_MaxActiveMines) return false;

            Vector3 position = GetDropPosition();
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            GameObject mineObject;
            if (m_Settings.m_MinePrefab != null)
                mineObject = Instantiate(m_Settings.m_MinePrefab, position, rotation);
            else
            {
                mineObject = new GameObject("LandMine");
                mineObject.transform.SetPositionAndRotation(position, rotation);
            }

            LandMine mine = mineObject.GetComponent<LandMine>();
            if (mine == null) mine = mineObject.AddComponent<LandMine>();

            mine.Setup(gameObject, m_Settings, GetExplosionFX(), GetTankMask());

            m_ActiveMines.Add(mine);
            m_CooldownTimer = m_Settings.m_Cooldown;

            CombatEvents.RaiseMineDropped(gameObject, mine);
            return true;
        }

        private Vector3 GetDropPosition()
        {
            Vector3 behind = transform.position - transform.forward * m_Settings.m_DropDistanceBehind;

            // Buscamos el suelo debajo del punto (ignorando tanques y triggers).
            RaycastHit hit;
            if (Physics.Raycast(behind + Vector3.up * 3f, Vector3.down, out hit, 10f, m_GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.01f;

            behind.y = 0.01f;
            return behind;
        }

        /// <summary>Reutiliza las partículas de explosión del proyectil del tanque si no hay otras.</summary>
        private ParticleSystem GetExplosionFX()
        {
            if (m_Settings.m_ExplosionFX != null) return m_Settings.m_ExplosionFX;

            if (m_Shooting != null && m_Shooting.m_Shell != null)
            {
                ShellExplosion shell = m_Shooting.m_Shell.GetComponent<ShellExplosion>();
                if (shell != null) return shell.m_ExplosionParticles;
            }
            return null;
        }

        /// <summary>Usa la misma capa de tanques que el proyectil original.</summary>
        private LayerMask GetTankMask()
        {
            if (m_Shooting != null && m_Shooting.m_Shell != null)
            {
                ShellExplosion shell = m_Shooting.m_Shell.GetComponent<ShellExplosion>();
                if (shell != null && shell.m_TankMask.value != 0) return shell.m_TankMask;
            }
            return m_TankMask;
        }

        private void PruneMines()
        {
            // Las minas destruidas (explotaron, expiraron o se limpiaron) se vuelven "null" en Unity.
            m_ActiveMines.RemoveAll(m => m == null);
        }
    }
}
