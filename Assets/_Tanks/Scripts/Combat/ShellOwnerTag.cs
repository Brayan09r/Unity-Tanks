using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// "Etiqueta de dueño" que se pega a cada proyectil para saber QUÉ tanque lo disparó.
    ///
    /// TankShooting.Fire() crea el proyectil exactamente en la posición de m_FireTransform.
    /// Al aparecer, buscamos el tanque cuyo cañón está en ese punto: ese es el dueño.
    /// Así no tuvimos que modificar TankShooting ni ShellExplosion.
    ///
    /// CombatTelemetry la agrega automáticamente a cada proyectil nuevo; opcionalmente
    /// también puede ponerse directamente en el prefab CompleteShell.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShellOwnerTag : MonoBehaviour
    {
        private static readonly List<ShellOwnerTag> s_Active = new List<ShellOwnerTag>();

        /// <summary>Distancia máxima entre el cañón y el proyectil para considerarlo "suyo".</summary>
        private const float k_MaxOwnerDistance = 2.5f;

        public GameObject Owner { get; private set; }
        public bool IsResolved { get; private set; }

        private bool m_Initialized;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Resuelve el dueño y publica el evento "disparo realizado". Solo corre una vez.</summary>
        public void Initialize()
        {
            if (m_Initialized) return;
            m_Initialized = true;

            s_Active.Add(this);
            Owner = FindOwner(transform.position);

            if (Owner != null)
                CombatEvents.RaiseShotFired(Owner);
        }

        private void OnDestroy()
        {
            s_Active.Remove(this);

            // Si el proyectil desapareció sin explotar (se acabó su tiempo de vida), es un fallo.
            if (!IsResolved && Owner != null)
            {
                IsResolved = true;
                CombatEvents.RaiseShotResolved(Owner, false);
            }
        }

        /// <summary>Marca el disparo como terminado e informa si dañó a un enemigo.</summary>
        public void Resolve(bool hitEnemy)
        {
            if (IsResolved) return;
            IsResolved = true;
            if (Owner != null)
                CombatEvents.RaiseShotResolved(Owner, hitEnemy);
        }

        /// <summary>Busca el proyectil vivo que está en la posición indicada (la de una explosión).</summary>
        public static ShellOwnerTag FindAt(Vector3 position)
        {
            ShellOwnerTag best = null;
            float bestSqr = 0.25f; // tolerancia de 0.5 unidades
            for (int i = 0; i < s_Active.Count; i++)
            {
                ShellOwnerTag tag = s_Active[i];
                if (tag == null || tag.IsResolved) continue;

                float sqr = (tag.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = tag;
                }
            }
            return best;
        }

        /// <summary>Devuelve el tanque cuyo punto de disparo está más cerca de la posición dada.</summary>
        private static GameObject FindOwner(Vector3 spawnPosition)
        {
            TankShooting[] shooters = FindObjectsByType<TankShooting>(FindObjectsSortMode.None);
            GameObject best = null;
            float bestSqr = k_MaxOwnerDistance * k_MaxOwnerDistance;

            for (int i = 0; i < shooters.Length; i++)
            {
                TankShooting shooter = shooters[i];
                if (shooter == null || shooter.m_FireTransform == null) continue;

                float sqr = (shooter.m_FireTransform.position - spawnPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = shooter.gameObject;
                }
            }
            return best;
        }
    }
}
