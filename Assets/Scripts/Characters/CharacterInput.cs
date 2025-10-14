using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 MoveInput = Vector2.zero;
    public Vector2 LookInput = Vector2.zero;

    private CharacterStatus characterStatus;
    public CharacterData CharacterData => characterStatus.characterData;
    public ICharacterAI CharacterAI { get; private set; }

    void Awake()
    {
        characterStatus = GetComponent<CharacterStatus>();
    }

    void Start()
    {
        // 等到对应的gameObject中Awake相关的逻辑都执行完毕再获取AI对象
        CharacterAI = CharacterAIManager.GetCharacterAI(gameObject);
    }

    void Update()
    {
        CharacterAI?.Update();
    }

    void FixedUpdate()
    {
        CharacterAI?.FixedUpdate();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CharacterAI?.OnCollisionEnter(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        CharacterAI?.OnCollisionStay(collision);
    }
}
