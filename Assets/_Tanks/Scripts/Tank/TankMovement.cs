using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Tanks.Complete
{
    //Ensure it run before the TankShooting component as TankShooting grabs the InputUser from this when there are no
    //GameManager set (used during learning experience to test tank in empty scenes)
    [DefaultExecutionOrder(-10)]
    public class TankMovement : MonoBehaviour
    {
        [Tooltip("The player number. Without a tank selection menu, Player 1 is left keyboard control, Player 2 is right keyboard")]
        public int m_PlayerNumber = 1;              // Used to identify which tank belongs to which player.  This is set by this tank's manager.
        [Tooltip("The speed in unity unit/second the tank move at")]
        public float m_Speed = 12f;                 // How fast the tank moves forward and back.
        [Tooltip("The speed in deg/s that tank will rotate at")]
        public float m_TurnSpeed = 180f;            // How fast the tank turns in degrees per second.
        [Tooltip("If set to true, the tank auto orient and move toward the pressed direction instead of rotating on left/right and move forward on up")]
        public bool m_IsDirectControl;
        public AudioSource m_MovementAudio;         // Reference to the audio source used to play engine sounds. NB: different to the shooting audio source.
        public AudioClip m_EngineIdling;            // Audio to play when the tank isn't moving.
        public AudioClip m_EngineDriving;           // Audio to play when the tank is moving.
		public float m_PitchRange = 0.2f;           // The amount by which the pitch of the engine noises can vary.
        [Tooltip("Is set to true this will be controlled by the computer and not a player")]
        public bool m_IsComputerControlled = false; // Is this tank player or computer controlled
        [HideInInspector]
        public TankInputUser m_InputUser;            // The Input User component for that tanks. Contains the Input Actions.
        
        public Rigidbody Rigidbody => m_Rigidbody;
        
        public int ControlIndex { get; set; } = -1; //this define the index of the control 1 = left keyboard or pad, 2 = right keyboard, -1 = no control
        
        private string m_MovementAxisName;          // The name of the input axis for moving forward and back.
        private string m_TurnAxisName;              // The name of the input axis for turning.
        private Rigidbody m_Rigidbody;              // Reference used to move the tank.
        private float m_MovementInputValue;         // The current value of the movement input.
        private float m_TurnInputValue;             // The current value of the turn input.
        private Vector3 m_ExplosionForceValue;      // The current value of the force  applied on the tank from an explosion.
        private float m_OriginalPitch;              // The pitch of the audio source at the start of the scene.
        private ParticleSystem[] m_particleSystems; // References to all the particles systems used by the Tanks
        
        private InputAction m_MoveAction;             // The InputAction used to move, retrieved from TankInputUser
        private InputAction m_TurnAction;             // The InputAction used to shot, retrieved from TankInputUser

        private Vector3 m_RequestedDirection;       // In Direct Control mode, store the direction the user *wants* to go toward
        
        [Header("Cyber Nitro Boost")]
        public float m_BoostMultiplier = 1.75f;
        public float m_BoostDuration = 1.2f;
        public float m_BoostCooldown = 3.5f;
        private float m_BoostTimer = 0f;
        private float m_BoostCooldownTimer = 0f;
        private bool m_IsBoosting = false;

        public bool IsBoosting => m_IsBoosting;
        public float BoostCooldownRatio => Mathf.Clamp01(m_BoostCooldownTimer / m_BoostCooldown);
        
        private void Awake ()
        {
            m_Rigidbody = GetComponent<Rigidbody> ();
            
            m_InputUser = GetComponent<TankInputUser>();
            if (m_InputUser == null)
                m_InputUser = gameObject.AddComponent<TankInputUser>();
        }


        private void OnEnable ()
        {
            // Computer controlled tank are kinematic
            m_Rigidbody.isKinematic = false;

            // Also reset the input values and explosion force.
            m_MovementInputValue = 0f;
            m_TurnInputValue = 0f;
            m_ExplosionForceValue = Vector3.zero;
            // We grab all the Particle systems child of that Tank to be able to Stop/Play them on Deactivate/Activate
            // It is needed because we move the Tank when spawning it, and if the Particle System is playing while we do that
            // it "think" it move from (0,0,0) to the spawn point, creating a huge trail of smoke
            m_particleSystems = GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < m_particleSystems.Length; ++i)
            {
                m_particleSystems[i].Play();
            }
        }


        private void OnDisable ()
        {
            // When the tank is turned off, set it to kinematic so it stops moving.
            m_Rigidbody.isKinematic = true;

            // Stop all particle system so it "reset" it's position to the actual one instead of thinking we moved when spawning
            for(int i = 0; i < m_particleSystems.Length; ++i)
            {
                m_particleSystems[i].Stop();
            }
        }


        private void Start ()
        {
            // If this is computer controlled...
            if (m_IsComputerControlled)
            {
                // but it doesn't have an AI component...
                var ai = GetComponent<TankAI>();
                if (ai == null)
                {
                    // we add it, to ensure this will control the tank.
                    // This is only useful when user test tank in empty scene, otherwise the TankManager ensure 
                    // computer controlled tank are setup properly
                    gameObject.AddComponent<TankAI>();
                }
            }

            // If no control index was set, this mean this is a scene without a GameManager and that tank was manually
            // added to an empty scene, so we used the manually set Player Number in the Inspector as the ControlIndex,
            // so Player 1 will be ControlIndex 1 -> KeyboardLeft and Player 2 -> KeyboardRight
            if (ControlIndex == -1 && !m_IsComputerControlled)
            {
                ControlIndex = m_PlayerNumber;
            }
            
            // PC Keyboard controls: Player 1 -> KeyboardLeft, Player 2 -> KeyboardRight
            if (m_InputUser != null)
            {
                try
                {
                    m_InputUser.ActivateScheme(ControlIndex == 1 ? "KeyboardLeft" : "KeyboardRight");
                }
                catch { }
            }

            // The axes names are based on player number.
            m_MovementAxisName = "Vertical";
            m_TurnAxisName = "Horizontal";
            
            // Get the action input from the TankInputUser component if available
            if (m_InputUser != null && m_InputUser.ActionAsset != null)
            {
                try
                {
                    m_MoveAction = m_InputUser.ActionAsset.FindAction(m_MovementAxisName);
                    m_TurnAction = m_InputUser.ActionAsset.FindAction(m_TurnAxisName);
                    
                    if (m_MoveAction != null) m_MoveAction.Enable();
                    if (m_TurnAction != null) m_TurnAction.Enable();
                }
                catch { }
            }
            
            // Store the original pitch of the audio source.
            if(m_MovementAudio)
            {
                m_OriginalPitch = m_MovementAudio.pitch;
            }
        }


        private void Update ()
        {
            // Computer controlled tank will be moved by the TankAI component, so only read input for player controlled tanks
            if (!m_IsComputerControlled)
            {
                int effectivePlayer = ControlIndex > 0 ? ControlIndex : m_PlayerNumber;

                float move = 0f;
                float turn = 0f;

                // Player 1: WASD
                if (effectivePlayer == 1)
                {
                    if (Keyboard.current != null)
                    {
                        if (Keyboard.current.wKey.isPressed) move += 1f;
                        if (Keyboard.current.sKey.isPressed) move -= 1f;
                        if (Keyboard.current.aKey.isPressed) turn -= 1f;
                        if (Keyboard.current.dKey.isPressed) turn += 1f;
                    }
                    if (Input.GetKey(KeyCode.W)) move += 1f;
                    if (Input.GetKey(KeyCode.S)) move -= 1f;
                    if (Input.GetKey(KeyCode.A)) turn -= 1f;
                    if (Input.GetKey(KeyCode.D)) turn += 1f;
                }
                // Player 2: IJKL (and Arrow keys)
                else if (effectivePlayer == 2)
                {
                    if (Keyboard.current != null)
                    {
                        if (Keyboard.current.iKey.isPressed || Keyboard.current.upArrowKey.isPressed) move += 1f;
                        if (Keyboard.current.kKey.isPressed || Keyboard.current.downArrowKey.isPressed) move -= 1f;
                        if (Keyboard.current.jKey.isPressed || Keyboard.current.leftArrowKey.isPressed) turn -= 1f;
                        if (Keyboard.current.lKey.isPressed || Keyboard.current.rightArrowKey.isPressed) turn += 1f;
                    }
                    if (Input.GetKey(KeyCode.I) || Input.GetKey(KeyCode.UpArrow)) move += 1f;
                    if (Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.DownArrow)) move -= 1f;
                    if (Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.LeftArrow)) turn -= 1f;
                    if (Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.RightArrow)) turn += 1f;
                }

                // Gamepad support for Player 1
                if (effectivePlayer == 1 && Gamepad.current != null)
                {
                    Vector2 stick = Gamepad.current.leftStick.ReadValue();
                    if (stick.sqrMagnitude > 0.05f)
                    {
                        move = stick.y;
                        turn = stick.x;
                    }
                }

                // Fallback to InputAction if no keyboard keys are being pressed
                if (Mathf.Approximately(move, 0f) && m_MoveAction != null && m_MoveAction.enabled)
                {
                    move = m_MoveAction.ReadValue<float>();
                }
                if (Mathf.Approximately(turn, 0f) && m_TurnAction != null && m_TurnAction.enabled)
                {
                    turn = m_TurnAction.ReadValue<float>();
                }

                m_MovementInputValue = Mathf.Clamp(move, -1f, 1f);
                m_TurnInputValue = Mathf.Clamp(turn, -1f, 1f);

                // Nitro Boost Input check
                bool boostPressed = false;
                if (effectivePlayer == 1)
                {
                    if (Keyboard.current != null && (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                        boostPressed = true;
                    if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space))
                        boostPressed = true;
                    if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.rightTrigger.wasPressedThisFrame))
                        boostPressed = true;
                }
                else if (effectivePlayer == 2)
                {
                    if (Keyboard.current != null && (Keyboard.current.rightShiftKey.wasPressedThisFrame || Keyboard.current.uKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                        boostPressed = true;
                    if (Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        boostPressed = true;
                }

                if (boostPressed && m_BoostCooldownTimer <= 0f && !m_IsBoosting)
                {
                    m_IsBoosting = true;
                    m_BoostTimer = m_BoostDuration;
                    CameraControl.TriggerShake(0.2f, 0.2f);
                }
            }

            if (m_IsBoosting)
            {
                m_BoostTimer -= Time.deltaTime;
                if (m_BoostTimer <= 0f)
                {
                    m_IsBoosting = false;
                    m_BoostCooldownTimer = m_BoostCooldown;
                }
            }
            else if (m_BoostCooldownTimer > 0f)
            {
                m_BoostCooldownTimer -= Time.deltaTime;
            }
            
            if(m_MovementAudio)
            {
                EngineAudio ();
            }
        }


        private void EngineAudio ()
        {
            // If there is no input (the tank is stationary)...
            if (Mathf.Abs (m_MovementInputValue) < 0.1f && Mathf.Abs (m_TurnInputValue) < 0.1f)
            {
                // ... and if the audio source is currently playing the driving clip...
                if (m_MovementAudio.clip == m_EngineDriving)
                {
                    // ... change the clip to idling and play it.
                    m_MovementAudio.clip = m_EngineIdling;
                    m_MovementAudio.pitch = Random.Range (m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                    m_MovementAudio.Play ();
                }
            }
            else
            {
                // Otherwise if the tank is moving and if the idling clip is currently playing...
                if (m_MovementAudio.clip == m_EngineIdling)
                {
                    // ... change the clip to driving and play.
                    m_MovementAudio.clip = m_EngineDriving;
                    m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                    m_MovementAudio.Play();
                }
            }
        }


        private bool IsDirectControlActive()
        {
            if (m_IsDirectControl) return true;
            if (m_InputUser != null && m_InputUser.InputUser.valid && m_InputUser.InputUser.controlScheme.HasValue)
            {
                return m_InputUser.InputUser.controlScheme.Value.name == "Gamepad";
            }
            return false;
        }


        private void FixedUpdate ()
        {
            // If this is using a gamepad or have direct control enabled, this used a different movement method : instead of
            // "up" behind moving forward for the tank, it instead takes the gamepad move direction as the desired forward for the tank
            // and will compute the speed and rotation needed to move the tank toward that direction.
            if (IsDirectControlActive())
            {
                var camForward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                camForward.y = 0;

                // If camForward is zero, use camera up instead
                if (camForward.sqrMagnitude < 0.0001f && Camera.main != null)
                {
                    camForward = Camera.main.transform.up;
                    camForward.y = 0;
                }

                camForward.Normalize();
                var camRight = Vector3.Cross(Vector3.up, camForward);
                
                //this creates a vector based on camera look (e.g. pressing up mean we want to go up in the direction of the
                //camera, not forward in the direction of the tank)
                m_RequestedDirection = (camForward * m_MovementInputValue + camRight * m_TurnInputValue);
                m_RequestedDirection.Normalize();
            }
            
            // Adjust the rigidbodies position and orientation in FixedUpdate.
            Move ();
            Turn ();
        }


        private void Move ()
        {
            float speedInput = 0.0f;
            
            // In direct control mode, the speed will depend on how far from the desired direction we are
            if (IsDirectControlActive())
            {
                speedInput = m_RequestedDirection.magnitude;
                //if we are direct control, the speed of the move is based angle between current direction and the wanted
                //direction. If under 90, full speed, then speed reduced between 90 and 180
                speedInput *= 1.0f - Mathf.Clamp01((Vector3.Angle(m_RequestedDirection, transform.forward) - 90) / 90.0f);
            }
            else
            {
                // in normal "tank control" the speed value is how much we press "up/forward"
                speedInput = m_MovementInputValue;
            }
            
            // Create a vector in the direction the tank is facing with a magnitude based on the input, speed and the time between frames.
            float effectiveSpeed = m_Speed * (m_IsBoosting ? m_BoostMultiplier : 1.0f);
            Vector3 movement = transform.forward * speedInput * effectiveSpeed;

            // Apply this movement to the rigidbody's position.
            m_Rigidbody.linearVelocity = movement + m_ExplosionForceValue;
            m_ExplosionForceValue = Vector3.Lerp(m_ExplosionForceValue, Vector3.zero, Time.deltaTime * 3f); // 3f = braking speed
        }


        private void Turn ()
        {
            Quaternion turnRotation;
            // If in direct control...
            if (IsDirectControlActive())
            {
                if (m_RequestedDirection.sqrMagnitude > 0.001f)
                {
                    // Compute the rotation needed to reach the desired direction
                    float angleTowardTarget = Vector3.SignedAngle(m_RequestedDirection, transform.forward, Vector3.up);
                    var rotatingAngle = Mathf.Sign(angleTowardTarget) * Mathf.Min(Mathf.Abs(angleTowardTarget), m_TurnSpeed * Time.deltaTime);
                    turnRotation = Quaternion.AngleAxis(-rotatingAngle, Vector3.up);
                }
                else
                {
                    turnRotation = Quaternion.identity;
                }
            }
            else
            {
                float turn = m_TurnInputValue * m_TurnSpeed * Time.deltaTime;

                // Make this into a rotation in the y axis.
                turnRotation = Quaternion.Euler (0f, turn, 0f);
            }

            // Apply this rotation to the rigidbody's rotation.
            m_Rigidbody.MoveRotation (m_Rigidbody.rotation * turnRotation);
        }

        public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0f)
        {
            // Direction from the center of the explosion to the rigidbody
            Vector3 explosionDir = transform.position - explosionPosition;
            float explosionDistance = explosionDir.magnitude;

            // Normalize and apply vertical modifier if exists
            if (upwardsModifier != 0)
            {
                explosionDir.y += upwardsModifier;
                explosionDir.Normalize();
            }
            else
            {
                explosionDir = explosionDir.normalized;
            }

            // Attenuation factor according to distance
            float attenuation = 1f - Mathf.Clamp01(explosionDistance / explosionRadius);

            // Resulting speed according to ForceMode
            Vector3 velocityChange = explosionDir * (explosionForce * attenuation);

            m_ExplosionForceValue = velocityChange;
        }
    }
}