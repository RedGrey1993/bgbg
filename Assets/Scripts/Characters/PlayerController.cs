using UnityEngine;
using UnityEngine.InputSystem;
using NetworkMessageProto;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // Private variables
    private Vector2 moveInput;
    private Vector2 lookInput;
    private CharacterBaseAI baseAi;
    private InputAction m_MoveAction;
    private InputAction m_LookAction;
    private InputAction m_JumpAction;
    private static InputSystem_Actions s_InputActions;

    void Awake()
    {
        // Initialize the static input actions asset if it hasn't been already
        if (s_InputActions == null)
        {
            s_InputActions = new InputSystem_Actions();
        }

        m_MoveAction = s_InputActions.Player.Move;
        m_LookAction = s_InputActions.Player.Look;
        m_JumpAction = s_InputActions.Player.Jump;

        baseAi = GetComponent<CharacterBaseAI>();
    }

    private void OnEnable()
    {
        m_MoveAction?.Enable();
        m_LookAction?.Enable();
        m_JumpAction?.Enable();

        m_JumpAction.performed += OnJumpPerformed;
    }
    
    private void OnDisable()
    {
        m_JumpAction.performed -= OnJumpPerformed;

        m_MoveAction?.Disable();
        m_LookAction?.Disable();
        m_JumpAction?.Disable();
    }

    void Start()
    {

    }

    void Update()
    {
        // Read input from the resolved action
        moveInput = m_MoveAction.ReadValue<Vector2>();
        lookInput = m_LookAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // We are in an online lobby, send input to the network manager
        uint tick = (uint)(Time.realtimeSinceStartup * 1000);
        var inputMsg = new InputMessage
        {
            PlayerId = CharacterManager.Instance.MyInfo.Id,
            Tick = tick,
            MoveInput = new Vec2
            {
                X = moveInput.x,
                Y = moveInput.y
            },
            LookInput = new Vec2
            {
                X = lookInput.x,
                Y = lookInput.y
            }
        };
        var genericMessage = new GenericMessage
        {
            // 所有输入指令都由Client自己处理，但Host会定期同步执行后的状态
            Target = (uint)MessageTarget.All,
            Type = (uint)MessageType.Input,
            InputMsg = inputMsg
        };
        // 输入指令频率很高，丢失了也会很快被下一次输入覆盖，因此不需要可靠传输
        MessageManager.Instance.SendMessage(genericMessage, false);
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        StartCoroutine(UseActiveSkill());
    }

    private IEnumerator UseActiveSkill()
    {
        PlayerState state = baseAi.characterStatus.State;
        SkillData skillData = SkillDatabase.Instance.GetActiveSkill(state.ActiveSkillId);
        if (skillData != null && (state.ActiveSkillCurCd == -1 || state.ActiveSkillCurCd >= skillData.cooldown))
        {
            // TODO: cd设置为实际的0，当前暂时是无cd
            // state.ActiveSkillCurCd = 0;
            state.ActiveSkillCurCd = -1;
            var spc = UIManager.Instance.GetComponent<StatusPanelController>();
            spc.UpdateMyStatusUI(state);
            yield return new WaitUntil(() => baseAi.CanUseActiveItem());
            skillData.executor.ExecuteSkill(gameObject, skillData);
        }
    }
}
