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
        CharacterAI?.OnCollision(collision);
    }
}
