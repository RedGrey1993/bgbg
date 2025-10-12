using UnityEngine;
using System.Collections.Generic;

public class LevelDatabase : MonoBehaviour
{
    // 使用字典可以让我们通过技能名称快速查找，非常方便
    public List<LevelData> LevelDatas { get; private set; }
    public Dictionary<int, LevelData> LevelDictionary { get; private set; }

    // 单例模式，方便从任何地方访问这个技能数据库
    public static LevelDatabase Instance { get; private set; }

    private void Awake()
    {
        // 实现单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 确保在切换场景时数据库不被销毁

        // 加载所有关卡数据
        LoadLevelDatas();
    }

    private void LoadLevelDatas()
    {
        // 初始化列表
        LevelDatas = new List<LevelData>();
        LevelDictionary = new Dictionary<int, LevelData>();

        // 从 Resources/Configs/Levels/ 文件夹中加载所有 LevelData 类型的资产
        // 如果你直接放在 Resources 根目录，路径就是 ""
        LevelData[] levelAssets = Resources.LoadAll<LevelData>("Configs/Levels");

        // 将加载的关卡数组转换为字典
        foreach (LevelData levelData in levelAssets)
        {
            LevelDatas.Add(levelData);
            LevelDictionary.Add(levelData.level, levelData);
        }

        Debug.Log($"成功加载 {LevelDatas.Count} 个关卡到数据库。");
    }

    public bool IsSysBugStage(int level)
    {
        if (level == LevelDatas.Count)
        {
            return true;
        }
        return false;
    }

    // 提供一个公共方法来根据ID获取技能
    public LevelData GetLevelData(int level)
    {
        if (level < 1 || level > LevelDatas.Count || LevelDictionary[level] == null)
        {
            Debug.Log("No LevelData for level " + level);
            return null;
        }
        return LevelDictionary[level];
    }
}