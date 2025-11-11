using System.Collections.Generic;
using UnityEngine;

public class TilemapDatabase : MonoBehaviour
{
    // stage -> tileType -> tileTemplate
    public Dictionary<int, Dictionary<TileType, List<TileTemplate>>> StageTileTemplates = new();

    public static TilemapDatabase Instance { get; private set; }

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
        LoadTilemaps();
    }

    private void LoadTilemaps()
    {
        TileTemplate[] tileTemplates = Resources.LoadAll<TileTemplate>("TileTemplates");

        foreach (var tileTemplate in tileTemplates)
        {
            if (!StageTileTemplates.ContainsKey(tileTemplate.stage))
            {
                StageTileTemplates.Add(tileTemplate.stage, new Dictionary<TileType, List<TileTemplate>>());
            }

            if (!StageTileTemplates[tileTemplate.stage].ContainsKey(tileTemplate.tileType))
            {
                StageTileTemplates[tileTemplate.stage].Add(tileTemplate.tileType, new List<TileTemplate>());
            }

            StageTileTemplates[tileTemplate.stage][tileTemplate.tileType].Add(tileTemplate);
        }
    }
    
    public TileTemplate GetRandomTileTemplate(int stage, TileType tileType)
    {
        List<TileTemplate> templates;
        if (!StageTileTemplates.ContainsKey(stage) || !StageTileTemplates[stage].ContainsKey(tileType))
        {
            if (!StageTileTemplates.ContainsKey(1) || !StageTileTemplates[1].ContainsKey(tileType))
            {
                return null;
            }
            else
            {
                templates = StageTileTemplates[1][tileType];
            }
        }
        else
        {
            templates = StageTileTemplates[stage][tileType];
        }

        var cumulativeWeight = 0;
        foreach (var template in templates) cumulativeWeight += template.weight;

        var randomWeight = Random.Range(0, cumulativeWeight);
        foreach (var template in templates)
        {
            randomWeight -= template.weight;
            if (randomWeight < 0)
            {
                return template;
            }
        }

        return templates[Random.Range(0, templates.Count)];
    }
}