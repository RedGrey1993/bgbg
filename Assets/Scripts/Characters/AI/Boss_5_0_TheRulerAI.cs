using System.Collections;
using System.Collections.Generic;
using NetworkMessageProto;
using TMPro;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_5_0_TheRulerAI : CharacterBaseAI
{
    private static WaitForSeconds _waitForSeconds1 = new WaitForSeconds(1f);

    // Inspector
    [SerializeField] private List<GameObject> pokeMinionPrefabs;
    [SerializeField] private GameObject summonPokeEffectPrefab;
    [SerializeField] private GameObject speedupEffectPrefab;
    [SerializeField] private GameObject rageEffectPrefab;
    [SerializeField] private int pokeMinionBuffTime = 5;
    [SerializeField] private AudioClip energyWaveAccumulateSound;
    [SerializeField] private AudioClip energyWaveShootSound;
    [SerializeField] private GameObject energyWavePrefab;
    [SerializeField] private GameObject virtualScreen;
    [SerializeField] private Animator screenAnim;

    private List<BossPrefabInfo> prevBossPrefabInfos;
    protected override void SubclassStart()
    {
        prevBossPrefabInfos = new List<BossPrefabInfo>();
        foreach (int stage in GameManager.Instance.PassedStages)
        {
            LevelData levelData = LevelDatabase.Instance.GetLevelData(stage);
            for (int i = 0; i < levelData.bossPrefabs.Count; ++i)
            {
                prevBossPrefabInfos.Add(new BossPrefabInfo
                {
                    StageId = stage,
                    PrefabId = i,
                });
            }
        }
        // 随机排序prevBossPrefabs；Fisher-Yates 洗牌算法，时间复杂度为 O(n)，且能保证每个排列出现的概率相等
        System.Random rng = new ();
        for (int i = prevBossPrefabInfos.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (prevBossPrefabInfos[i], prevBossPrefabInfos[j]) = (prevBossPrefabInfos[j], prevBossPrefabInfos[i]); // 交换元素
        }
    }

    #region AI Logic / Update Input
    // 统治者不能移动，会坐在原地，然后召唤或使用一些全场技能
    protected override void UpdateMoveInput()
    {
        characterInput.MoveInput = Vector2.zero;
    }

    // 统治者不需要设置LookInput，他在协程中直接攻击最新的目标位置
    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }
    #endregion

    #region Attack Action
    // private HashSet<int> existingBosses = new HashSet<int>();
    private List<GameObject> existingBosses = new ();
    private int bossIdx = 0;
    protected override void AttackAction()
    {
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            if (Time.time < nextAtkTime) return;
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            existingBosses.RemoveAll(obj => obj == null);
            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            if (hpRatio > 0.4f)
            {
                int rndSkillId = Random.Range(0, 2);
                if (rndSkillId == 0 && bossIdx < prevBossPrefabInfos.Count && existingBosses.Count < 2)
                {
                    if (summonCoroutine == null && isShowingVirtualScreen == false)
                    {
                        Debug.Log("fhhtest, Summon::::::");
                        summonCoroutine = StartCoroutine(SummonBoss());
                    }
                }
                else
                {
                    if (explosionCoroutine == null && isShowingVirtualScreen == false)
                    {
                        Debug.Log("fhhtest, Explosion::::::");
                        explosionCoroutine = StartCoroutine(Explosion());
                    }
                }
            }
            else if (hpRatio > 0.1f)
            {
                if (teleportCoroutine == null)
                {
                    Debug.Log("fhhtest, Teleport::::::");
                    teleportCoroutine = StartCoroutine(TeleportAndPreviousBossAttack());
                }
            }
            else
            {
                // TODO: 终极格式化，时间设置为合理的时间，15s或30s；
                formattingCoroutine ??= StartCoroutine(Formatting(60f, AggroTarget));
                explosionCoroutine ??= StartCoroutine(Explosion(true));
                teleportCoroutine ??= StartCoroutine(TeleportAndPreviousBossAttack(false));
            }
        }
    }
    #endregion

    private bool isShowingVirtualScreen = false;
    #region 技能1，召唤
    private Coroutine summonCoroutine = null;
    private IEnumerator SummonBoss()
    {
        isShowingVirtualScreen = true;
        virtualScreen.SetActive(true);
        var animClips = screenAnim.runtimeAnimatorController.animationClips;
        float showTime = 0.5f, dismissTime = 0.5f;
        foreach (var clip in animClips)
        {
            if (clip.name == "VirtualScreenShowing")
            {
                showTime = clip.length;
            }
            else if (clip.name == "VirtualScreenDismiss")
            {
                dismissTime = clip.length;
            }
        }
        // yield return new WaitForSeconds(showTime);

        var rulerClips = animator.runtimeAnimatorController.animationClips;
        float summonTime = 0.5f;
        foreach (var clip in rulerClips)
        {
            if (clip.name == "Pointing")
            {
                summonTime = clip.length;
            }
        }
        animator.SetTrigger("Pointing");
        yield return new WaitForSeconds(Mathf.Max(showTime, summonTime));
        screenAnim.Play("VirtualScreenDismiss");
        yield return new WaitForSeconds(dismissTime);
        virtualScreen.SetActive(false);
        isShowingVirtualScreen = false;

        while (existingBosses.Count < 2 && bossIdx < prevBossPrefabInfos.Count)
        {
            // 召唤之前的boss
            int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
            var room = LevelManager.Instance.Rooms[roomId];
            var bossPrefabInfo = prevBossPrefabInfos[bossIdx++];
            var bossPrefab = LevelDatabase.Instance.GetBossPrefab(bossPrefabInfo.StageId, bossPrefabInfo.PrefabId);
            var charData = bossPrefab.GetComponent<CharacterStatus>().characterData;
            int extentsX = (int)charData.bound.extents.x, extentsY = (int)charData.bound.extents.y;
            int theRulerHeight = (int)CharacterData.bound.extents.y;
            var rndX = Random.Range(room.xMin + 1 + extentsX + 0.1f, room.xMin + room.width - extentsX - 0.1f);
            var rndY = Random.Range(room.yMin + 1 + extentsY + 0.1f, room.yMin + room.height - theRulerHeight - extentsY - 0.1f);
            Vector2 position = new Vector2(rndX, rndY);
            GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.summonEffectPrefab, position);
            yield return new WaitForSeconds(1.5f);
            Destroy(summonEffect);

            GameObject boss = CharacterManager.Instance.InstantiateBossObject(bossPrefab, position, bossPrefabInfo.StageId, bossPrefabInfo.PrefabId, null);
            // GameObject boss = LevelManager.Instance.InstantiateTemporaryObject(bossPrefab, position);
            boss.name = Constants.SummonBossName;
            boss.tag = gameObject.tag;
            if (boss.layer == LayerMask.NameToLayer("Default")) boss.layer = gameObject.layer;
            var bossStatus = boss.GetComponent<CharacterStatus>();
            bossStatus.IsBoss = true;
            existingBosses.Add(boss);
        }

        summonCoroutine = null;
    }
    #endregion

    #region 技能2，爆炸
    private Coroutine explosionCoroutine;
    private IEnumerator Explosion(bool explosionSameTime = false)
    {
        isShowingVirtualScreen = true;
        virtualScreen.SetActive(true);
        var animClips = screenAnim.runtimeAnimatorController.animationClips;
        float showTime = 0.5f, dismissTime = 0.5f;
        foreach (var clip in animClips)
        {
            if (clip.name == "VirtualScreenShowing")
            {
                showTime = clip.length;
            }
            else if (clip.name == "VirtualScreenDismiss")
            {
                dismissTime = clip.length;
            }
        }
        // yield return new WaitForSeconds(showTime);

        var rulerClips = animator.runtimeAnimatorController.animationClips;
        float summonTime = 0.5f;
        foreach (var clip in rulerClips)
        {
            if (clip.name == "Pointing")
            {
                summonTime = clip.length;
            }
        }
        animator.SetTrigger("Pointing");
        yield return new WaitForSeconds(Mathf.Max(showTime, summonTime));

        screenAnim.Play("VirtualScreenDismiss");
        yield return new WaitForSeconds(dismissTime);
        virtualScreen.SetActive(false);
        isShowingVirtualScreen = false;

        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        var room = LevelManager.Instance.Rooms[roomId];

        Bounds bossBound = CharacterData.bound;
        var bossPos = transform.position;
        float explosionRatio = 0.6f;
        int tileNumber = (int)((room.width - 1) * (room.height - 1) * explosionRatio);
        List<Vector2Int> tilePositions = new List<Vector2Int>();
        Vector2Int startPos = new Vector2Int((int)room.center.x, (int)room.center.y);
        if (AggroTarget != null) 
            startPos = new Vector2Int((int)AggroTarget.transform.position.x, (int)AggroTarget.transform.position.y);
        tilePositions.Add(startPos);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        visited.Add(startPos);

        for (int i = 0; i < tileNumber; i++)
        {
            int dir = Random.Range(0, 4);
            Vector2Int from = tilePositions[Random.Range(0, tilePositions.Count)];
            Vector2Int to = from;
            switch (dir)
            {
                case 0:
                    to = from + Vector2Int.up;
                    break;
                case 1:
                    to = from + Vector2Int.down;
                    break;
                case 2:
                    to = from + Vector2Int.left;
                    break;
                case 3:
                    to = from + Vector2Int.right;
                    break;
            }
            if (visited.Contains(to)
                || to.x > bossPos.x - bossBound.extents.x && to.x < bossPos.x + bossBound.extents.x && to.y > bossPos.y - bossBound.extents.y && to.y < bossPos.y + bossBound.extents.y)
            {
                continue;
            }
            if (to.x > (int)room.xMin && to.x < (int)room.xMax && to.y > (int)room.yMin && to.y < (int)room.yMax)
            {
                tilePositions.Add(to);
                visited.Add(to);
            }
        }

        foreach (var tilePos in tilePositions)
        {
            LevelManager.Instance.SetFloorTileExplosionWarning(new Vector3Int(tilePos.x, tilePos.y, 0));
        }
        yield return new WaitForSeconds(1f);

        foreach (var tilePos in tilePositions)
        {
            StartCoroutine(PlayExplosionEffect(tilePos));
            if (!explosionSameTime) yield return new WaitForSeconds(0.02f);
        }

        yield return new WaitForSeconds(1f);
        foreach (var tilePos in tilePositions)
        {
            LevelManager.Instance.ResetFloorTile(new Vector3Int(tilePos.x, tilePos.y, 0));
        }

        if (isAi)
        {
            // 攻击完之后给1-2s的移动，避免过高频率传送和攻击
            yield return new WaitForSeconds(Random.Range(1, 2f));
        }

        explosionCoroutine = null;
    }

    private IEnumerator PlayExplosionEffect(Vector2Int position)
    {
        var explosionEffect = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.explosionEffectPrefab, new Vector2(position.x, position.y));
        var particleSystem = explosionEffect.GetComponentInChildren<ParticleSystem>();
        // LevelManager.Instance.SetFloorTileDestroyedAndCantPass(new Vector3Int(position.x, position.y, 0));
        yield return new WaitForSeconds(particleSystem.main.duration);
        Object.Destroy(explosionEffect);
    }
    #endregion

    #region 技能3，传送+高频率boss大招
    private Coroutine teleportCoroutine = null;
    private IEnumerator TeleportAndPreviousBossAttack(bool attack = true)
    {
        var teleportPrefab = CharacterData.teleportEffectPrefab;
        var teleportEffect1 = LevelManager.Instance.InstantiateTemporaryObject(teleportPrefab, transform.position);
        var particleSystem1 = teleportEffect1.GetComponentInChildren<ParticleSystem>();
        yield return new WaitForSeconds(particleSystem1.main.duration / 2);
        SetTheRulerAlpha(0f);
        yield return new WaitForSeconds(particleSystem1.main.duration / 2);
        Object.Destroy(teleportEffect1);

        if (attack)
        {
            int rndAtk = Random.Range(0, 3);
            if (rndAtk == 0)
            {
                SkillData skillData = SkillDatabase.Instance.GetActiveSkill(Constants.PhantomChargeSkillId);
                skillData.executor.ExecuteSkill(gameObject, skillData);
            }
            else if (rndAtk == 1)
                energyWaveCoroutine ??= StartCoroutine(Boss2_EnergyWave(8));
            else if (rndAtk == 2)
                summonPokesCoroutine ??= StartCoroutine(Boss3_SummonAndStrengthenPokes(3));
        }

        var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        var room = LevelManager.Instance.Rooms[roomId];
        var targetPos = room.center;
        int rnd = Random.Range(0, 4);
        Vector2 lookTo = Vector2.down;
        switch (rnd)
        {
            case 0: // left
                {
                    targetPos.x = room.xMin + 1 + CharacterData.bound.extents.y;
                    targetPos.y = Random.Range(room.yMin + 1 + CharacterData.bound.extents.x, room.yMax - CharacterData.bound.extents.x);
                    lookTo = new Vector2(1, 0);
                    break;
                }
            case 1: // right
                {
                    targetPos.x = room.xMax - CharacterData.bound.extents.y;
                    targetPos.y = Random.Range(room.yMin + 1 + CharacterData.bound.extents.x, room.yMax - CharacterData.bound.extents.x);
                    lookTo = new Vector2(-1, 0);
                    break;
                }
            case 2: // top
                {
                    targetPos.x = Random.Range(room.xMin + 1 + CharacterData.bound.extents.x, room.xMax - CharacterData.bound.extents.x);
                    targetPos.y = room.yMax - CharacterData.bound.extents.y;
                    lookTo = new Vector2(0, -1);
                    break;
                }
            case 3: // bottom
                {
                    targetPos.x = Random.Range(room.xMin + 1 + CharacterData.bound.extents.x, room.xMax - CharacterData.bound.extents.x);
                    targetPos.y = room.yMin + 1 + CharacterData.bound.extents.y;
                    lookTo = new Vector2(0, 1);
                    break;
                }
        }
        Transform childTransform1 = transform.GetChild(0);
        childTransform1.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), lookTo); // 45度
        Transform childTransform2 = transform.GetChild(1);
        childTransform2.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), lookTo); // 45度
        // Transform childTransform3 = transform.GetChild(2);
        virtualScreen.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), lookTo); // 45度

        var teleportEffect2 = LevelManager.Instance.InstantiateTemporaryObject(teleportPrefab, targetPos);
        var particleSystem2 = teleportEffect2.GetComponentInChildren<ParticleSystem>();
        transform.position = targetPos;
        yield return new WaitForSeconds(particleSystem2.main.duration / 2);
        SetTheRulerAlpha(1f);
        yield return new WaitForSeconds(particleSystem2.main.duration / 2);
        Object.Destroy(teleportEffect2);

        if (isAi)
        {
            // 攻击完之后给1-2s的移动，避免过高频率传送和攻击
            yield return new WaitForSeconds(Random.Range(1, 2f));
        }
        teleportCoroutine = null;
    }

    private void SetTheRulerAlpha(float to)
    {
        SkinnedMeshRenderer skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        var color1 = skinnedMeshRenderer.material.color;
        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        var color2 = meshRenderer.material.color;
        color1.a = color2.a = to;
        skinnedMeshRenderer.material.color = color1;
        meshRenderer.material.color = color2;
    }

    private Coroutine energyWaveCoroutine = null;
    private int rotateDir = 1;
    private IEnumerator Boss2_EnergyWave(int count)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];
        var vfx = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.accumulateEffectPrefab, room.center);
        if (energyWaveAccumulateSound)
        {
            if (OneShotAudioSource == null)
                OneShotAudioSource = gameObject.AddComponent<AudioSource>();
            OneShotAudioSource.PlayOneShot(energyWaveAccumulateSound);
        }
        yield return new WaitForSeconds(1.6f);
        Vector2 lookInput = Vector2.right;
        if (AggroTarget != null)
            lookInput = characterInput.LookInput = AggroTarget.transform.position - transform.position;
        // 攻击0.5s之前的位置，给玩家一些缓冲时间
        yield return new WaitForSeconds(0.5f);
        Object.Destroy(vfx);

        float angle = 360f / count;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, angle);
        for (int i = 0; i < count; i++)
        {
            // 计算子弹的初始位置，稍微偏离玩家边界
            Vector2 waveOffset = lookInput.normalized;
            Vector2 waveStartPosition = room.center;
            waveStartPosition += waveOffset;

            var energeWave = LevelManager.Instance.InstantiateTemporaryObject(energyWavePrefab, waveStartPosition);
            EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
            energyWaveScript.PosOffset = waveOffset;
            energyWaveScript.Direction = lookInput.normalized;
            energyWaveScript.OwnerStatus = characterStatus;
            energyWaveScript.FollowOwner = false;
            energyWaveScript.Rotate = count > 1 ? rotateDir : 0;

            lookInput = rotationPlus * lookInput;
            Object.Destroy(energeWave, 2.5f);
        }
        rotateDir = -rotateDir;

        if (energyWaveShootSound)
        {
            if (OneShotAudioSource == null)
                OneShotAudioSource = gameObject.AddComponent<AudioSource>();
            OneShotAudioSource.PlayOneShot(energyWaveShootSound);
        }

        yield return new WaitForSeconds(2.5f);
        energyWaveCoroutine = null;
    }

    private int pokeIdx = 0;
    private List<GameObject> existingPokes = new();
    private Coroutine summonPokesCoroutine = null;
    private IEnumerator Boss3_SummonAndStrengthenPokes(int count)
    {
        existingPokes.RemoveAll(obj => obj == null);
        int needCount = count - existingPokes.Count;

        while (existingPokes.Count < count)
        {
            // 召唤小弟
            int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
            var room = LevelManager.Instance.Rooms[roomId];
            var pokePrefab = pokeMinionPrefabs[pokeIdx];
            pokeIdx = (pokeIdx + 1) % pokeMinionPrefabs.Count;

            var col2d = pokePrefab.GetComponentInChildren<Collider2D>();
            Vector2 summonPosition = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2d.bounds);

            GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(summonPokeEffectPrefab, summonPosition);
            yield return new WaitForSeconds(1.5f);
            Destroy(summonEffect);
            GameObject pokeMinion = LevelManager.Instance.InstantiateTemporaryObject(pokePrefab, summonPosition);
            pokeMinion.tag = gameObject.tag;
            if (pokeMinion.layer == LayerMask.NameToLayer("Default")) pokeMinion.layer = gameObject.layer;
            Physics2D.SyncTransforms();
            var col2D = pokeMinion.GetComponentInChildren<Collider2D>();
            var tarPos = pokeMinion.transform.position;
            tarPos.y += col2D.bounds.extents.y + 0.5f;
            // 将血条显示到对象的头上
            var miniStatusCanvas = pokeMinion.GetComponentInChildren<Canvas>();
            if (miniStatusCanvas == null)
            {
                var obj1 = Instantiate(CharacterManager.Instance.miniStatusPrefab, tarPos, Quaternion.identity);
                obj1.transform.SetParent(pokeMinion.transform);
                miniStatusCanvas = obj1.GetComponent<Canvas>();
            }
            existingPokes.Add(pokeMinion);
        }

        if (existingPokes.Count > 0)
        {
            foreach (var poke in existingPokes)
            {
                if (poke == null) continue;
                var rnd = Random.Range(0, 2);
                // speedup
                {
                    var speedupEffect = LevelManager.Instance.InstantiateTemporaryObject(speedupEffectPrefab, poke.transform.position);
                    Destroy(speedupEffect, 3);
                    StartCoroutine(SpeedUp(poke));
                    yield return new WaitForSeconds(0.5f);
                }
                // rage
                {
                    var rageEffect = LevelManager.Instance.InstantiateTemporaryObject(rageEffectPrefab, poke.transform.position);
                    Destroy(rageEffect, 3);
                    StartCoroutine(Rage(poke));
                }
            }
        }

        summonPokesCoroutine = null;
    }

    private IEnumerator SpeedUp(GameObject poke)
    {
        var status = poke.GetComponent<CharacterStatus>();
        status.State.MoveSpeed *= 2;

        yield return new WaitForSeconds(pokeMinionBuffTime);
        status.State.MoveSpeed /= 2;
    }

    private IEnumerator Rage(GameObject poke)
    {
        var status = poke.GetComponent<CharacterStatus>();
        status.State.Damage *= 2;
        var sr = poke.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.red;
        }

        yield return new WaitForSeconds(pokeMinionBuffTime);
        status.State.Damage /= 2;
        if (sr != null)
        {
            sr.color = Color.white;
        }
    }
    #endregion

    #region 技能4，格式化+连续爆炸
    private Coroutine formattingCoroutine = null;
    private IEnumerator Formatting(float duration, GameObject aggroTarget)
    {
        UIManager.Instance.formatPanel.SetActive(true);

        virtualScreen.SetActive(true);
        var animClips = screenAnim.runtimeAnimatorController.animationClips;
        float showTime = 0.5f;
        foreach (var clip in animClips)
        {
            if (clip.name == "VirtualScreenShowing")
            {
                showTime = clip.length;
            }
        }
        yield return new WaitForSeconds(showTime);

        var textObj = UIManager.Instance.formatPanel.GetComponentInChildren<TextMeshProUGUI>();
        var slider = UIManager.Instance.formatPanel.GetComponentInChildren<UnityEngine.UI.Slider>();
        slider.maxValue = 100;

        float startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            int percent = Mathf.CeilToInt((Time.time - startTime) / duration * 100);
            textObj.text = $"SYSTEM PURGE, FORMATTING {percent}%...";
            slider.value = percent;
            yield return _waitForSeconds1;
        }
        slider.value = 100;
        textObj.text = "SYSTEM PURGE, FORMATTING COMPLETE!";
        if (aggroTarget != null)
        {
            var playerStatus = aggroTarget.GetComponent<CharacterStatus>();
            playerStatus.TakeDamage_Host(100000000, null, DamageType.Bullet);
        }

        UIManager.Instance.formatPanel.SetActive(false);
        // formattingCoroutine = null;
    }
    #endregion

    protected override void SubclassOnDestroy()
    {
        UIManager.Instance.formatPanel.SetActive(false);
    }
}