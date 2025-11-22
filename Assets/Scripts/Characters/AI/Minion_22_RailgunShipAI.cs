

using System.Collections;
using UnityEngine;

public class Minion_22_RailgunShipAI : CharacterBaseAI
{
    public GameObject vfxPrefab;
    public GameObject energyWavePrefab;
    public AudioClip energyWaveAccumulateSound;
    public AudioClip energyWaveShootSound;
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine ??= StartCoroutine(Attack_Shoot(lookInput));
        }
    }

    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;
        
        var vfx = Instantiate(vfxPrefab, transform);
        vfx.transform.localScale = Vector3.one * 0.3f;
        TobeDestroyed.Add(vfx);
        OneShotAudioSource.PlayOneShot(energyWaveAccumulateSound);

        yield return new WaitForSeconds(1.2f);

        Destroy(vfx);
        TobeDestroyed.Remove(vfx);

        Vector2 waveOffset = lookInput.normalized * (col2D.bounds.extents.magnitude + 0.1f);
        Vector2 waveStartPosition = transform.position;
        waveStartPosition += waveOffset;

        var energeWave = LevelManager.Instance.InstantiateTemporaryObject(energyWavePrefab, waveStartPosition);
        EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
        energyWaveScript.PosOffset = waveOffset;
        energyWaveScript.Direction = lookInput.normalized;
        energyWaveScript.OwnerStatus = characterStatus;
        energyWaveScript.Rotate = 0;

        OneShotAudioSource.PlayOneShot(energyWaveShootSound);

        Destroy(energeWave, 3f);
        yield return new WaitForSeconds(3f);

        // isAttack = false后才能移动
        isAttack = false; // isAttack=false后就不再设置朝向为LookInput，而是朝向MoveInput
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 shootCoroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        // shootCoroutine = null后才能再次使用该技能
        atkCoroutine = null;
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return atkCoroutine == null;
    }
}