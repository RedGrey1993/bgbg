

using System.Collections;
using UnityEngine;

// 弹跳南瓜
public class Minion_16_HopperPumpAI : CharacterBaseAI
{
    public AudioClip jumpSound;
    public AudioClip smallJumpSound;
    public GameObject shadowPrefab;

    protected override void MoveAction()
    {
        if (jumpCoroutine == null)
        {
            var moveInput = characterInput.MoveInput;
            if (moveInput.sqrMagnitude < 0.1f) return;
            
            if (Random.value < 0.1f && AggroTarget != null) 
            {
                jumpCoroutine = StartCoroutine(JumpToTarget(AggroTarget, characterInput.MoveInput, 10f, 0.8f));
            }
            else
            {
                jumpCoroutine = StartCoroutine(JumpToTarget(null, characterInput.MoveInput, 2f, 0.5f));
            }
        }
        rb.linearVelocity = curVelocity;
    }

    private Vector2 curVelocity = Vector2.zero;
    private Coroutine jumpCoroutine = null;
    private IEnumerator JumpToTarget(GameObject aggroTarget, Vector2 moveInput, float jumpHeight, float jumpDuration = 0.5f)
    {
        isAttack = true;

        var characterBound = col2D.bounds;

        // animator.SetTrigger("Jump");
        float prepareJumpDuration = 0.5f;
        float afterJumpDuration = 0.5f;
        if (aggroTarget == null)
        {
            OneShotAudioSource.PlayOneShot(smallJumpSound);
        }
        else 
        {
            OneShotAudioSource.PlayOneShot(jumpSound);
            prepareJumpDuration = 2.3f;
            afterJumpDuration = 1.2f;
        }

        yield return new WaitForSeconds(prepareJumpDuration);

        Vector2 targetPos = (Vector2)transform.position + moveInput * CharacterData.MoveSpeed;
        if (aggroTarget != null) 
        {
            var tarStatus = aggroTarget.GetCharacterStatus();
            if (tarStatus != null) {
                targetPos = tarStatus.CharacterAI.col2D.bounds.center;
                targetPos.y += tarStatus.CharacterAI.col2D.bounds.extents.y;
            }
        }

        var shadowPos = transform.position;
        shadowPos.y -= characterBound.extents.y;
        var shadowObj = LevelManager.Instance.InstantiateTemporaryObject(shadowPrefab, shadowPos);
        shadowObj.transform.localScale = transform.localScale * 2f;
        TobeDestroyed.Add(shadowObj);

        // y = jumpHeight - 1/2*g*t^2
        Vector2 startPos = transform.position;
        float g = jumpHeight * 2 / (jumpDuration * jumpDuration) * 4;

        Transform spriteTransform = gameObject.transform.GetChild(0);
        int initialLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer(Constants.LayerOnlyProcessObstacle);

        curVelocity = (targetPos - startPos) / jumpDuration;
        float elapsedTime = 0;
        while (elapsedTime < jumpDuration)
        {
            float tt = Mathf.Lerp(0, jumpDuration, elapsedTime / jumpDuration);
            // 抛物线
            float t = tt - jumpDuration / 2;
            float y = jumpHeight - g * (t * t) / 2;

            if (elapsedTime > jumpDuration / 2f && y < characterBound.size.y)
            {
                gameObject.layer = initialLayer;
            }

            spriteTransform.position = new Vector3(transform.position.x, transform.position.y + y);
            shadowObj.transform.position = new Vector3(transform.position.x, transform.position.y - characterBound.extents.y);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        gameObject.layer = initialLayer;
        curVelocity = Vector2.zero;
        spriteTransform.position = new Vector3(transform.position.x, transform.position.y);
        shadowObj.transform.position = new Vector3(transform.position.x, transform.position.y - characterBound.extents.y);
        // rb.MovePosition(targetPos);
        Destroy(shadowObj);
        TobeDestroyed.Remove(shadowObj);
        yield return new WaitForSeconds(afterJumpDuration);

        isAttack = false;
        jumpCoroutine = null;
    }
}