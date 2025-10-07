

using System.Collections.Generic;
using UnityEngine;

public static class CharacterAIManager
{
    public static ICharacterAI GetCharacterAI(GameObject character)
    {
        CharacterStatus characterStatus = character.GetComponent<CharacterStatus>();
        if (characterStatus == null)
        {
            Debug.LogError("[CharacterAIManager] CharacterStatus component not found on character GameObject.");
            return null;
        }

        switch (characterStatus.characterData.CharacterType)
        {
            case CharacterType.SuperMinionNormal:
                return new SuperMinionAI(character);
            case CharacterType.Minion_1_0_Stomper:
                return new Minion_1_0_StomperAI(character);
            default:
                Debug.LogWarning($"[CharacterAIManager] No AI for character type: {characterStatus.characterData.CharacterType}");
                return null;
        }
    }
}