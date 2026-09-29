using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Estadísticas de UN jugador (clase de datos, sin lógica de Unity).
    /// Se usa dos veces por jugador: una para la ronda actual y otra acumulada de la partida.
    /// </summary>
    [System.Serializable]
    public class PlayerMatchStats
    {
        public GameObject Tank;
        public int PlayerNumber;
        public string DisplayName;       // texto con color, ej. "<color=#FF0000>JUGADOR 1</color>"
        public Color PlayerColor = Color.white;
        public bool IsComputer;

        public int ShotsFired;
        public int ShotsHit;
        public float DamageDealt;        // daño efectivo causado a enemigos (proyectiles + minas)
        public float DamageTaken;
        public int PowerUpsCollected;
        public int Kills;
        public int MinesPlaced;

        /// <summary>Efectividad de disparo en porcentaje (0-100).</summary>
        public float Accuracy
        {
            get { return ShotsFired > 0 ? (ShotsHit * 100f) / ShotsFired : 0f; }
        }

        /// <summary>
        /// Puntaje para elegir al MVP:
        ///   daño causado + bajas × peso + power-ups × peso + precisión × peso.
        /// Los pesos se configuran en MatchStatsTracker desde el Inspector.
        /// </summary>
        public float GetScore(MvpWeights w)
        {
            return DamageDealt * w.DamagePoint
                 + Kills * w.KillPoints
                 + PowerUpsCollected * w.PowerUpPoints
                 + Accuracy * w.AccuracyPoint;
        }

        /// <summary>Copia congelada para mostrar en pantalla (no cambia si llegan eventos tarde).</summary>
        public PlayerMatchStats Clone()
        {
            return (PlayerMatchStats)MemberwiseClone();
        }

        public void Reset()
        {
            ShotsFired = 0;
            ShotsHit = 0;
            DamageDealt = 0f;
            DamageTaken = 0f;
            PowerUpsCollected = 0;
            Kills = 0;
            MinesPlaced = 0;
        }
    }

    /// <summary>Pesos de la fórmula del MVP (editables en el Inspector).</summary>
    [System.Serializable]
    public class MvpWeights
    {
        [Tooltip("Puntos por cada punto de daño causado.")]
        public float DamagePoint = 1f;
        [Tooltip("Puntos por cada tanque destruido.")]
        public float KillPoints = 50f;
        [Tooltip("Puntos por cada power-up recogido.")]
        public float PowerUpPoints = 10f;
        [Tooltip("Puntos por cada 1% de efectividad de disparo.")]
        public float AccuracyPoint = 0.5f;
    }
}
