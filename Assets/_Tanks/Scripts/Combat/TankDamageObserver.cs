using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Observa la vida de un tanque SIN modificar TankHealth.
    ///
    /// Idea: guardamos la última vida conocida. Si la vida actual es menor, el tanque recibió
    /// daño; si es mayor, se curó. La diferencia es exactamente el daño que aplicó
    /// TankHealth.TakeDamage (ya con el escudo descontado y 0 si era invencible).
    ///
    /// ¿Quién hizo el daño? Lo decide quien llama a FlushDamage(atacante):
    ///  - CombatTelemetry lo llama justo después de cada explosión de proyectil.
    ///  - LandMine lo llama justo después de su explosión.
    /// Si algo baja la vida sin avisar, LateUpdate lo publica con atacante desconocido.
    ///
    /// Se añade automáticamente en tiempo de ejecución (CombatTelemetry), no hace falta
    /// tocar los prefabs de los tanques.
    /// </summary>
    [DisallowMultipleComponent]
    public class TankDamageObserver : MonoBehaviour
    {
        // Registro estático de todos los observadores (incluye tanques desactivados/muertos).
        private static readonly List<TankDamageObserver> s_All = new List<TankDamageObserver>();
        public static IReadOnlyList<TankDamageObserver> All { get { return s_All; } }

        private TankHealth m_Health;
        private float m_LastKnownHealth;
        private bool m_DeathReported;

        public TankHealth Health { get { return m_Health; } }

        private void Awake()
        {
            m_Health = GetComponent<TankHealth>();
            if (!s_All.Contains(this))
                s_All.Add(this);
        }

        private void OnDestroy()
        {
            s_All.Remove(this);
        }

        private void OnEnable()
        {
            // TankHealth.OnEnable reinicia la vida al empezar cada ronda. Este componente se
            // agrega después de TankHealth, así que su OnEnable corre después y lee la vida nueva.
            Resync();
        }

        /// <summary>Toma la vida actual como punto de partida (no genera eventos).</summary>
        public void Resync()
        {
            if (m_Health == null) m_Health = GetComponent<TankHealth>();
            if (m_Health == null) return;
            m_LastKnownHealth = m_Health.CurrentHealth;
            m_DeathReported = m_Health.CurrentHealth <= 0f;
        }

        private void LateUpdate()
        {
            // Red de seguridad: cambios de vida que nadie reportó (por ejemplo, curaciones de power-ups).
            FlushDamage(null, DamageSource.Unknown);
        }

        /// <summary>
        /// Compara la vida actual con la última conocida y publica el evento correspondiente.
        /// Devuelve el daño bruto detectado (0 si no hubo daño).
        /// </summary>
        public float FlushDamage(GameObject attacker, DamageSource source)
        {
            if (m_Health == null) return 0f;

            float current = m_Health.CurrentHealth;
            float delta = m_LastKnownHealth - current;
            float before = m_LastKnownHealth;
            m_LastKnownHealth = current;

            // Ignoramos micro-diferencias de coma flotante.
            if (Mathf.Abs(delta) < 0.01f)
                return 0f;

            if (delta < 0f)
            {
                // La vida subió: curación.
                CombatEvents.RaiseHealed(gameObject, -delta);
                return 0f;
            }

            bool lethal = current <= 0f && !m_DeathReported;

            DamageInfo info = new DamageInfo();
            info.Victim = gameObject;
            info.Attacker = attacker;
            info.RawAmount = delta;
            info.EffectiveAmount = Mathf.Min(delta, Mathf.Max(0f, before)); // no contamos el exceso bajo 0
            info.Source = source;
            info.VictimHadShield = m_Health.m_HasShield;
            info.WasLethal = lethal;

            CombatEvents.RaiseDamageTaken(info);

            if (lethal)
            {
                m_DeathReported = true;
                CombatEvents.RaiseTankKilled(gameObject, attacker);
            }

            return delta;
        }
    }
}
