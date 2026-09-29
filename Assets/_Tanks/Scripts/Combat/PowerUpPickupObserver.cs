using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Detecta cuándo ESTE tanque recoge un power-up, sin modificar PowerUp.cs.
    ///
    /// Cuando el tanque toca un power-up, Unity llama OnTriggerEnter en los dos objetos.
    /// Anotamos ese power-up como "candidato". PowerUp.cs se destruye a sí mismo solo si
    /// realmente fue recogido (si el tanque ya tenía otro activo, no pasa nada). Por eso,
    /// si el candidato desaparece en un instante, sabemos que ESTE tanque lo recogió.
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerUpPickupObserver : MonoBehaviour
    {
        private const float k_ConfirmWindow = 0.25f; // segundos para confirmar la recogida

        private struct Candidate
        {
            public PowerUp PowerUp;
            public PowerUp.PowerUpType Type;
            public float Time;
        }

        private readonly List<Candidate> m_Candidates = new List<Candidate>();

        private void OnDisable()
        {
            m_Candidates.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            PowerUp powerUp = other.GetComponentInParent<PowerUp>();
            if (powerUp == null) return;

            for (int i = 0; i < m_Candidates.Count; i++)
                if (m_Candidates[i].PowerUp == powerUp) return;

            Candidate c = new Candidate();
            c.PowerUp = powerUp;
            c.Type = powerUp.Type;
            c.Time = Time.time;
            m_Candidates.Add(c);
        }

        private void Update()
        {
            for (int i = m_Candidates.Count - 1; i >= 0; i--)
            {
                Candidate c = m_Candidates[i];

                // "== null" en Unity es verdadero cuando el objeto fue destruido.
                if (c.PowerUp == null)
                {
                    CombatEvents.RaisePowerUpCollected(gameObject, c.Type);
                    m_Candidates.RemoveAt(i);
                }
                else if (Time.time - c.Time > k_ConfirmWindow)
                {
                    // Sigue existiendo: no lo recogimos (ya teníamos un power-up activo).
                    m_Candidates.RemoveAt(i);
                }
            }
        }
    }
}
