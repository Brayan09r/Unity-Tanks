using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Punto central de la telemetría de combate. Se coloca UNA vez en la escena.
    ///
    /// Responsabilidades:
    ///  1. Instalador: al empezar cada ronda agrega a cada tanque los componentes observadores
    ///     (TankDamageObserver, PowerUpPickupObserver y, opcionalmente, TankMineDropper).
    ///     Así no hay que editar los prefabs de los tanques.
    ///  2. Etiqueta los proyectiles nuevos con ShellOwnerTag (quién disparó).
    ///  3. Escucha ShellExplosion.OnShellExplosion (evento que ya existía) para atribuir
    ///     el daño de cada explosión al tanque que disparó.
    /// </summary>
    public class CombatTelemetry : MonoBehaviour
    {
        [Header("Instalación automática en los tanques")]
        [Tooltip("Agrega TankDamageObserver y PowerUpPickupObserver a cada tanque al iniciar la ronda.")]
        [SerializeField] private bool m_AutoInstallObservers = true;

        [Tooltip("Agrega TankMineDropper a los tanques que no lo tengan ya en su prefab.")]
        [SerializeField] private bool m_AutoInstallMineDropper = true;

        [Tooltip("Configuración que se copia a los TankMineDropper agregados automáticamente.")]
        [SerializeField] private MineDropperSettings m_MineSettings = new MineDropperSettings();

        public static CombatTelemetry Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CombatTelemetry] Hay más de uno en la escena; se desactiva el duplicado.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            ShellExplosion.OnShellExplosion += HandleShellExplosion;
            GameManager.OnRoundStarted += HandleRoundStarted;
        }

        private void OnDisable()
        {
            ShellExplosion.OnShellExplosion -= HandleShellExplosion;
            GameManager.OnRoundStarted -= HandleRoundStarted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Instalador

        private void HandleRoundStarted(int roundNumber)
        {
            InstallOnAllTanks();
        }

        /// <summary>Agrega los componentes aditivos a los tanques que creó el GameManager.</summary>
        public void InstallOnAllTanks()
        {
            // Usamos la lista pública del GameManager para no tocar tanques "de vitrina" del menú.
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.m_SpawnPoints == null) return;

            for (int i = 0; i < manager.m_SpawnPoints.Length; i++)
            {
                TankManager slot = manager.m_SpawnPoints[i];
                if (slot == null || slot.m_Instance == null) continue;

                GameObject tank = slot.m_Instance;
                if (tank.GetComponent<TankHealth>() == null || tank.GetComponent<TankShooting>() == null) continue;

                if (m_AutoInstallObservers)
                {
                    if (tank.GetComponent<TankDamageObserver>() == null)
                        tank.AddComponent<TankDamageObserver>();
                    if (tank.GetComponent<PowerUpPickupObserver>() == null)
                        tank.AddComponent<PowerUpPickupObserver>();
                }

                if (m_AutoInstallMineDropper && tank.GetComponent<TankMineDropper>() == null)
                {
                    TankMineDropper dropper = tank.AddComponent<TankMineDropper>();
                    dropper.ApplySettings(m_MineSettings);
                }
            }
        }

        // ------------------------------------------------------------------ Proyectiles

        private void LateUpdate()
        {
            // TankShooting.Fire() crea los proyectiles durante Update. En LateUpdate del mismo
            // frame todavía no se han movido (la física corre en el siguiente FixedUpdate),
            // así que siguen en la boca del cañón y podemos identificar al dueño.
            ShellExplosion[] shells = FindObjectsByType<ShellExplosion>(FindObjectsSortMode.None);
            for (int i = 0; i < shells.Length; i++)
            {
                if (shells[i].GetComponent<ShellOwnerTag>() == null)
                    shells[i].gameObject.AddComponent<ShellOwnerTag>(); // su Awake resuelve el dueño
            }
        }

        /// <summary>
        /// ShellExplosion invoca este evento DESPUÉS de aplicar TakeDamage a los tanques,
        /// por eso aquí las vidas ya bajaron y podemos medir cuánto daño hizo este proyectil.
        /// </summary>
        private void HandleShellExplosion(Vector3 position, float radius, float maxDamage)
        {
            ShellOwnerTag shell = ShellOwnerTag.FindAt(position);
            GameObject attacker = shell != null ? shell.Owner : null;

            bool hitEnemy = false;
            var observers = TankDamageObserver.All;
            for (int i = 0; i < observers.Count; i++)
            {
                TankDamageObserver observer = observers[i];
                if (observer == null) continue;

                float damage = observer.FlushDamage(attacker, DamageSource.Shell);
                if (damage > 0f && observer.gameObject != attacker)
                    hitEnemy = true;
            }

            if (shell != null)
                shell.Resolve(hitEnemy);
        }
    }
}
