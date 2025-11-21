

using System.Collections;
using UnityEngine;

public class Minion_17_WarpMageAI : CharacterBaseAI
{
    public GameObject teleportPrefab;

    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        // StartCoroutine(Teleport());
        if (CharacterData.causeCollisionDamage)
            ProcessCollisionDamage(collision);
    }

    // private IEnumerator Teleport()
    // {
        
    // }
}