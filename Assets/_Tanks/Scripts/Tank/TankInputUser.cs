using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Tanks.Complete
{
    /// <summary>
    /// Contains the Input System Input User that is linked to a Tank. This take care of copying the default input actions
    /// from the Project Settings and link them to the given Input User. This is necessary as otherwise the project wide
    /// input actions would keep getting overriden by whoever bind to them last.
    /// </summary>
    public class TankInputUser : MonoBehaviour
    {
        public InputUser InputUser => m_InputUser;                      // The InputUser for this tank 
        public InputActionAsset ActionAsset => m_LocalActionAsset;      // The local Input Action Asset copy only binded to the right device
        
        private InputUser m_InputUser;                                  
        private InputActionAsset m_LocalActionAsset;
        
        private void Awake()
        {
            if (InputSystem.actions != null)
            {
                m_LocalActionAsset = InputActionAsset.FromJson(InputSystem.actions.ToJson());
            }
            
            // By default, pair to the keyboard if available, otherwise create user without paired devices
            if (Keyboard.current != null)
            {
                try
                {
                    SetNewInputUser(InputUser.PerformPairingWithDevice(Keyboard.current));
                }
                catch
                {
                    m_InputUser = InputUser.CreateUserWithoutPairedDevices();
                    if (m_LocalActionAsset != null)
                        m_InputUser.AssociateActionsWithUser(m_LocalActionAsset);
                }
            }
            else
            {
                m_InputUser = InputUser.CreateUserWithoutPairedDevices();
                if (m_LocalActionAsset != null)
                    m_InputUser.AssociateActionsWithUser(m_LocalActionAsset);
            }
        }

        /// <summary>
        /// Activate the given control scheme on the Input User
        /// </summary>
        /// <param name="name">The name of the ControlScheme to activate</param>
        public void ActivateScheme(string name)
        {
            m_InputUser.ActivateControlScheme(name);
        }

        /// <summary>
        /// Replace the input user contained in this component by the given one
        /// </summary>
        /// <param name="user">The new InputUser</param>
        public void SetNewInputUser(InputUser user)
        {
            if (!user.valid)
                return;

            m_InputUser = user;
            m_InputUser.AssociateActionsWithUser(m_LocalActionAsset);
            
            // If this user have an associated controlScheme (e.g. in this project KeyboardRight or KeyboardLeft) we
            // re-activate this scheme on the input user. This is necessary as we changed the associated actions in the above
            // line, so those new action haven't had their control scheme set, and this will set it.
            if(m_InputUser.controlScheme.HasValue)
                m_InputUser.ActivateControlScheme(m_InputUser.controlScheme.Value);
        }
    }
}
