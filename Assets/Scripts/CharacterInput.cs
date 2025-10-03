using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 MoveInput;
    public Vector2 LookInput;

    private CharacterStatus characterStatus;
    public CharacterData CharacterData => characterStatus.characterData;
    public ICharacterAI CharacterAI { get; private set; }

    void Awake()
    {
        characterStatus = GetComponent<CharacterStatus>();
        CharacterAI = CharacterAIManager.GetCharacterAI(gameObject);
    }

    void Update()
    {
        CharacterAI?.Update();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleAICollision(collision);
    }

    #region AI Logic
    private void HandleAICollision(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && CharacterAI != null
            && !characterStatus.IsDead())
        {
            switch (CharacterData.CharacterType)
            {
                case CharacterType.PlayerAI:
                // AI玩家角色逻辑（暂时共用小兵逻辑）
                case CharacterType.SuperMinionNormal:
                    // 小兵角色逻辑
                    {
                        // 碰撞到墙壁或者其他敌人时，翻转移动方向
                        if (collision.gameObject.CompareTag(Constants.TagWall) || collision.gameObject.CompareTag(Constants.TagEnemy))
                        {
                            if (Mathf.Abs(MoveInput.x) > 0.1f && Mathf.Abs(MoveInput.y) > 0.1f)
                            {
                                // 对角线方向，随机翻转水平或垂直方向
                                if (Random.value < 0.5f)
                                {
                                    MoveInput.x = -MoveInput.x;
                                    MoveInput.y = 0;
                                }
                                else
                                {
                                    MoveInput.x = 0;
                                    MoveInput.y = -MoveInput.y;
                                }
                            }
                            else if (Mathf.Abs(MoveInput.x) > 0.1f)
                            {
                                MoveInput.x = -MoveInput.x;
                            }
                            else if (Mathf.Abs(MoveInput.y) > 0.1f)
                            {
                                MoveInput.y = -MoveInput.y;
                            }
                        }
                        break;
                    }
            }
        }
    }
    #endregion
}
