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

    public (TileTemplate, int) GetRandomTileTemplate(int stage, TileType tileType)
    {
        List<TileTemplate> templates;
        if (!StageTileTemplates.ContainsKey(stage) || !StageTileTemplates[stage].ContainsKey(tileType))
        {
            return (null, -1);
        }
        else
        {
            templates = StageTileTemplates[stage][tileType];
        }

        var cumulativeWeight = 0;
        foreach (var template in templates) cumulativeWeight += template.weight;

        var randomWeight = Random.Range(0, cumulativeWeight);
        for (int i = 0; i < templates.Count; ++i)
        {
            var template = templates[i];
            randomWeight -= template.weight;
            if (randomWeight < 0)
            {
                return (template, i);
            }
        }

        int rnd = Random.Range(0, templates.Count);
        return (templates[rnd], rnd);
    }
    
    public TileTemplate GetTileTemplate(int stage, TileType tileType, int tileTemplateId)
    {
        return StageTileTemplates[stage][tileType][tileTemplateId];
    }
}