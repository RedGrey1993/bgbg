using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq; // 用于方便的数组处理
#endif

public class AutoIncrementSO : ScriptableObject
{
    [Header("Database Settings")]
    [Tooltip("该ID会自动递增，请勿随意手动修改")]
    [SerializeField] private int _id = -1;
    
    // 公开只读属性，防止代码里误改
    public int ID => _id;

    // 当你在 Project 窗口右键 -> Create 时调用
    private void Reset()
    {
        AssignNextAvailableID();
    }

    [ContextMenu("Auto Assign Next ID")]
    public void AssignNextAvailableID()
    {
#if UNITY_EDITOR
        // 1. 获取当前脚本的具体类型（这样如果你有 WeaponSO 和 ArmorSO，它们的ID是分开计数的）
        string typeName = this.GetType().Name;
        
        // 2. 搜索项目中所有该类型的资源 GUID
        string[] guids = AssetDatabase.FindAssets($"t:{typeName}");

        int maxID = 0;
        bool duplicateFound = false;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // 加载资源
            AutoIncrementSO asset = AssetDatabase.LoadAssetAtPath<AutoIncrementSO>(path);
            
            // 跳过自己（如果是刚创建还没保存的，可能需要跳过）
            if (asset == this) continue;

            if (asset != null)
            {
                if (asset._id > maxID)
                {
                    maxID = asset._id;
                }
                
                // 顺便检查一下有没有人和我现在的 ID 冲突（针对 Ctrl+D 的情况）
                if (asset._id == this._id && this._id != -1)
                {
                    duplicateFound = true;
                }
            }
        }

        // 3. 赋值逻辑
        // 如果当前 ID 是 -1（新创建）或者发现了重复 ID，就分配新 ID
        if (_id == -1 || duplicateFound)
        {
            _id = maxID + 1;
            EditorUtility.SetDirty(this);
            Debug.Log($"<color=green>[AutoID]</color> Assigned ID {_id} to {name}");
        }
#endif
    }
    
    // 这是一个保险措施：每次你在 Inspector 修改东西时，检查一下是否 ID 冲突
    // 注意：为了性能，这里只做简单检查，或者你可以把这个注释掉，只依赖 Reset
//     private void OnValidate()
//     {
// #if UNITY_EDITOR
//         if (_id == -1) 
//         {
//              AssignNextAvailableID();
//         }
// #endif
//     }
}