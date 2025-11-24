using System.Collections.Generic;
using NetworkMessageProto;
using TMPro;
using UnityEngine;

public class StatusPanelController : MonoBehaviour
{
    public GameObject statusPanel;
    public UnityEngine.UI.Slider healthSlider;
    public UnityEngine.UI.Slider expSlider;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI expText;
    public TextMeshProUGUI tipsText;
    public HexagonRadarChart abilityRadarChart;
    public OwnedSkillIcon activeSkillIcon;
    public UnityEngine.UI.Slider activeSkillCdSlider;

    public void ShowMyStatusUI()
    {
        statusPanel.SetActive(true);
    }

    public void HideMyStatusUI()
    {
        statusPanel.SetActive(false);
    }

    public void UpdateMyStatusUI(PlayerState state)
    {
        int idx = Mathf.Min(Mathf.Max(0, (int)state.CurrentLevel - 1), Constants.LevelUpExp.Length - 1);
        int maxExp = Constants.LevelUpExp[idx];
        healthSlider.maxValue = state.MaxHp;
        healthSlider.value = state.CurrentHp;

        expSlider.maxValue = maxExp;
        expSlider.value = state.CurrentExp;

        // 4舍5入保留1位小数
        healthText.text = $"HP: {state.CurrentHp:0.#}/{state.MaxHp}";
        expText.text = $"Data Shards: {state.CurrentExp}/{maxExp}";

        abilityRadarChart.SetStats(state);

        SkillData skillData = SkillDatabase.Instance.GetActiveSkill(state.ActiveSkillId);
        float alpha = 1f;
        if (skillData != null)
        {
            activeSkillCdSlider.gameObject.SetActive(true);
            if (state.ActiveSkillCurCd == -1 || state.ActiveSkillCurCd > skillData.cooldown)
                state.ActiveSkillCurCd = skillData.cooldown;
            activeSkillCdSlider.maxValue = skillData.cooldown;
            // if (state.ActiveSkillCurCd > activeSkillCdSlider.value)
            // {
            //     // TODO: 播放技能cd恢复音效
            // }
            activeSkillCdSlider.value = state.ActiveSkillCurCd;
            if (state.ActiveSkillCurCd < skillData.cooldown) alpha = 0.8f;
        }
        else
        {
            activeSkillCdSlider.gameObject.SetActive(false);
        }
        activeSkillIcon.SetSkillData(skillData, alpha);
    }

    public void UpdateTipsText(int roomNo)
    {
        tipsText.text = $"Stage:{GameManager.Instance.Storage.CurrentStage}\nRoom: #{roomNo}";
    }
}
