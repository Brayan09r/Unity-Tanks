using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Resumen que se envía a la interfaz al terminar una ronda.
    /// </summary>
    public class MatchSummary
    {
        public int RoundNumber;
        public bool IsGameOver;
        public MvpWeights Weights;
        public List<PlayerMatchStats> RoundStats = new List<PlayerMatchStats>();  // ordenadas por puntaje
        public List<PlayerMatchStats> MatchStats = new List<PlayerMatchStats>();  // acumuladas, ordenadas
        public PlayerMatchStats RoundMvp;
        public PlayerMatchStats MatchMvp;

        public float ScoreOf(PlayerMatchStats stats)
        {
            return stats != null ? stats.GetScore(Weights) : 0f;
        }
    }

    /// <summary>
    /// Lleva las estadísticas de cada jugador (por ronda y acumuladas de toda la partida)
    /// y elige al MVP.
    ///
    /// No lee ni modifica el código original: solo escucha CombatEvents y
    /// GameManager.OnRoundStarted. Para saber que la ronda terminó aplica la misma regla
    /// que usa el GameManager: cuando queda 1 tanque activo o ninguno.
    /// </summary>
    public class MatchStatsTracker : MonoBehaviour
    {
        [Header("MVP")]
        [SerializeField] private MvpWeights m_Weights = new MvpWeights();

        [Header("Tiempos")]
        [Tooltip("Espera tras detectar el fin de ronda, para que el GameManager sume la victoria antes de armar el resumen.")]
        [SerializeField] private float m_SummaryDelay = 0.25f;

        [Header("Depuración")]
        [SerializeField] private bool m_LogSummaryToConsole = true;

        public static MatchStatsTracker Instance { get; private set; }

        /// <summary>Se dispara cuando el resumen de la ronda está listo (lo usa MatchStatsPanel).</summary>
        public static event Action<MatchSummary> OnRoundSummaryReady;
        /// <summary>Se dispara al empezar una ronda (para ocultar el panel).</summary>
        public static event Action OnRoundSummaryCleared;

        private readonly Dictionary<GameObject, PlayerMatchStats> m_RoundStats = new Dictionary<GameObject, PlayerMatchStats>();
        private readonly Dictionary<GameObject, PlayerMatchStats> m_MatchStats = new Dictionary<GameObject, PlayerMatchStats>();

        private int m_RoundNumber;
        private bool m_RoundInProgress;

        public MvpWeights Weights { get { return m_Weights; } }

        // ------------------------------------------------------------------ Ciclo de vida

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            GameManager.OnRoundStarted += HandleRoundStarted;
            CombatEvents.OnShotFired += HandleShotFired;
            CombatEvents.OnShotResolved += HandleShotResolved;
            CombatEvents.OnDamageTaken += HandleDamageTaken;
            CombatEvents.OnTankKilled += HandleTankKilled;
            CombatEvents.OnPowerUpCollected += HandlePowerUpCollected;
            CombatEvents.OnMineDropped += HandleMineDropped;
        }

        private void OnDisable()
        {
            GameManager.OnRoundStarted -= HandleRoundStarted;
            CombatEvents.OnShotFired -= HandleShotFired;
            CombatEvents.OnShotResolved -= HandleShotResolved;
            CombatEvents.OnDamageTaken -= HandleDamageTaken;
            CombatEvents.OnTankKilled -= HandleTankKilled;
            CombatEvents.OnPowerUpCollected -= HandlePowerUpCollected;
            CombatEvents.OnMineDropped -= HandleMineDropped;
        }

        private void Update()
        {
            if (!m_RoundInProgress) return;

            // Misma condición que GameManager.OneTankLeft(): queda uno o ninguno.
            if (CountActiveTanks() <= 1)
            {
                m_RoundInProgress = false;
                StartCoroutine(PublishSummaryAfterDelay());
            }
        }

        // ------------------------------------------------------------------ Rondas

        private void HandleRoundStarted(int roundNumber)
        {
            m_RoundNumber = roundNumber;
            RegisterPlayers();

            foreach (var stats in m_RoundStats.Values)
                stats.Reset();

            m_RoundInProgress = true;
            if (OnRoundSummaryCleared != null) OnRoundSummaryCleared();
        }

        /// <summary>Crea las fichas de estadísticas de los tanques que instanció el GameManager.</summary>
        private void RegisterPlayers()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.m_SpawnPoints == null) return;

            for (int i = 0; i < manager.m_SpawnPoints.Length; i++)
            {
                TankManager slot = manager.m_SpawnPoints[i];
                if (slot == null || slot.m_Instance == null) continue;

                if (!m_RoundStats.ContainsKey(slot.m_Instance))
                    m_RoundStats[slot.m_Instance] = CreateStats(slot);
                if (!m_MatchStats.ContainsKey(slot.m_Instance))
                    m_MatchStats[slot.m_Instance] = CreateStats(slot);
            }
        }

        private static PlayerMatchStats CreateStats(TankManager slot)
        {
            PlayerMatchStats stats = new PlayerMatchStats();
            stats.Tank = slot.m_Instance;
            stats.PlayerNumber = slot.m_PlayerNumber;
            stats.PlayerColor = slot.m_PlayerColor;
            stats.IsComputer = slot.m_ComputerControlled;

            string name = !string.IsNullOrEmpty(slot.m_ColoredPlayerText)
                ? slot.m_ColoredPlayerText
                : "JUGADOR " + slot.m_PlayerNumber;
            stats.DisplayName = slot.m_ComputerControlled ? name + " <size=70%>(IA)</size>" : name;
            return stats;
        }

        private int CountActiveTanks()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.m_SpawnPoints == null) return int.MaxValue;

            int alive = 0;
            int total = 0;
            for (int i = 0; i < manager.m_SpawnPoints.Length; i++)
            {
                TankManager slot = manager.m_SpawnPoints[i];
                if (slot == null || slot.m_Instance == null) continue;
                total++;
                if (slot.m_Instance.activeSelf) alive++;
            }
            return total == 0 ? int.MaxValue : alive;
        }

        private IEnumerator PublishSummaryAfterDelay()
        {
            yield return new WaitForSeconds(m_SummaryDelay);

            MatchSummary summary = BuildSummary();

            if (m_LogSummaryToConsole)
                LogSummary(summary);

            if (OnRoundSummaryReady != null) OnRoundSummaryReady(summary);
        }

        // ------------------------------------------------------------------ Resumen y MVP

        public MatchSummary BuildSummary()
        {
            MatchSummary summary = new MatchSummary();
            summary.RoundNumber = m_RoundNumber;
            summary.Weights = m_Weights;
            summary.IsGameOver = IsGameOver();

            foreach (var stats in m_RoundStats.Values) summary.RoundStats.Add(stats.Clone());
            foreach (var stats in m_MatchStats.Values) summary.MatchStats.Add(stats.Clone());

            SortByScore(summary.RoundStats);
            SortByScore(summary.MatchStats);

            summary.RoundMvp = PickMvp(summary.RoundStats);
            summary.MatchMvp = PickMvp(summary.MatchStats);
            return summary;
        }

        private void SortByScore(List<PlayerMatchStats> list)
        {
            list.Sort((a, b) =>
            {
                int byScore = b.GetScore(m_Weights).CompareTo(a.GetScore(m_Weights));
                if (byScore != 0) return byScore;
                return a.DamageTaken.CompareTo(b.DamageTaken); // desempate: quien recibió menos daño
            });
        }

        /// <summary>El MVP es el primero de la lista ordenada, si hizo algo (puntaje mayor a 0).</summary>
        private PlayerMatchStats PickMvp(List<PlayerMatchStats> sorted)
        {
            if (sorted.Count == 0) return null;
            PlayerMatchStats best = sorted[0];
            return best.GetScore(m_Weights) > 0f ? best : null;
        }

        private static bool IsGameOver()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.m_SpawnPoints == null) return false;

            for (int i = 0; i < manager.m_SpawnPoints.Length; i++)
            {
                TankManager slot = manager.m_SpawnPoints[i];
                if (slot != null && slot.m_Instance != null && slot.m_Wins >= manager.m_NumRoundsToWin)
                    return true;
            }
            return false;
        }

        private void LogSummary(MatchSummary summary)
        {
            string log = "[MatchStats] Ronda " + summary.RoundNumber + (summary.IsGameOver ? " (FIN DE PARTIDA)" : "") + "\n";
            for (int i = 0; i < summary.RoundStats.Count; i++)
            {
                PlayerMatchStats s = summary.RoundStats[i];
                log += string.Format("  J{0}: disparos {1}, aciertos {2} ({3:0}%), daño {4:0}, power-ups {5}, bajas {6}, puntaje {7:0}\n",
                    s.PlayerNumber, s.ShotsFired, s.ShotsHit, s.Accuracy, s.DamageDealt, s.PowerUpsCollected, s.Kills, s.GetScore(m_Weights));
            }
            log += "  MVP ronda: " + (summary.RoundMvp != null ? "J" + summary.RoundMvp.PlayerNumber : "ninguno");
            log += " | MVP partida: " + (summary.MatchMvp != null ? "J" + summary.MatchMvp.PlayerNumber : "ninguno");
            Debug.Log(log);
        }

        // ------------------------------------------------------------------ Eventos de combate

        /// <summary>Aplica un cambio a la ficha de la ronda y a la acumulada del mismo tanque.</summary>
        private void ForBoth(GameObject tank, Action<PlayerMatchStats> change)
        {
            if (tank == null) return;
            PlayerMatchStats stats;
            if (m_RoundStats.TryGetValue(tank, out stats)) change(stats);
            if (m_MatchStats.TryGetValue(tank, out stats)) change(stats);
        }

        private void HandleShotFired(GameObject shooter)
        {
            ForBoth(shooter, s => s.ShotsFired++);
        }

        private void HandleShotResolved(GameObject shooter, bool hitEnemy)
        {
            if (hitEnemy) ForBoth(shooter, s => s.ShotsHit++);
        }

        private void HandleDamageTaken(DamageInfo info)
        {
            float amount = info.EffectiveAmount;
            ForBoth(info.Victim, s => s.DamageTaken += amount);

            // El daño a uno mismo no suma como "daño causado".
            if (info.Attacker != null && !info.IsSelfDamage)
                ForBoth(info.Attacker, s => s.DamageDealt += amount);
        }

        private void HandleTankKilled(GameObject victim, GameObject killer)
        {
            if (killer != null && killer != victim)
                ForBoth(killer, s => s.Kills++);
        }

        private void HandlePowerUpCollected(GameObject tank, PowerUp.PowerUpType type)
        {
            ForBoth(tank, s => s.PowerUpsCollected++);
        }

        private void HandleMineDropped(GameObject owner, LandMine mine)
        {
            ForBoth(owner, s => s.MinesPlaced++);
        }
    }
}
