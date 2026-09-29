using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    /// <summary>
    /// Panel de estadísticas que aparece en la pantalla de fin de ronda (RoundEnding).
    ///
    /// Se construye solo por código al iniciar (Canvas + tabla con TextMeshPro), así que no
    /// hay que armar la UI a mano. Los colores, el ancho y la posición se ajustan en el Inspector.
    /// Escucha MatchStatsTracker.OnRoundSummaryReady para mostrarse y
    /// MatchStatsTracker.OnRoundSummaryCleared para ocultarse al empezar la siguiente ronda.
    /// </summary>
    public class MatchStatsPanel : MonoBehaviour
    {
        [Header("Disposición")]
        [Tooltip("Ancho del panel en píxeles de referencia (1920x1080).")]
        [SerializeField] private float m_PanelWidth = 1180f;
        [Tooltip("Separación desde el borde inferior de la pantalla.")]
        [SerializeField] private float m_BottomMargin = 10f;
        [Tooltip("Escala general del panel. Bájala si tapa el mensaje de victorias del GameManager.")]
        [Range(0.5f, 1.2f)]
        [SerializeField] private float m_PanelScale = 0.8f;
        [Tooltip("Orden de dibujo del Canvas (más alto = encima de otros Canvas).")]
        [SerializeField] private int m_SortingOrder = 60;
        [Tooltip("Fuente opcional para todo el panel.")]
        [SerializeField] private TMP_FontAsset m_Font;

        [Header("Colores")]
        [SerializeField] private Color m_BackgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        [SerializeField] private Color m_AccentColor = new Color(1f, 0.82f, 0.4f, 1f);
        [SerializeField] private Color m_HeaderTextColor = new Color(0.65f, 0.7f, 0.78f, 1f);
        [SerializeField] private Color m_RowTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        [SerializeField] private Color m_RowColorA = new Color(1f, 1f, 1f, 0.04f);
        [SerializeField] private Color m_RowColorB = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color m_MvpRowColor = new Color(1f, 0.82f, 0.4f, 0.22f);

        [Header("Animación")]
        [SerializeField] private float m_FadeDuration = 0.35f;

        // Columnas de la tabla: título y ancho.
        private static readonly string[] k_Headers =
            { "JUGADOR", "DISPAROS", "ACIERTOS", "EFECTIVIDAD", "DAÑO", "POWER-UPS", "BAJAS", "PUNTAJE" };
        private static readonly float[] k_Widths = { 260f, 105f, 105f, 140f, 100f, 125f, 85f, 115f };

        private Canvas m_Canvas;
        private CanvasGroup m_Group;
        private RectTransform m_Panel;
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI m_MvpBanner;
        private TextMeshProUGUI m_Footer;
        private RectTransform m_RowsContainer;
        private readonly List<GameObject> m_RowObjects = new List<GameObject>();
        private Coroutine m_Animation;

        // ------------------------------------------------------------------ Ciclo de vida

        private void Awake()
        {
            BuildLayout();
            HideImmediate();
        }

        private void OnEnable()
        {
            MatchStatsTracker.OnRoundSummaryReady += Show;
            MatchStatsTracker.OnRoundSummaryCleared += Hide;
        }

        private void OnDisable()
        {
            MatchStatsTracker.OnRoundSummaryReady -= Show;
            MatchStatsTracker.OnRoundSummaryCleared -= Hide;
        }

        // ------------------------------------------------------------------ Mostrar / ocultar

        public void Show(MatchSummary summary)
        {
            if (summary == null) return;

            // En la última ronda mostramos el acumulado de toda la partida.
            bool final = summary.IsGameOver;
            List<PlayerMatchStats> rows = final ? summary.MatchStats : summary.RoundStats;
            PlayerMatchStats mvp = final ? summary.MatchMvp : summary.RoundMvp;

            m_Title.text = final ? "RESUMEN FINAL DE LA PARTIDA" : "ESTADÍSTICAS  -  RONDA " + summary.RoundNumber;

            string mvpLabel = final ? "MVP DE LA PARTIDA" : "MVP DE LA RONDA";
            m_MvpBanner.text = mvp != null
                ? mvpLabel + ":  " + mvp.DisplayName + "   <color=#FFFFFF>" + summary.ScoreOf(mvp).ToString("0") + " pts</color>"
                : mvpLabel + ":  <color=#AAAAAA>sin MVP (nadie causó daño)</color>";

            if (final)
            {
                m_Footer.text = summary.RoundMvp != null
                    ? "MVP de la última ronda: " + summary.RoundMvp.DisplayName
                    : "";
            }
            else
            {
                m_Footer.text = summary.MatchMvp != null
                    ? "MVP acumulado de la partida: " + summary.MatchMvp.DisplayName + "  (" + summary.ScoreOf(summary.MatchMvp).ToString("0") + " pts)"
                    : "";
            }
            m_Footer.gameObject.SetActive(!string.IsNullOrEmpty(m_Footer.text));

            RebuildRows(rows, mvp, summary);

            if (m_Animation != null) StopCoroutine(m_Animation);
            m_Animation = StartCoroutine(Fade(true));
        }

        public void Hide()
        {
            if (m_Canvas == null || !m_Canvas.enabled) return;
            if (m_Animation != null) StopCoroutine(m_Animation);
            m_Animation = StartCoroutine(Fade(false));
        }

        private void HideImmediate()
        {
            m_Group.alpha = 0f;
            m_Canvas.enabled = false;
        }

        private IEnumerator Fade(bool show)
        {
            m_Canvas.enabled = true;
            float from = m_Group.alpha;
            float to = show ? 1f : 0f;
            float time = 0f;

            while (time < m_FadeDuration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / m_FadeDuration);
                m_Group.alpha = Mathf.Lerp(from, to, t);

                // Deslizamiento suave desde abajo al aparecer.
                float offset = show ? Mathf.Lerp(-30f, 0f, t) : 0f;
                m_Panel.anchoredPosition = new Vector2(0f, m_BottomMargin + offset);
                yield return null;
            }

            m_Group.alpha = to;
            m_Panel.anchoredPosition = new Vector2(0f, m_BottomMargin);
            if (!show) m_Canvas.enabled = false;
            m_Animation = null;
        }

        // ------------------------------------------------------------------ Tabla

        private void RebuildRows(List<PlayerMatchStats> rows, PlayerMatchStats mvp, MatchSummary summary)
        {
            for (int i = 0; i < m_RowObjects.Count; i++)
            {
                m_RowObjects[i].SetActive(false); // sale del layout en este mismo frame
                Destroy(m_RowObjects[i]);
            }
            m_RowObjects.Clear();

            for (int i = 0; i < rows.Count; i++)
            {
                PlayerMatchStats s = rows[i];
                bool isMvp = mvp != null && s.Tank == mvp.Tank;

                string[] values =
                {
                    (isMvp ? "<color=#FFD166>MVP</color>  " : "") + s.DisplayName,
                    s.ShotsFired.ToString(),
                    s.ShotsHit.ToString(),
                    s.Accuracy.ToString("0") + "%",
                    s.DamageDealt.ToString("0"),
                    s.PowerUpsCollected.ToString(),
                    s.Kills.ToString(),
                    summary.ScoreOf(s).ToString("0")
                };

                Color background = isMvp ? m_MvpRowColor : (i % 2 == 0 ? m_RowColorA : m_RowColorB);
                GameObject row = CreateRow("Row_J" + s.PlayerNumber, values, 23f, m_RowTextColor, background, s.PlayerColor, isMvp);
                m_RowObjects.Add(row);
            }
        }

        // ------------------------------------------------------------------ Construcción de la UI

        private void BuildLayout()
        {
            // Canvas propio en pantalla completa.
            GameObject canvasGO = new GameObject("MatchStatsCanvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            m_Canvas = canvasGO.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = m_SortingOrder;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel anclado abajo al centro; su alto se ajusta al contenido.
            GameObject panelGO = new GameObject("Panel", typeof(RectTransform));
            panelGO.transform.SetParent(canvasGO.transform, false);
            m_Panel = panelGO.GetComponent<RectTransform>();
            m_Panel.anchorMin = new Vector2(0.5f, 0f);
            m_Panel.anchorMax = new Vector2(0.5f, 0f);
            m_Panel.pivot = new Vector2(0.5f, 0f);
            m_Panel.sizeDelta = new Vector2(m_PanelWidth, 0f);
            m_Panel.anchoredPosition = new Vector2(0f, m_BottomMargin);
            m_Panel.localScale = Vector3.one * m_PanelScale;

            Image background = panelGO.AddComponent<Image>();
            background.color = m_BackgroundColor;
            background.raycastTarget = false;

            m_Group = panelGO.AddComponent<CanvasGroup>();
            m_Group.interactable = false;
            m_Group.blocksRaycasts = false;

            VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 0, 12);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Franja de color superior.
            GameObject accent = new GameObject("Accent", typeof(RectTransform));
            accent.transform.SetParent(panelGO.transform, false);
            Image accentImage = accent.AddComponent<Image>();
            accentImage.color = m_AccentColor;
            accentImage.raycastTarget = false;
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.preferredHeight = 5f;
            accentLayout.minHeight = 5f;

            m_Title = CreateText("Title", panelGO.transform, 32f, m_AccentColor, TextAlignmentOptions.Center, FontStyles.Bold);
            SetHeight(m_Title.gameObject, 44f);

            m_MvpBanner = CreateText("MvpBanner", panelGO.transform, 25f, m_AccentColor, TextAlignmentOptions.Center, FontStyles.Bold);
            SetHeight(m_MvpBanner.gameObject, 34f);

            // Encabezado de columnas.
            CreateRow("Header", k_Headers, 17f, m_HeaderTextColor, new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), false, panelGO.transform);

            // Contenedor de filas.
            GameObject rowsGO = new GameObject("Rows", typeof(RectTransform));
            rowsGO.transform.SetParent(panelGO.transform, false);
            m_RowsContainer = rowsGO.GetComponent<RectTransform>();
            VerticalLayoutGroup rowsLayout = rowsGO.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 3f;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            m_Footer = CreateText("Footer", panelGO.transform, 20f, m_HeaderTextColor, TextAlignmentOptions.Center, FontStyles.Italic);
            SetHeight(m_Footer.gameObject, 28f);
        }

        /// <summary>Crea una fila de la tabla con una celda por columna.</summary>
        private GameObject CreateRow(string name, string[] values, float fontSize, Color textColor,
                                     Color background, Color swatchColor, bool bold, Transform parent = null)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent != null ? parent : m_RowsContainer, false);

            Image image = row.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = false;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 12, 0, 0);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            SetHeight(row, fontSize + 14f);

            // Barrita con el color del jugador (transparente en el encabezado).
            GameObject swatch = new GameObject("Color", typeof(RectTransform));
            swatch.transform.SetParent(row.transform, false);
            Image swatchImage = swatch.AddComponent<Image>();
            swatchImage.color = swatchColor;
            swatchImage.raycastTarget = false;
            LayoutElement swatchLayout = swatch.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = 8f;
            swatchLayout.minWidth = 8f;

            for (int c = 0; c < values.Length && c < k_Widths.Length; c++)
            {
                TextAlignmentOptions align = c == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                TextMeshProUGUI cell = CreateText("Cell" + c, row.transform, fontSize, textColor, align,
                                                  bold ? FontStyles.Bold : FontStyles.Normal);
                cell.text = values[c];
                if (c == 0) cell.margin = new Vector4(10f, 0f, 0f, 0f);

                LayoutElement cellLayout = cell.gameObject.AddComponent<LayoutElement>();
                cellLayout.preferredWidth = k_Widths[c];
                cellLayout.flexibleWidth = c == 0 ? 1f : 0f;
            }

            return row;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float size, Color color,
                                           TextAlignmentOptions align, FontStyles style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) text.font = m_Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        private static void SetHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }
    }
}
