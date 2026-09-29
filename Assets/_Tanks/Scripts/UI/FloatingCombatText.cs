using TMPro;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Un número de daño flotante en el mundo 3D (TextMeshPro, no UI).
    /// Animación: "pop" de escala al aparecer, sube con desaceleración, se desvanece y se recicla.
    /// Lo crea y recicla FloatingTextSpawner (pool de objetos), no hace falta prefab.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingCombatText : MonoBehaviour
    {
        private TextMeshPro m_Text;
        private FloatingTextSpawner m_Owner;

        private Vector3 m_StartPosition;
        private Vector3 m_Drift;
        private Color m_Color;
        private float m_Age;
        private float m_Duration;
        private float m_RiseHeight;
        private float m_BaseScale;

        public TextMeshPro Text
        {
            get
            {
                if (m_Text == null) m_Text = GetComponent<TextMeshPro>();
                return m_Text;
            }
        }

        public void Play(FloatingTextSpawner owner, Vector3 worldPosition, string content, Color color,
                         float fontSize, float duration, float riseHeight, float scaleMultiplier)
        {
            m_Owner = owner;
            m_StartPosition = worldPosition;
            m_Color = color;
            m_Duration = Mathf.Max(0.1f, duration);
            m_RiseHeight = riseHeight;
            m_BaseScale = scaleMultiplier;
            m_Age = 0f;

            // Pequeña deriva lateral aleatoria para que varios números no se encimen.
            m_Drift = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.3f, 0.3f));

            TextMeshPro text = Text;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;

            transform.position = worldPosition;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
            FaceCamera();
        }

        private void LateUpdate()
        {
            m_Age += Time.deltaTime;
            float t = Mathf.Clamp01(m_Age / m_Duration);

            // Subida con desaceleración (ease-out).
            float rise = 1f - (1f - t) * (1f - t);
            transform.position = m_StartPosition + Vector3.up * (m_RiseHeight * rise) + m_Drift * rise;

            // "Pop": crece rápido hasta 1.35x y se asienta en 1x durante el primer 20%.
            float scale;
            if (t < 0.12f) scale = Mathf.Lerp(0f, 1.35f, t / 0.12f);
            else if (t < 0.25f) scale = Mathf.Lerp(1.35f, 1f, (t - 0.12f) / 0.13f);
            else scale = 1f;
            transform.localScale = Vector3.one * (scale * m_BaseScale);

            // Desvanecimiento en la segunda mitad de la animación.
            Color c = m_Color;
            c.a = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
            Text.color = c;

            FaceCamera();

            if (t >= 1f)
            {
                if (m_Owner != null) m_Owner.Release(this);
                else Destroy(gameObject);
            }
        }

        /// <summary>Billboard: el texto siempre mira a la cámara.</summary>
        private void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
        }
    }
}
