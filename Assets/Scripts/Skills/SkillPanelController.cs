using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

// 将一组待选技能封装成一个请求
public class SkillChoiceRequest
{
    public List<SkillData> SkillsToChoose;

    public SkillChoiceRequest(List<SkillData> skills)
    {
        SkillsToChoose = skills;
    }
}

public class SkillPanelController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject learnableSkillsPanel;
    [SerializeField] private Transform ownedSkillsContainer;
    [SerializeField] private Transform learnableSkillsContainer;

    [Header("Prefabs")]
    [SerializeField] private GameObject ownedSkillIconPrefab; // 一个带 Image 和 SkillIcon 脚本的 Prefab
    [SerializeField] private GameObject skillChoiceButtonPrefab; // 一个带 Button 和 SkillChoiceButton 脚本的 Prefab

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private float choiceTime = 30f;

    // 技能选择队列
    private Queue<SkillChoiceRequest> skillChoiceQueue = new Queue<SkillChoiceRequest>();
    private bool isChoosing = false;
    // 如果玩家一直不选择技能进入下一关，则设置这个变量为 true，强制在进入下一关之前自动选择一个技能
    public bool ForceRandomChoose { get; set; } = false;

    public void RandomizeNewPassiveSkillChoice()
    {
        UIManager.Instance.OpenSkillPanel();
        var skillNum = SkillDatabase.Instance.PassiveSkills.Count;
        List<SkillData> skills = new List<SkillData>();
        for (int i = 0; i < Constants.SkillChooseNumber; i++)
        {
            var skillId = Random.Range(0, skillNum);
            var skillData = SkillDatabase.Instance.PassiveSkills[skillId];
            skills.Add(skillData);
        }
        AddNewSkillChoice(skills);
    }

    // 触发技能选择
    private void AddNewSkillChoice(List<SkillData> skills)
    {
        skillChoiceQueue.Enqueue(new SkillChoiceRequest(skills));
        // 如果当前没有在选择，则开始处理队列
        if (!isChoosing)
        {
            StartCoroutine(ProcessSkillQueue());
        }
    }

    public void Initialize(List<SkillData> initialSkills)
    {
        UIManager.Instance.OpenSkillPanel();
        foreach (Transform child in ownedSkillsContainer)
        {
            if (child.GetComponent<OwnedSkillIcon>() != null) Destroy(child.gameObject);
        }
        foreach (var skill in initialSkills)
        {
            GameObject iconObj = Instantiate(ownedSkillIconPrefab, ownedSkillsContainer);
            iconObj.GetComponent<OwnedSkillIcon>().SetSkillData(skill);
        }
        // 清理之前的Coroutines
        StopAllCoroutines();
        skillChoiceQueue.Clear();
        isChoosing = false;
        learnableSkillsPanel.SetActive(false);
    }

    public List<uint> GetOwnedSkillIds()
    {
        List<uint> skillIds = new List<uint>();
        foreach (Transform child in ownedSkillsContainer)
        {
            var icon = child.GetComponent<OwnedSkillIcon>();
            if (icon != null && icon.SkillData != null)
            {
                skillIds.Add(icon.SkillData.id);
            }
        }
        return skillIds;
    }

    private IEnumerator ProcessSkillQueue()
    {
        // 当队列中有待处理项时循环
        while (skillChoiceQueue.Count > 0)
        {
            isChoosing = true;
            SkillChoiceRequest currentRequest = skillChoiceQueue.Dequeue();
            yield return StartCoroutine(HandleSkillChoice(currentRequest));
        }
        isChoosing = false; // 队列处理完毕
    }

    private IEnumerator HandleSkillChoice(SkillChoiceRequest request)
    {
        learnableSkillsPanel.SetActive(true);
        // 1. 清理并显示待选技能UI
        // 定义回调函数：当按钮被点击时，这个函数会被调用
        SkillData selectedSkill = null; // 用于存储玩家的选择
        System.Action<SkillData> onSkillSelected = (skill) =>
        {
            selectedSkill = skill;
        };
        PopulateAndSetupLearnableSkills(request.SkillsToChoose, onSkillSelected);

        // 2. 开始倒计时
        float timer = choiceTime;
        while (timer > 0)
        {
            // 如果玩家已经点击按钮做出了选择，则跳出循环
            if (selectedSkill != null || ForceRandomChoose)
            {
                break;
            }

            timer -= Time.deltaTime;
            timerText.text = "Please choose one item\n" + Mathf.CeilToInt(timer).ToString() + " seconds left";
            yield return null;
        }

        // 3. 处理选择结果
        // 如果循环结束时玩家仍未选择，则默认选择第一个
        if (selectedSkill == null)
        {
            selectedSkill = request.SkillsToChoose[0];
            Debug.Log("时间到！默认选择: " + selectedSkill.skillName);
        }
        else
        {
            Debug.Log("玩家选择: " + selectedSkill.skillName);
        }

        // 4. 应用技能并清理UI
        LearnNewSkill(selectedSkill);
        learnableSkillsPanel.SetActive(false);
        UIManager.Instance.HideSkillPanel(); // 关闭技能面板
    }

    private void PopulateAndSetupLearnableSkills(List<SkillData> skills, System.Action<SkillData> callback)
    {
        // 清理旧的按钮
        foreach (Transform child in learnableSkillsContainer)
        {
            if (child.GetComponent<LearnableSkillButton>() != null) Destroy(child.gameObject);
        }

        // 根据技能数据创建并设置新的按钮
        foreach (var skillData in skills)
        {
            GameObject buttonObj = Instantiate(skillChoiceButtonPrefab, learnableSkillsContainer);
            // 在这里立刻对新创建的按钮进行设置！
            buttonObj.GetComponent<LearnableSkillButton>().Setup(skillData, callback);
        }
    }

    private void LearnNewSkill(SkillData newSkill)
    {
        // 在这里添加你的游戏逻辑，比如给玩家添加能力
        CharacterManager.Instance.LearnSkill(newSkill);

        // 更新“持有技能”UI
        GameObject iconObj = Instantiate(ownedSkillIconPrefab, ownedSkillsContainer);
        iconObj.GetComponent<OwnedSkillIcon>().SetSkillData(newSkill);
    }

    // --- 用于测试的函数 ---
    private int curSkillId = 0;
    void Update()
    {
#if DEBUG
        // 按下 "1" 键，添加第一组技能到队列
        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Debug.Log("添加第一组技能到队列...");
            UIManager.Instance.OpenSkillPanel();
            var skillNum = SkillDatabase.Instance.PassiveSkills.Count;
            List<SkillData> testSkills = new List<SkillData>();
            for (int i = 0; i < 3; i++)
            {
                // var skillData = SkillDatabase.Instance.PassiveSkills[curSkillId];
                var skillData = SkillDatabase.Instance.GetPassiveSkill(6);
                testSkills.Add(skillData);
            }
            curSkillId = (curSkillId + 1) % skillNum;
            AddNewSkillChoice(testSkills);
        }
#endif
    }
}
