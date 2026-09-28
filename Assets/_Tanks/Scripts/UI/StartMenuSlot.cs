using TMPro;
using UnityEngine;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Tanks.Complete
{
    // Class handling on "slot" in the main menu, which is an entry that show a tank preview, display its stat and allow
    // to add that tank to the game or not and change who control it (p1, p2 or computer)
    public class StartMenuSlot : MonoBehaviour
    {
        public Color m_SlotColor;                       // The color the tank in that slot will take
        
        [Header("References")]
        public RectTransform m_TankPreviewPosition;     // The Transform on which to place the Tank preview so it display at the right place on screen
        public TextMeshProUGUI m_TankStats;             // The Text to use to display the tank stats
        public Button m_AddControlButton;               // The button on which when clicked the tank get added to the current game

        public RectTransform m_ControlChoiceRoot;       // The root of which all the control choice buttons are parented to
        public Button m_P1ControlButton;                // the button that make this tank controlled by p1
        public Button m_P2ControlButton;                // the button that make this tank controlled by p2
        public Button m_ComputerControlButton;          // the button that make this tank controlled by the computer
        public Button m_OffControlButton;               // the button that remove that tank from the currently used tanks

        public Image BackgroundImage;                   // The Image that is the background of the whole slot
        public Sprite OpenSlotBackground;               // The sprite to use when the slot is open (still not used by anyone)
        public Sprite UsedSlotBackground;               // The sprite to use when the slot is used (controlled by p1/p2 or computer)

        public GameObject TankPreview { get; set; }         // The preview instance that show this tank rotating in the menu
        public GameObject TankPrefab { get; private set; }  // The prefab this slot is based on
        public int PlayerControlling { get; set; }          // Which player control this, 1 or 2 (will be -1 for computer)
        public bool IsOpen { get; set; }                    // Is the slot open (not join the game yet) or not (already assigned to p1/p2 or computer)
        public bool IsComputer { get; set; }                // Is the slot used by a computer controlled tank or a player controlled one
        
        private Camera m_MenuCamera;                        // The Camera used to display the menu

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            m_MenuCamera = GetComponentInParent<Camera>();
            IsOpen = true;

            if (m_P2ControlButton != null)
            {
                m_P2ControlButton.gameObject.SetActive(true);
            }

            BackgroundImage.sprite = OpenSlotBackground;
        }
        
        private void Update()
        {
            // If we have a preview slowly rotate it
            if (TankPreview != null)
            {
                TankPreview.transform.Rotate(Vector3.up, 45.0f * Time.deltaTime);
            }
        }

        public void AddTank()
        {
            m_AddControlButton.gameObject.SetActive(false);
            m_ControlChoiceRoot.gameObject.SetActive(true);

            IsOpen = false;
            BackgroundImage.sprite = UsedSlotBackground;
        }

        public void RemoveTank()
        {
            m_AddControlButton.gameObject.SetActive(true);
            m_ControlChoiceRoot.gameObject.SetActive(false);

            SetPlayerControlling(-1);

            IsOpen = true;
            BackgroundImage.sprite = OpenSlotBackground;
        }

        public void SetPlayerControlling(int playerNumber)
        {
            //re-enable the button for the current controller as we now can re-select it again
            if (PlayerControlling == 1)
                m_P1ControlButton.interactable = true;
            else if (PlayerControlling == 2)
                m_P2ControlButton.interactable = true;
            else if (PlayerControlling == -1)
                m_ComputerControlButton.interactable = true;
            
            // change the controller 
            PlayerControlling = playerNumber;
            
            // then disable the associated button and set if its a computer or not 
            switch(playerNumber)
            {
                case 1:
                    m_P1ControlButton.interactable = false;
                    IsComputer = false;
                    break;
                case 2:
                    m_P2ControlButton.interactable = false;
                    IsComputer = false;
                    break;
                case -1:
                    m_ComputerControlButton.interactable = false;
                    IsComputer = true;
                    break;
            }
        }

        public void SetTankPreview(GameObject prefab)
        {
            // If we already have a tank preview, destroy it
            if (TankPreview != null)
            {
                Destroy(TankPreview);
            }

            //assign the right prefab
            TankPrefab = prefab;
            TankPreview = Instantiate(prefab);
            
            // Disable gameplay scripts on preview
            var move = TankPreview.GetComponent<TankMovement>();
            if (move != null) move.enabled = false;
            var shoot = TankPreview.GetComponent<TankShooting>();
            if (shoot != null) shoot.enabled = false;
            var health = TankPreview.GetComponent<TankHealth>();
            var inputUser = TankPreview.GetComponent<TankInputUser>();
            if (inputUser != null) Destroy(inputUser);
            var rb = TankPreview.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // update the tank stats text with this tank stats
            float speed = move != null ? move.m_Speed : 10f;
            float damage = shoot != null ? shoot.m_MaxDamage : 50f;
            float hp = health != null ? health.m_StartingHealth : 150f;
            m_TankStats.text = $"Velocidad: {speed}\nDaño: {damage}\nSalud: {hp}";
            
            // move it to the right preview position so it appears in the right spot on screen
            var position = m_MenuCamera.WorldToScreenPoint(m_TankPreviewPosition.position);
            TankPreview.transform.position =
                m_MenuCamera.ScreenToWorldPoint(position) + Vector3.back * 3.0f;
            
            // go through all renderers of that tank and ensure valid URP materials
            MeshRenderer[] renderers = TankPreview.GetComponentsInChildren<MeshRenderer>();
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var mats = renderer.materials;
                for (int j = 0; j < mats.Length; ++j)
                {
                    var mat = mats[j];
                    if (mat == null) continue;

                    // Fix any magenta/pink error shader
                    if (mat.shader == null || mat.shader.name.Contains("Error") || mat.shader.name.Contains("InternalErrorShader"))
                    {
                        if (urpShader != null) mat.shader = urpShader;
                    }

                    if (mat.name.Contains("TankColor"))
                    {
                        mat.color = m_SlotColor;
                    }
                    else if (mat.name.Contains("TankGrey"))
                    {
                        mat.color = new Color(0.28f, 0.28f, 0.28f, 1f);
                    }
                    else if (mat.name.Contains("TankLights"))
                    {
                        mat.color = new Color(0.7f, 0.85f, 0.95f, 1f);
                    }
                }
                renderer.materials = mats;
            }
            
            // Disable all audio
            var audioSource = TankPreview.GetComponentsInChildren<AudioSource>();
            foreach (var source in audioSource)
            {
                Destroy(source);
            }
        }
    }
}