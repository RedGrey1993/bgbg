using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class IDManager : EditorWindow
{
    [MenuItem("Tools/Renumber All Items")]
    public static void RenumberAll()
    {
        // 假设你的道具类名叫 GameItemSO，请根据实际情况修改
        string typeName = "CharacterSpawnConfigSO"; 
        
        string[] guids = AssetDatabase.FindAssets($"t:{typeName}");
        Debug.Log($"Find {guids.Length} {typeName}");
        List<AutoIncrementSO> assets = new List<AutoIncrementSO>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<AutoIncrementSO>(path);
            if (so != null) assets.Add(so);
        }

        // 按照现在的 ID 排序，或者按照名字排序
        // 这里演示按名字排序，这样 ID 就会跟文件名顺序一致
        assets = assets.OrderBy(x => x.name).ToList();

        for (int i = 0; i < assets.Count; i++)
        {
            // 利用反射或者把 _id 改为 public/internal 来修改
            // 这里为了演示，假设你有办法修改 _id (比如把 _id 设为 protected 并在该类中通过 SerializedObject 修改)
            
            SerializedObject serializedSO = new SerializedObject(assets[i]);
            SerializedProperty idProp = serializedSO.FindProperty("_id");
            
            if (idProp.intValue != i + 1)
            {
                idProp.intValue = i + 1;
                serializedSO.ApplyModifiedProperties();
                Debug.Log($"Renamed {assets[i].name} to ID {i + 1}");
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("重排完成！");
    }
}