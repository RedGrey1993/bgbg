using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class LearnableSkillButton : MonoBehaviour
{
    public SkillData skillData { get; private set; }
    private Action<SkillData> onSkillSelectedCallback;

    private Button button;
    private Image iconImage; // 按钮上的图标
    private TextMeshProUGUI nameText; // 按钮上的技能名称

    private void Awake()
    {
        button = GetComponent<Button>();
        iconImage = GetComponent<Image>();
        nameText = GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(OnButtonClicked);
    }

    public void Setup(SkillData data, Action<SkillData> callback)
    {
        skillData = data;
        iconImage.sprite = data.icon;
        nameText.text = data.skillName;
        onSkillSelectedCallback = callback;
    }

    private void OnButtonClicked()
    {
        onSkillSelectedCallback?.Invoke(skillData);
    }
}
