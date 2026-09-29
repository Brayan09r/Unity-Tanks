using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tanks.Complete
{
    // This handles both the start menu (selecting which tank each player uses) and the pause menu if present.
    // Enhanced with robust mouse clicking and direct keyboard selection:
    // - Player 1 selects tanks with keys 1, 2, 3, 4
    // - Player 2 selects tanks with keys 5, 6, 7, 8
    // - Space or Enter to start the battle
    public class GameUIHandler : MonoBehaviour
    {
        [System.Serializable]
        public struct MapOption
        {
            public string sceneName;
            public string displayName;
            public string icon;
            public string description;
        }

        public static readonly MapOption[] AvailableMaps = new MapOption[]
        {
            new MapOption { sceneName = "Demo_Game_Moon", displayName = "LUNA", icon = "🌙", description = "Superficie lunar con cráteres" },
            new MapOption { sceneName = "Demo_Game_Desert", displayName = "DESIERTO", icon = "🏜️", description = "Dunas de arena y ruinas" },
            new MapOption { sceneName = "Demo_Game_Jungle", displayName = "JUNGLA", icon = "🌴", description = "Vegetación y ruinas antiguas" }
        };

        [System.Serializable]
        public struct PendingPlayerData
        {
            public int slotIndex;
            public Color tankColor;
            public bool isComputer;
            public int controlIndex;
        }

        private static List<PendingPlayerData> s_PendingAutoStart;
        private static int s_SelectedMapIndex = -1;

        public GameManager m_GameManager;               // Reference to the GameManager in the scene

        [Header("Start Menu")] 
        public RectTransform m_StartMenuRoot;           // The GameObject root that is parent of the Start Menu
        public Button m_StartButton;                    // The Button that will start the game

        [Tooltip("The slot in the UI that can be taken by a player or computer tank")]
        public StartMenuSlot[] m_PlayerSlots;           // The Slots in the Start Menu that display the available tanks and handle players selections

        public OnScreenButton m_PauseMenuButton;        // Reference to OnScreenButton that emulate pressing a Gamepad Start button
        
        private Button m_MapSelectButton;               // Button to cycle through maps
        private TextMeshProUGUI m_MapSelectButtonText;  // Text inside the Map Selection Button
        private TextMeshProUGUI m_StartButtonText;      // Reference to the Text on the Start Button 
        private int m_SlotUsed = 0;                     // How many slots are currently used, the game needs at least 2 to start

        private PauseMenu m_PauseMenu;                  // Reference to the pause menu (if present in the scene)
        private InputAction m_PauseAction;              // The InputAction that will trigger the pause menu
        
        private CanvasScaler m_CanvasScaler;

        private void Awake()
        {
            Camera cam = GetComponentInParent<Camera>();
            m_CanvasScaler = GetComponentInParent<CanvasScaler>();

            if (Camera.main != null)
            {
                var data = Camera.main.GetUniversalAdditionalCameraData();
                if (data != null && cam != null && !data.cameraStack.Contains(cam))
                {
                    data.cameraStack.Add(cam);
                }
            }

            // Ensure InputSystemUIInputModule actions are fully enabled
            var uiModule = FindAnyObjectByType<InputSystemUIInputModule>();
            if (uiModule != null && uiModule.actionsAsset != null)
            {
                uiModule.actionsAsset.Enable();
            }
        }

        private void Start()
        {
            if (MobileUIControl.Instance != null)
                MobileUIControl.Instance.Hide();

            // Check if we arrived here to automatically start a match on this selected map
            if (s_PendingAutoStart != null && s_PendingAutoStart.Count >= 2)
            {
                var autostartData = s_PendingAutoStart;
                s_PendingAutoStart = null;

                m_StartMenuRoot.gameObject.SetActive(false);

                var prefabs = new[]
                {
                    m_GameManager.m_Tank1Prefab, m_GameManager.m_Tank2Prefab,
                    m_GameManager.m_Tank3Prefab, m_GameManager.m_Tank4Prefab
                };

                List<GameManager.PlayerData> pList = new List<GameManager.PlayerData>();
                foreach (var p in autostartData)
                {
                    pList.Add(new GameManager.PlayerData()
                    {
                        TankColor = p.tankColor,
                        IsComputer = p.isComputer,
                        ControlIndex = p.controlIndex,
                        UsedPrefab = (prefabs.Length > p.slotIndex && prefabs[p.slotIndex] != null) ? prefabs[p.slotIndex] : prefabs[0]
                    });
                }

                m_GameManager.StartGame(pList.ToArray());

                foreach (var slot in m_PlayerSlots)
                {
                    if (slot != null && slot.TankPreview != null)
                        Destroy(slot.TankPreview);
                }

                if (MobileUIControl.Instance != null)
                    MobileUIControl.Instance.Show();

                // Setup pause menu for auto-started game
                m_PauseMenu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                if (m_PauseMenu != null)
                {
                    m_PauseMenu.Init();
                    if (InputSystem.actions != null)
                    {
                        var pauseAct = InputSystem.actions.FindAction("Pause");
                        if (pauseAct != null)
                            m_PauseAction = pauseAct.Clone();
                    }
                    if (m_PauseAction != null)
                    {
                        m_PauseAction.performed += evt => { TogglePause(); };
                        m_PauseAction.Enable();
                    }
                    if (m_PauseMenuButton != null)
                    {
                        var rectTransform = m_PauseMenuButton.GetComponent<RectTransform>();
                        if (rectTransform != null) rectTransform.SetAsLastSibling();
                        m_PauseMenuButton.gameObject.SetActive(true);
                    }
                }
                return;
            }

            // Detect current scene and initialize map selection
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            s_SelectedMapIndex = 0;
            for (int i = 0; i < AvailableMaps.Length; ++i)
            {
                if (AvailableMaps[i].sceneName == activeScene)
                {
                    s_SelectedMapIndex = i;
                    break;
                }
            }

            // Setup the Start button
            m_StartButton.onClick.AddListener(StartGame);
            m_StartButton.interactable = false;
            m_StartButtonText = m_StartButton.GetComponentInChildren<TextMeshProUGUI>();

            // Create and initialize the Map Selection Button directly above Start Button
            CreateMapSelectButton();
            UpdateMapButtonVisuals();
            UpdateStartButtonState();
            
            // Disable the on-screen pause button initially
            if (m_PauseMenuButton != null)
                m_PauseMenuButton.gameObject.SetActive(false);

            // Pause Menu setup
            m_PauseMenu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (m_PauseMenu != null)
            {
                m_PauseMenu.Init();
                if (InputSystem.actions != null)
                {
                    var pauseAct = InputSystem.actions.FindAction("Pause");
                    if (pauseAct != null)
                        m_PauseAction = pauseAct.Clone();
                }
                if (m_PauseMenuButton != null)
                {
                    var rectTransform = m_PauseMenuButton.GetComponent<RectTransform>();
                    if (rectTransform != null) rectTransform.SetAsLastSibling();
                }
            }

            var tanksPrefabs = new[]
            {
                m_GameManager.m_Tank1Prefab, m_GameManager.m_Tank2Prefab, m_GameManager.m_Tank3Prefab,
                m_GameManager.m_Tank4Prefab
            };

            // Initialize all 4 player slots
            for (int i = 0; i < m_PlayerSlots.Length; ++i)
            {
                var slot = m_PlayerSlots[i];
                slot.SetTankPreview(tanksPrefabs.Length > i ? tanksPrefabs[i] : tanksPrefabs[0]);

                // Update button text to display helpful shortcut hints
                var addBtnText = slot.m_AddControlButton.GetComponentInChildren<TextMeshProUGUI>();
                if (addBtnText != null)
                {
                    addBtnText.text = $"Elegir Tanque {i + 1}\n<size=70%>[P1: Tecla {i + 1}]  [P2: Tecla {i + 5}]</size>";
                }

                var i1 = i;
                slot.m_AddControlButton.onClick.AddListener(() =>
                {
                    slot.AddTank();
                    m_SlotUsed += 1;

                    // Check if player 1 is already assigned
                    bool player1Present = false;
                    for (int j = 0; j < m_PlayerSlots.Length; ++j)
                    {
                        if (i1 == j) continue;
                        if (m_PlayerSlots[j].PlayerControlling == 1)
                            player1Present = true;
                    }

                    if (!player1Present)
                        slot.SetPlayerControlling(1);
                    else
                        slot.SetPlayerControlling(-1); // Default to CPU

                    UpdateStartButtonState();
                });

                slot.m_OffControlButton.onClick.AddListener(() =>
                {
                    slot.RemoveTank();
                    m_SlotUsed -= 1;
                    UpdateStartButtonState();
                });

                slot.m_P1ControlButton.onClick.AddListener(() =>
                {
                    slot.SetPlayerControlling(1);
                    for (int j = 0; j < m_PlayerSlots.Length; ++j)
                    {
                        var localSlot = m_PlayerSlots[j];
                        if (localSlot.IsOpen || localSlot == slot) continue;
                        if (localSlot.PlayerControlling == 1)
                            localSlot.SetPlayerControlling(-1);
                    }
                    UpdateStartButtonState();
                });

                slot.m_P2ControlButton.onClick.AddListener(() =>
                {
                    slot.SetPlayerControlling(2);
                    for (int j = 0; j < m_PlayerSlots.Length; ++j)
                    {
                        var localSlot = m_PlayerSlots[j];
                        if (localSlot.IsOpen || localSlot == slot) continue;
                        if (localSlot.PlayerControlling == 2)
                            localSlot.SetPlayerControlling(-1);
                    }
                    UpdateStartButtonState();
                });

                slot.m_ComputerControlButton.onClick.AddListener(() =>
                {
                    slot.SetPlayerControlling(-1);
                    UpdateStartButtonState();
                });
            }
        }

        public void AssignTankToPlayer(int slotIndex, int playerNumber)
        {
            if (slotIndex < 0 || slotIndex >= m_PlayerSlots.Length) return;
            var slot = m_PlayerSlots[slotIndex];

            if (slot.IsOpen)
            {
                slot.AddTank();
                m_SlotUsed += 1;
            }

            slot.SetPlayerControlling(playerNumber);

            // Ensure only one tank is controlled by this playerNumber
            for (int j = 0; j < m_PlayerSlots.Length; ++j)
            {
                var other = m_PlayerSlots[j];
                if (other == slot || other.IsOpen) continue;
                if (other.PlayerControlling == playerNumber)
                {
                    other.SetPlayerControlling(-1); // Switch previous slot to CPU
                }
            }

            UpdateStartButtonState();
        }

        private void CreateMapSelectButton()
        {
            if (m_StartButton == null || m_StartMenuRoot == null) return;

            GameObject mapBtnObj = Instantiate(m_StartButton.gameObject, m_StartButton.transform.parent);
            mapBtnObj.name = "MapSelectButton";
            mapBtnObj.SetActive(true);

            RectTransform mapRect = mapBtnObj.GetComponent<RectTransform>();
            RectTransform startRect = m_StartButton.GetComponent<RectTransform>();

            mapRect.anchorMin = startRect.anchorMin;
            mapRect.anchorMax = startRect.anchorMax;
            mapRect.pivot = startRect.pivot;
            mapRect.sizeDelta = new Vector2(startRect.sizeDelta.x, 56f);

            // Position right above Start Button
            float yOffset = (startRect.sizeDelta.y * 0.5f) + 40f;
            mapRect.anchoredPosition = new Vector2(startRect.anchoredPosition.x, startRect.anchoredPosition.y + yOffset);

            m_MapSelectButton = mapBtnObj.GetComponent<Button>();
            m_MapSelectButton.onClick.RemoveAllListeners();
            m_MapSelectButton.interactable = true;
            m_MapSelectButton.onClick.AddListener(() => CycleMap(1));

            // Set a distinct metallic cyan accent color for the map selector
            Image btnImg = mapBtnObj.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.18f, 0.28f, 0.45f, 0.95f);
            }

            m_MapSelectButtonText = mapBtnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (m_MapSelectButtonText != null)
            {
                m_MapSelectButtonText.enableAutoSizing = true;
                m_MapSelectButtonText.fontSizeMin = 13f;
                m_MapSelectButtonText.fontSizeMax = 20f;
            }
        }

        public void CycleMap(int direction = 1)
        {
            s_SelectedMapIndex = (s_SelectedMapIndex + direction + AvailableMaps.Length) % AvailableMaps.Length;
            UpdateMapButtonVisuals();
        }

        private void UpdateMapButtonVisuals()
        {
            if (s_SelectedMapIndex < 0 || s_SelectedMapIndex >= AvailableMaps.Length)
                s_SelectedMapIndex = 0;

            var curMap = AvailableMaps[s_SelectedMapIndex];

            if (m_MapSelectButtonText != null)
            {
                m_MapSelectButtonText.text = $"◀   MAPA: {curMap.icon} <b>{curMap.displayName}</b>   ▶\n<size=68%><color=#A8DADC>Click o [M] para cambiar  •  {curMap.description}</color></size>";
            }

            UpdateStartButtonState();
        }

        private void UpdateStartButtonState()
        {
            if (m_StartButtonText == null) return;

            var curMap = (s_SelectedMapIndex >= 0 && s_SelectedMapIndex < AvailableMaps.Length)
                ? AvailableMaps[s_SelectedMapIndex]
                : AvailableMaps[0];

            if (m_SlotUsed >= 2)
            {
                m_StartButtonText.text = $"¡INICIAR EN {curMap.displayName}! [ENTER / ESPACIO]\n<size=62%><color=#FFD166>{curMap.icon} {curMap.description}</color></size>";
                m_StartButton.interactable = true;
            }
            else
            {
                m_StartButtonText.text = $"Mínimo 2 Tanques (Pulsa 1..8 o Click)\n<size=62%><color=#A8DADC>Mapa seleccionado: {curMap.icon} {curMap.displayName}</color></size>";
                m_StartButton.interactable = false;
            }
        }

        public void StartGame()
        {
            if (m_SlotUsed < 2) return;

            var targetMap = (s_SelectedMapIndex >= 0 && s_SelectedMapIndex < AvailableMaps.Length)
                ? AvailableMaps[s_SelectedMapIndex]
                : AvailableMaps[0];

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // If the user selected a map different from the active scene, transition smoothly
            if (targetMap.sceneName != currentScene)
            {
                List<PendingPlayerData> pending = new List<PendingPlayerData>();
                for (int i = 0; i < m_PlayerSlots.Length; ++i)
                {
                    var slot = m_PlayerSlots[i];
                    if (!slot.IsOpen)
                    {
                        pending.Add(new PendingPlayerData()
                        {
                            slotIndex = i,
                            tankColor = slot.m_SlotColor,
                            isComputer = slot.IsComputer,
                            controlIndex = slot.PlayerControlling
                        });
                    }
                }
                s_PendingAutoStart = pending;
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetMap.sceneName);
                return;
            }

            // Normal start in the active scene
            m_StartMenuRoot.gameObject.SetActive(false);

            List<GameManager.PlayerData> playerData = new List<GameManager.PlayerData>();
            foreach (var slot in m_PlayerSlots)
            {
                if (!slot.IsOpen)
                {
                    playerData.Add(new GameManager.PlayerData()
                    {
                        TankColor = slot.m_SlotColor,
                        IsComputer = slot.IsComputer,
                        ControlIndex = slot.PlayerControlling,
                        UsedPrefab = slot.TankPrefab,
                    });
                }
            }

            m_GameManager.StartGame(playerData.ToArray());

            foreach (var slot in m_PlayerSlots)
            {
                if (slot.TankPreview != null)
                    Destroy(slot.TankPreview);
            }

            if (MobileUIControl.Instance != null)
                MobileUIControl.Instance.Show();

            if (m_PauseMenu != null && m_PauseAction != null)
            {
                m_PauseAction.performed += evt => { TogglePause(); };
                m_PauseAction.Enable();
                if (m_PauseMenuButton != null)
                    m_PauseMenuButton.gameObject.SetActive(true);
            }
        }

        private void TogglePause()
        {
            if (m_PauseMenu != null)
                m_PauseMenu.TogglePause();
        }

        private void Update()
        {
            if (m_CanvasScaler != null)
            {
                float ratio = Screen.width / (float)Screen.height;
                m_CanvasScaler.matchWidthOrHeight = ratio > 1.0f ? 1.0f : 0.0f;
            }

            // Keyboard and Direct Mouse Handling only while Start Menu is open
            if (m_StartMenuRoot != null && m_StartMenuRoot.gameObject.activeInHierarchy)
            {
                HandleKeyboardMenuSelection();
                HandleDirectMouseClick();
            }
        }

        private void HandleKeyboardMenuSelection()
        {
            // Map cycling shortcuts: M key, or Left / Right Arrows
            if (IsKeyPressed(Key.M, KeyCode.M))
            {
                CycleMap(1);
            }
            else if (IsKeyPressed(Key.LeftArrow, KeyCode.LeftArrow))
            {
                CycleMap(-1);
            }
            else if (IsKeyPressed(Key.RightArrow, KeyCode.RightArrow))
            {
                CycleMap(1);
            }

            // Player 1 tank selection: Keys 1 to 4
            if (IsKeyPressed(Key.Digit1, KeyCode.Alpha1, Key.Numpad1, KeyCode.Keypad1)) AssignTankToPlayer(0, 1);
            else if (IsKeyPressed(Key.Digit2, KeyCode.Alpha2, Key.Numpad2, KeyCode.Keypad2)) AssignTankToPlayer(1, 1);
            else if (IsKeyPressed(Key.Digit3, KeyCode.Alpha3, Key.Numpad3, KeyCode.Keypad3)) AssignTankToPlayer(2, 1);
            else if (IsKeyPressed(Key.Digit4, KeyCode.Alpha4, Key.Numpad4, KeyCode.Keypad4)) AssignTankToPlayer(3, 1);

            // Player 2 tank selection: Keys 5 to 8
            else if (IsKeyPressed(Key.Digit5, KeyCode.Alpha5, Key.Numpad5, KeyCode.Keypad5)) AssignTankToPlayer(0, 2);
            else if (IsKeyPressed(Key.Digit6, KeyCode.Alpha6, Key.Numpad6, KeyCode.Keypad6)) AssignTankToPlayer(1, 2);
            else if (IsKeyPressed(Key.Digit7, KeyCode.Alpha7, Key.Numpad7, KeyCode.Keypad7)) AssignTankToPlayer(2, 2);
            else if (IsKeyPressed(Key.Digit8, KeyCode.Alpha8, Key.Numpad8, KeyCode.Keypad8)) AssignTankToPlayer(3, 2);

            // Start battle with Space, Enter or NumpadEnter
            if (m_SlotUsed >= 2)
            {
                if (IsKeyPressed(Key.Enter, KeyCode.Return, Key.NumpadEnter, KeyCode.KeypadEnter) ||
                    IsKeyPressed(Key.Space, KeyCode.Space))
                {
                    StartGame();
                }
            }
        }

        private bool IsKeyPressed(Key inputKey, KeyCode legacyKey, Key? numpadKey = null, KeyCode? legacyNumpadKey = null)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current[inputKey].wasPressedThisFrame) return true;
                if (numpadKey.HasValue && Keyboard.current[numpadKey.Value].wasPressedThisFrame) return true;
            }
            if (Input.GetKeyDown(legacyKey)) return true;
            if (legacyNumpadKey.HasValue && Input.GetKeyDown(legacyNumpadKey.Value)) return true;
            return false;
        }

        private void HandleDirectMouseClick()
        {
            bool clicked = false;
            Vector2 mousePos = Vector2.zero;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                clicked = true;
                mousePos = Mouse.current.position.ReadValue();
            }
            else if (Input.GetMouseButtonDown(0))
            {
                clicked = true;
                mousePos = Input.mousePosition;
            }

            if (!clicked) return;

            var canvas = GetComponentInParent<Canvas>();
            Camera eventCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : GetComponentInParent<Camera>();

            // Check Map button click
            if (m_MapSelectButton != null && m_MapSelectButton.gameObject.activeInHierarchy)
            {
                var mapRect = m_MapSelectButton.GetComponent<RectTransform>();
                if (mapRect != null && RectTransformUtility.RectangleContainsScreenPoint(mapRect, mousePos, eventCam))
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, mousePos, eventCam, out Vector2 localPoint);
                    if (localPoint.x < -mapRect.rect.width * 0.25f)
                        CycleMap(-1);
                    else
                        CycleMap(1);
                    return;
                }
            }

            // Check Start button
            if (m_StartButton != null && m_SlotUsed >= 2)
            {
                var startRect = m_StartButton.GetComponent<RectTransform>();
                if (startRect != null && RectTransformUtility.RectangleContainsScreenPoint(startRect, mousePos, eventCam))
                {
                    StartGame();
                    return;
                }
            }

            // Check each tank slot
            for (int i = 0; i < m_PlayerSlots.Length; ++i)
            {
                var slot = m_PlayerSlots[i];
                if (slot == null) continue;
                var slotRect = slot.GetComponent<RectTransform>();
                if (slotRect == null) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(slotRect, mousePos, eventCam))
                {
                    if (slot.IsOpen)
                    {
                        slot.m_AddControlButton.onClick.Invoke();
                    }
                    else
                    {
                        // Check sub-buttons if clicked directly
                        if (slot.m_P1ControlButton != null && RectTransformUtility.RectangleContainsScreenPoint(slot.m_P1ControlButton.GetComponent<RectTransform>(), mousePos, eventCam))
                        {
                            slot.m_P1ControlButton.onClick.Invoke();
                        }
                        else if (slot.m_P2ControlButton != null && RectTransformUtility.RectangleContainsScreenPoint(slot.m_P2ControlButton.GetComponent<RectTransform>(), mousePos, eventCam))
                        {
                            slot.m_P2ControlButton.onClick.Invoke();
                        }
                        else if (slot.m_ComputerControlButton != null && RectTransformUtility.RectangleContainsScreenPoint(slot.m_ComputerControlButton.GetComponent<RectTransform>(), mousePos, eventCam))
                        {
                            slot.m_ComputerControlButton.onClick.Invoke();
                        }
                        else if (slot.m_OffControlButton != null && RectTransformUtility.RectangleContainsScreenPoint(slot.m_OffControlButton.GetComponent<RectTransform>(), mousePos, eventCam))
                        {
                            slot.m_OffControlButton.onClick.Invoke();
                        }
                        else
                        {
                            // Clicking on the slot card cycles through: P1 -> P2 -> CPU -> P1
                            if (slot.PlayerControlling == 1)
                                slot.m_P2ControlButton.onClick.Invoke();
                            else if (slot.PlayerControlling == 2)
                                slot.m_ComputerControlButton.onClick.Invoke();
                            else
                                slot.m_P1ControlButton.onClick.Invoke();
                        }
                    }
                    return;
                }
            }
        }
    }
}