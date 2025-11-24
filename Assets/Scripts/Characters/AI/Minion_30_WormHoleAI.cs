using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 虫洞（Worm Hole），不断生产小怪
// 不移动，但不断释放 小型无人机干扰玩家。
public class Minion_30_WormHoleAI : CharacterBaseAI
{
    public float rotateSpeed = 120;
    public float spawnInterval = 10;
    public List<GameObject> spawnPrefabs;
    private float rotateZ = 0;
    private Transform child;
    private float lastSpawnTime = 0;

    protected override void SubclassStart()
    {
        child = transform.GetChild(0);
    }

    protected override void SubclassFixedUpdate()
    {
        rotateZ += rotateSpeed * Time.fixedDeltaTime;
        child.localRotation = Quaternion.Euler(0, 0, rotateZ);

        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            if (Time.time - lastSpawnTime > spawnInterval)
            {
                lastSpawnTime = Time.time;
                StartCoroutine(SpawnMinion());
            }
        }
    }

    private IEnumerator SpawnMinion()
    {
        Vector2 lookInput = Vector2.down;
        if (AggroTarget != null)
        {
            lookInput = (AggroTarget.transform.position - transform.position).normalized;
        }
        yield break;

        // GameObject pokeball = null;
        // if (aiScript.CharacterData.Is3DModel())
        // {
        //     float holdBallTime = 0.87f;
        //     aiScript.PlayAnimationAllLayers("Throw Object");
        //     var rightHandTransform = aiScript.animator.GetBoneTransform(HumanBodyBones.RightHand);
        //     pokeball = Instantiate(pokeballPrefab, rightHandTransform);
        //     pokeball.transform.localScale = Vector3.one * 0.3f
        //         * aiScript.transform.lossyScale.x / pokeball.transform.lossyScale.x;
        //     aiScript.TobeDestroyed.Add(pokeball);
        //     yield return new WaitForSeconds(holdBallTime);
        // }

        // for (int idx = 0; idx < aliveNotSummonedPokePrefabs.Count; idx++)
        // {
        //     // 召唤小弟
        //     int roomId = LevelManager.Instance.GetRoomNoByPosition(aiScript.transform.position);
        //     var room = LevelManager.Instance.Rooms[roomId];
        //     var pokePrefab = aliveNotSummonedPokePrefabs[idx];
        //     Vector2 summonPosition = aiScript.transform.position;
        //     summonPosition += lookInput * 5;
        //     if (summonPosition.x < room.xMin + 2) summonPosition.x = room.xMin + 2;
        //     else if (summonPosition.x > room.xMax - 1) summonPosition.x = room.xMax - 1;
        //     if (summonPosition.y < room.yMin + 2) summonPosition.y = room.yMin + 2;
        //     else if (summonPosition.y > room.yMax - 1) summonPosition.y = room.yMax - 1;

        //     float summonTime = 1.5f;
        //     if (idx == 0)
        //     {
        //         GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(summonPokeEffectPrefab, summonPosition);
        //         if (aiScript.CharacterData.Is3DModel())
        //         {
        //             var startPos = pokeball.transform.position;
        //             float elapsedTime = 0;
        //             while (elapsedTime < summonTime)
        //             {
        //                 elapsedTime += Time.deltaTime;
        //                 pokeball.transform.position = Vector3.Lerp(startPos, summonPosition, elapsedTime / summonTime);
        //                 yield return null;
        //             }
        //             pokeball.transform.position = summonPosition;

        //             Destroy(pokeball);
        //             aiScript.TobeDestroyed.Remove(pokeball);
        //         }
        //         else
        //         {
        //             yield return new WaitForSeconds(summonTime);
        //         }
        //         Destroy(summonEffect);
        //     }

        //     GameObject pokeMinion = CharacterManager.Instance.InstantiateCompanionObject(pokePrefab.Item1, summonPosition);
        //     pokeMinion.name += pokePrefab.Item2;
        //     pokeMinion.tag = aiScript.gameObject.tag;
        //     if (pokeMinion.layer == LayerMask.NameToLayer("Default")) pokeMinion.layer = aiScript.gameObject.layer;
        //     Physics2D.SyncTransforms();
        //     var col2D = pokeMinion.GetComponentInChildren<Collider2D>();
        //     var tarPos = pokeMinion.transform.position;
        //     tarPos.y += col2D.bounds.extents.y + 0.5f;
        //     // 将血条显示到对象的头上
        //     var miniStatusCanvas = pokeMinion.GetComponentInChildren<Canvas>();
        //     if (miniStatusCanvas == null)
        //     {
        //         var obj1 = Instantiate(CharacterManager.Instance.miniStatusPrefab, tarPos, Quaternion.identity);
        //         obj1.transform.SetParent(pokeMinion.transform);
        //         miniStatusCanvas = obj1.GetComponent<Canvas>();
        //     }
        //     if (pokeMinion.CompareTag(Constants.TagPlayer))
        //     {
        //         var playerNameText = miniStatusCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
        //         if (playerNameText != null)
        //         {
        //             playerNameText.gameObject.SetActive(true);
        //             playerNameText.text = $"Companion #{pokePrefab.Item2 + 1}";
        //         }
        //         var obj2 = Instantiate(capturedMinionCanvas, tarPos, Quaternion.identity);
        //         obj2.transform.SetParent(pokeMinion.transform);
        //     }
        //     var pokeStatus = pokeMinion.GetComponent<CharacterStatus>();
        //     pokeStatus.Trainer = aiScript.characterStatus;
        //     if (!aiScript.isAi)
        //     {
        //         var pokeState = aiScript.characterStatus.State.CatchedMinionStates[pokePrefab.Item2];
        //         pokeState.Position = null;
        //         pokeState.CurrentHp = pokeState.MaxHp;
        //         pokeState.Damage = (int)(aiScript.characterStatus.State.Damage * pokeState.Scale / 2);
        //         if (pokeState.Damage < 1) pokeState.Damage = 1;
        //         pokeStatus.SetState(pokeState);
        //     }
        //     aiScript.ExistingPokes.Add((pokeMinion, pokePrefab.Item2));
        // }

        // aiScript.isAttack = false;

        // if (aiScript.isAi)
        // {
        //     // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        //     yield return new WaitForSeconds(Random.Range(1, 3f));
        // }

        // aiScript.ActiveSkillCoroutine = null;
    }
}