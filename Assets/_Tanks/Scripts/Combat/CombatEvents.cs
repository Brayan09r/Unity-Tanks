using System;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Origen del daño. Sirve para que las estadísticas distingan disparos de minas.
    /// </summary>
    public enum DamageSource
    {
        Unknown,
        Shell,
        Mine
    }

    /// <summary>
    /// "Bus de eventos" de combate (patrón Observer).
    ///
    /// Los scripts ORIGINALES del juego no saben que esta clase existe: nadie modificó
    /// TankHealth, TankShooting ni ShellExplosion. Unos componentes "observadores"
    /// (CombatTelemetry, TankDamageObserver, PowerUpPickupObserver, LandMine) detectan
    /// lo que pasa y lo publican aquí. Los sistemas nuevos (estadísticas, textos
    /// flotantes) solo se suscriben a estos eventos, sin conocerse entre ellos.
    /// </summary>
    public static class CombatEvents
    {
        /// <summary>Un tanque disparó un proyectil. (tirador)</summary>
        public static event Action<GameObject> OnShotFired;

        /// <summary>Un proyectil terminó su vuelo. (tirador, ¿dañó a un enemigo?)</summary>
        public static event Action<GameObject, bool> OnShotResolved;

        /// <summary>Un tanque recibió daño real. (víctima, atacante o null, daño bruto, daño efectivo, origen)</summary>
        public static event Action<DamageInfo> OnDamageTaken;

        /// <summary>Un tanque recuperó vida. (tanque, cantidad)</summary>
        public static event Action<GameObject, float> OnHealed;

        /// <summary>Un tanque fue destruido. (víctima, atacante o null)</summary>
        public static event Action<GameObject, GameObject> OnTankKilled;

        /// <summary>Un tanque recogió un power-up. (tanque, tipo)</summary>
        public static event Action<GameObject, PowerUp.PowerUpType> OnPowerUpCollected;

        /// <summary>Un tanque soltó una mina. (dueño, mina)</summary>
        public static event Action<GameObject, LandMine> OnMineDropped;

        /// <summary>Una mina explotó. (mina, posición, radio)</summary>
        public static event Action<LandMine, Vector3, float> OnMineExploded;

        // ---------- Métodos para publicar (los eventos solo se pueden invocar desde esta clase) ----------

        public static void RaiseShotFired(GameObject shooter)
        {
            if (OnShotFired != null) OnShotFired(shooter);
        }

        public static void RaiseShotResolved(GameObject shooter, bool hitEnemy)
        {
            if (OnShotResolved != null) OnShotResolved(shooter, hitEnemy);
        }

        public static void RaiseDamageTaken(DamageInfo info)
        {
            if (OnDamageTaken != null) OnDamageTaken(info);
        }

        public static void RaiseHealed(GameObject tank, float amount)
        {
            if (OnHealed != null) OnHealed(tank, amount);
        }

        public static void RaiseTankKilled(GameObject victim, GameObject killer)
        {
            if (OnTankKilled != null) OnTankKilled(victim, killer);
        }

        public static void RaisePowerUpCollected(GameObject tank, PowerUp.PowerUpType type)
        {
            if (OnPowerUpCollected != null) OnPowerUpCollected(tank, type);
        }

        public static void RaiseMineDropped(GameObject owner, LandMine mine)
        {
            if (OnMineDropped != null) OnMineDropped(owner, mine);
        }

        public static void RaiseMineExploded(LandMine mine, Vector3 position, float radius)
        {
            if (OnMineExploded != null) OnMineExploded(mine, position, radius);
        }
    }

    /// <summary>
    /// Datos de un golpe recibido.
    /// RawAmount = daño exacto que bajó la barra de vida (lo que muestra el texto flotante).
    /// EffectiveAmount = daño sin contar el "exceso" por debajo de 0 de vida (lo que suman las estadísticas).
    /// </summary>
    public struct DamageInfo
    {
        public GameObject Victim;
        public GameObject Attacker;
        public float RawAmount;
        public float EffectiveAmount;
        public DamageSource Source;
        public bool VictimHadShield;
        public bool WasLethal;

        public bool IsSelfDamage
        {
            get { return Attacker != null && Attacker == Victim; }
        }
    }
}
