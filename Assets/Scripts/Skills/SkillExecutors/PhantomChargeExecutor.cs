using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "PhantomChargeExecutor", menuName = "Skills/Effects/Phantom Charge")]
public class PhantomChargeExecutor : SkillExecutor
{
    public GameObject chargeEffectPrefab;
    public GameObject phantomChargePrefab;
    private CharacterBaseAI aiScript = null;

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");

        aiScript = playerObj.GetComponent<CharacterBaseAI>();
        if (aiScript.ActiveSkillCoroutine != null) return;
        var tarEnemy = CharacterManager.Instance.FindNearestEnemyInAngle(playerObj, aiScript.LookDir, 90);

        aiScript.ActiveSkillCoroutine = aiScript.StartCoroutine(Attack_Charge(playerObj, tarEnemy));
    }
    
    private IEnumerator Attack_Charge(GameObject owner, GameObject target)
    {
        var characterStatus = owner.GetComponent<CharacterStatus>();
        // 幻影冲锋时还能够射击或者移动，所以不设置isAttack = true;
        int roomId = LevelManager.Instance.GetRoomNoByPosition(owner.transform.position);
        var room = LevelManager.Instance.Rooms[roomId];
        Vector2 targetPos = room.center;
        if (target != null && LevelManager.Instance.InSameRoom(owner, target))
            targetPos = target.transform.position;
        var chargeEffect = LevelManager.Instance.InstantiateTemporaryObject(chargeEffectPrefab, targetPos);
        chargeEffect.tag = owner.tag;
        if (chargeEffect.layer == LayerMask.NameToLayer("Default")) chargeEffect.layer = owner.layer;
        aiScript.TobeDestroyed.Add(chargeEffect);
        yield return new WaitForSeconds(1f / characterStatus.State.AttackFrequency);
        var horizontalStartPos = targetPos;
        int dir = Random.Range(0, 2);
        Vector2 horizontalVelocity = Vector2.zero;
        Vector2 hLookTo;
        if (dir == 0)
        {
            horizontalStartPos.x = room.xMin + 1;
            horizontalVelocity.x = characterStatus.State.BulletSpeed;
            hLookTo = Vector2.right;
        }
        else
        {
            horizontalStartPos.x = room.xMax - 1;
            horizontalVelocity.x = -characterStatus.State.BulletSpeed;
            hLookTo = Vector2.left;
        }

        var verticalStartPos = targetPos;
        dir = Random.Range(0, 2);
        Vector2 verticalVelocity = Vector2.zero;
        Vector2 vLookTo;
        if (dir == 0)
        {
            verticalStartPos.y = room.yMin + 1;
            verticalVelocity.y = characterStatus.State.BulletSpeed;
            vLookTo = Vector2.up;
        }
        else
        {
            verticalStartPos.y = room.yMax - 1;
            verticalVelocity.y = -characterStatus.State.BulletSpeed;
            vLookTo = Vector2.down;
        }

        var horizontalPhantomCharge = LevelManager.Instance.InstantiateTemporaryObject(phantomChargePrefab, horizontalStartPos);
        var verticalPhantomCharge = LevelManager.Instance.InstantiateTemporaryObject(phantomChargePrefab, verticalStartPos);
        horizontalPhantomCharge.tag = owner.tag;
        if (horizontalPhantomCharge.layer == LayerMask.NameToLayer("Default")) horizontalPhantomCharge.layer = owner.layer;
        verticalPhantomCharge.tag = owner.tag;
        if (verticalPhantomCharge.layer == LayerMask.NameToLayer("Default")) verticalPhantomCharge.layer = owner.layer;
        aiScript.TobeDestroyed.Add(horizontalPhantomCharge);
        aiScript.TobeDestroyed.Add(verticalPhantomCharge);

        horizontalPhantomCharge.GetComponent<PhantomChargeDamage>().OwnerStatus = characterStatus;
        verticalPhantomCharge.GetComponent<PhantomChargeDamage>().OwnerStatus = characterStatus;

        horizontalPhantomCharge.transform.localRotation = Quaternion.LookRotation(Vector3.forward, hLookTo);
        verticalPhantomCharge.transform.localRotation = Quaternion.LookRotation(Vector3.forward, vLookTo);
        var hrb = horizontalPhantomCharge.GetComponent<Rigidbody2D>();
        var vrb = verticalPhantomCharge.GetComponent<Rigidbody2D>();
        hrb.linearVelocity = horizontalVelocity;
        vrb.linearVelocity = verticalVelocity;

        while (LevelManager.Instance.InSameRoom(horizontalPhantomCharge, owner) || LevelManager.Instance.InSameRoom(verticalPhantomCharge, owner))
        {
            yield return null;
        }

        Destroy(chargeEffect);
        Destroy(horizontalPhantomCharge);
        Destroy(verticalPhantomCharge);

        aiScript.ActiveSkillCoroutine = null;
    }
}