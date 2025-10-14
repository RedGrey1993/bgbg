

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
            case CharacterType.Contra_Bill:
                return new ContraBillAI(character);
            case CharacterType.Minion_1_0_Stomper:
                return new Minion_1_0_StomperAI(character);
            case CharacterType.Minion_1_1_BusterBot:
                return new Minion_1_1_BusterBotAI(character);
            case CharacterType.Boss_1_0_PhantomTank:
                return new Boss_1_0_PhantomTankAI(character);
            case CharacterType.Minion_2_0_GlitchSlime:
                return new Minion_2_0_GlitchSlimeAI(character);
            default:
                Debug.LogWarning($"[CharacterAIManager] No AI for character type: {characterStatus.characterData.CharacterType}");
                return null;
        }
    }
}