using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Muestra números de daño (y curación) flotando sobre los tanques.
    ///
    /// Se suscribe a CombatEvents.OnDamageTaken, que publica TankDamageObserver cuando la vida
    /// de un tanque baja dentro de TankHealth.TakeDamage. El número mostrado es el daño
    /// EXACTO aplicado (ya con el escudo descontado).
    ///
    /// Usa un "pool" de objetos: en vez de crear y destruir textos todo el tiempo, los recicla.
    /// Se coloca una vez en la escena (en el objeto "[Mejoras de Combate]").
    /// </summary>
    public class FloatingTextSpawner : MonoBehaviour
    {
        [Header("Apariencia")]
        [Tooltip("Fuente opcional. Si está vacío se usa la fuente por defecto de TextMeshPro.")]
        [SerializeField] private TMP_FontAsset m_Font;
        [Tooltip("Tamaño base del texto en el mundo.")]
        [SerializeField] private float m_FontSize = 14f;
        [Tooltip("Altura sobre el tanque donde aparece el número.")]
        [SerializeField] private float m_HeightOffset = 1.6f;
        [Tooltip("Cuánto sube el número durante la animación.")]
        [SerializeField] private float m_RiseHeight = 1.3f;
        [Tooltip("Duración total de la animación en segundos.")]
        [SerializeField] private float m_Duration = 1.1f;
        [Tooltip("Dibuja el texto por encima de los objetos para que nunca quede tapado.")]
        [SerializeField] private bool m_RenderOnTop = true;

        [Header("Colores")]
        [SerializeField] private Color m_LowDamageColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color m_MediumDamageColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color m_HighDamageColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color m_ShieldedColor = new Color(0.45f, 0.85f, 1f);
        [SerializeField] private Color m_HealColor = new Color(0.35f, 1f, 0.45f);
        [Tooltip("A partir de este daño se usa el color medio.")]
        [SerializeField] private float m_MediumThreshold = 20f;
        [Tooltip("A partir de este daño se usa el color alto y el texto se agranda.")]
        [SerializeField] private float m_HighThreshold = 45f;

        [Header("Opciones")]
        [SerializeField] private bool m_ShowHealing = true;
        [Tooltip("Muestra un decimal (ej. -37.4) en lugar de redondear a entero.")]
        [SerializeField] private bool m_ShowDecimals = false;
        [Tooltip("Cantidad de textos que se crean al inicio para reutilizar.")]
        [SerializeField] private int m_PrewarmCount = 12;

        private readonly Stack<FloatingCombatText> m_Pool = new Stack<FloatingCombatText>();

        private void Awake()
        {
            for (int i = 0; i < m_PrewarmCount; i++)
                m_Pool.Push(CreateText());
        }

        private void OnEnable()
        {
            CombatEvents.OnDamageTaken += HandleDamage;
            CombatEvents.OnHealed += HandleHeal;
        }

        private void OnDisable()
        {
            CombatEvents.OnDamageTaken -= HandleDamage;
            CombatEvents.OnHealed -= HandleHeal;
        }

        // ------------------------------------------------------------------ Eventos

        private void HandleDamage(DamageInfo info)
        {
            if (info.Victim == null) return;

            if (info.RawAmount < 0.05f) return;
            string amount = m_ShowDecimals ? info.RawAmount.ToString("0.0") : Mathf.Max(1, Mathf.RoundToInt(info.RawAmount)).ToString();

            Color color;
            float scale = 1f;
            if (info.VictimHadShield) color = m_ShieldedColor;
            else if (info.RawAmount >= m_HighThreshold) { color = m_HighDamageColor; scale = 1.35f; }
            else if (info.RawAmount >= m_MediumThreshold) { color = m_MediumDamageColor; scale = 1.15f; }
            else color = m_LowDamageColor;

            string content = "-" + amount;
            if (info.WasLethal)
            {
                content += "\n<size=55%>¡DESTRUIDO!</size>";
                scale *= 1.1f;
            }

            Spawn(info.Victim.transform.position, content, color, scale);
        }

        private void HandleHeal(GameObject tank, float amount)
        {
            if (!m_ShowHealing || tank == null) return;

            // Evitamos mostrar el reinicio de vida al empezar la ronda (el tanque aún está desactivado).
            if (!tank.activeInHierarchy) return;

            int value = Mathf.RoundToInt(amount);
            if (value <= 0) return;

            Spawn(tank.transform.position, "+" + value, m_HealColor, 1f);
        }

        // ------------------------------------------------------------------ API pública

        /// <summary>Muestra un texto flotante cualquiera sobre una posición del mundo.</summary>
        public void Spawn(Vector3 targetPosition, string content, Color color, float scale)
        {
            FloatingCombatText text = m_Pool.Count > 0 ? m_Pool.Pop() : CreateText();
            if (text == null) text = CreateText();

            Vector3 position = targetPosition + Vector3.up * m_HeightOffset;
            text.Play(this, position, content, color, m_FontSize, m_Duration, m_RiseHeight, scale);
        }

        /// <summary>Devuelve el texto al pool cuando termina su animación.</summary>
        public void Release(FloatingCombatText text)
        {
            if (text == null) return;
            text.gameObject.SetActive(false);
            m_Pool.Push(text);
        }

        // ------------------------------------------------------------------ Creación

        private FloatingCombatText CreateText()
        {
            // Se crea ACTIVO para que TextMeshPro ejecute su Awake (carga fuente y material)
            // antes de configurar el contorno; luego se apaga hasta que se use.
            GameObject go = new GameObject("FloatingCombatText");
            go.transform.SetParent(transform, false);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            if (m_Font != null) tmp.font = m_Font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = FontStyles.Bold;
            tmp.fontSize = m_FontSize;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 3f);
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.isOverlay = m_RenderOnTop;

            FloatingCombatText text = go.AddComponent<FloatingCombatText>();
            go.SetActive(false);
            return text;
        }
    }
}
