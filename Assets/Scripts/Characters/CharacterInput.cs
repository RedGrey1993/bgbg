using UnityEngine;

[RequireComponent(typeof(CharacterStatus))]
public class CharacterInput : MonoBehaviour
{
    public Vector2 MoveInput = Vector2.zero;
    public Vector2 MoveAdditionalInput = Vector2.zero;
    public Vector2 LookInput = Vector2.zero;

    private CharacterStatus characterStatus;
    private CharacterData CharacterData => characterStatus.characterData;

    void Awake()
    {
        characterStatus = GetComponent<CharacterStatus>();
    }

    public void NormalizeMoveInput()
    {
        if (MoveInput.sqrMagnitude < 0.1f) return;
        // Handle diagonal movement setting
        if (!CharacterData.canMoveDiagonally)
        {
            // Prioritize the axis with larger absolute value
            if (Mathf.Abs(MoveInput.x) > Mathf.Abs(MoveInput.y))
            {
                MoveInput = new Vector2(MoveInput.x, 0).normalized;
            }
            else
            {
                MoveInput = new Vector2(0, MoveInput.y).normalized;
            }
        }
        // Normalize for consistent speed in all directions
        else // (playerStatus.canMoveDiagonally)
        {
            MoveInput = MoveInput.normalized;
        }
    }

    public void NormalizeLookInput()
    {
        if (LookInput.sqrMagnitude < 0.1f) return;
        // Handle diagonal shooting setting
        if (!CharacterData.canAttackDiagonally)
        {
            // Restrict look input to horizontal or vertical only
            if (Mathf.Abs(LookInput.x) > Mathf.Abs(LookInput.y))
            {
                LookInput = new Vector2(LookInput.x, 0).normalized;
            }
            else
            {
                LookInput = new Vector2(0, LookInput.y).normalized;
            }
        }
    }

    void OnDestroy()
    {
        Constants.goToCharacterInput.Remove(gameObject);
    }
}
