using NetworkMessageProto;
using UnityEngine;

public class MessageManager : MonoBehaviour
{
    public static MessageManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #region Send&Receive Message
    public void SendMessage(GenericMessage msg, bool reliable)
    {
        if (GameManager.Instance.IsLocal() || msg.Target == (uint)MessageTarget.Local)
        {
            ReceiveMessage(msg);
        }
        else
        {
            switch (msg.Target)
            {
                case (uint)MessageTarget.All:
                    {
                        LobbyNetworkManager.Instance.SendToAll(msg, reliable);
                        break;
                    }
                case (uint)MessageTarget.Host:
                    {
                        LobbyNetworkManager.Instance.SendToHost(msg, reliable);
                        break;
                    }
                case (uint)MessageTarget.Others:
                    {
                        LobbyNetworkManager.Instance.SendToOthers(msg, reliable);
                        break;
                    }
            }
        }
    }

    public void ReceiveMessage(GenericMessage msg)
    {
        if (msg == null) return;
        // Local消息：只有自己会发给自己，处理
        // All消息：Host和Client都处理
        // Others消息：收到了就处理（自己不会收到，只发送给其他人）
        // Host消息：只有Host处理（理论上只有Host才会收到）
        // IsLocal()：离线模式，处理所有消息
        if (!GameManager.Instance.IsLocal() && msg.Target == (uint)MessageTarget.Host && !GameManager.Instance.IsHost()) return;

        switch (msg.Type)
        {
            case (uint)MessageType.Input:
                {
                    OnPlayerInput(msg.InputMsg);
                    break;
                }
            case (uint)MessageType.TransformStateUpdate:
                {
                    CharacterManager.Instance.ApplyTransformStateUpdate_Client(msg.StateMsg);
                    break;
                }
            case (uint)MessageType.FullTransformState:
                {
                    CharacterManager.Instance.ApplyFullTransformState_Client(msg.StateMsg);
                    break;
                }
            case (uint)MessageType.PlayersUpdate:
                {
                    CharacterManager.Instance.UpdatePlayers(msg.PlayersMsg);
                    break;
                }
            case (uint)MessageType.LearnSkill:
                {
                    CharacterManager.Instance.CalculateSkillEffect_Host(msg.LearnSkillMsg.SkillId, msg.LearnSkillMsg.PlayerId);
                    break;
                }
            case (uint)MessageType.FireRateStateUpdate:
                {
                    CharacterManager.Instance.UpdateAbilityState_Client(msg);
                    break;
                }
        }
    }
    #endregion

    public void OnPlayerInput(InputMessage inputMsg)
    {
        if (CharacterManager.Instance.playerObjects.TryGetValue(inputMsg.PlayerId, out GameObject playerObject))
        {
            var playerInput = playerObject.GetComponent<CharacterInput>();
            if (playerInput != null)
            {
                // if (IsLocalOrHost()) // 移动指令都由Host处理后再同步给Client，射击指令（LookInput）后Client自己处理
                // 上面的注释是老逻辑，现在最新的逻辑是所有输入指令都由Client自己处理，但Host会定期同步执行后的状态
                playerInput.MoveInput = new Vector2(inputMsg.MoveInput.X, inputMsg.MoveInput.Y);
                playerInput.LookInput = new Vector2(inputMsg.LookInput.X, inputMsg.LookInput.Y);
            }
        }
    }
}
