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

        healthText.text = $"HP: {state.CurrentHp}/{state.MaxHp}";
        expText.text = $"Data Shards: {state.CurrentExp}/{maxExp}";

        abilityRadarChart.SetStats(state);

        SkillData skillData = SkillDatabase.Instance.GetActiveSkill(state.ActiveSkillId);
        activeSkillIcon.SetSkillData(skillData);
    }

    public void UpdateTipsText(int roomNo)
    {
        tipsText.text = $"Stage:{GameManager.Instance.CurrentStage}\nRoom: #{roomNo}";
    }
}
