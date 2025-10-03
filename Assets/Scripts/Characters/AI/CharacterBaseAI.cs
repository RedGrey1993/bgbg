

using UnityEngine;

public abstract class CharacterBaseAI : ICharacterAI
{
    protected GameObject character;
    protected CharacterInput characterInput;
    protected CharacterStatus characterStatus;
    protected CharacterData CharacterData => characterStatus.characterData;

    protected CharacterBaseAI(GameObject character)
    {
        this.character = character;
        characterInput = character.GetComponent<CharacterInput>();
        characterStatus = character.GetComponent<CharacterStatus>();
    }

    protected void Move_RandomMove()
    {
        int horizontalDir = Random.Range(-1, 2);
        int verticalDir = Random.Range(-1, 2);
        characterInput.MoveInput = new Vector2(horizontalDir, verticalDir).normalized;
    }

    protected void Move_ChaseNearestEnemy()
    {

    }

    protected bool IsAlive()
    {
        return characterStatus.IsAlive();
    }

    #region ICharacterAI implementation
    public abstract void Update();
    #endregion
}