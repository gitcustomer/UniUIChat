using UnityEngine;
using UnityEditor;
using UIReuse.AI;
using UIReuse.Utils;
using System.IO;

namespace UIReuse.Editor
{
    /// <summary>
    /// AI配置文件创建器 - 编辑器专用，使用AIConfigUtility工具类
    /// </summary>
    public static class AIConfigCreator
    {
        private const string RESOURCES_FOLDER = "Assets/Resources";
        
        [MenuItem("Tools/AI Assistant/Create Default AI Config")]
        public static void CreateDefaultAIConfig()
        {
            CreateAIConfig("AIConfig");
        }
        
        [MenuItem("Tools/AI Assistant/Create Custom AI Config")]
        public static void CreateCustomAIConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建AI配置文件",
                "CustomAIConfig",
                "asset",
                "选择保存位置"
            );
            
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            
            // 从路径提取文件名
            string fileName = Path.GetFileNameWithoutExtension(path);
            string directory = Path.GetDirectoryName(path);
            
            // 如果不在Resources文件夹下，需要提醒用户
            if (!path.Contains("Resources"))
            {
                bool moveToResources = EditorUtility.DisplayDialog(
                    "配置文件位置", 
                    "AI配置文件需要放在Resources文件夹下才能在运行时加载。\n是否移动到Resources文件夹？", 
                    "移动到Resources", 
                    "保持当前位置"
                );
                
                if (moveToResources)
                {
                    CreateAIConfig(fileName);
                    return;
                }
            }
            
            // 创建AIConfig实例
            AIConfig config = ScriptableObject.CreateInstance<AIConfig>();
            
            // 填充默认值
            AIConfigUtility.FillDefaultValues(config);
            
            // 保存到指定路径
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 选中并聚焦到创建的文件
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"✅ 已创建AI配置文件: {path}");
        }
        
        /// <summary>
        /// 创建AI配置文件的通用方法
        /// </summary>
        private static void CreateAIConfig(string configName)
        {
            // 确保Resources文件夹存在
            if (!AIConfigUtility.ResourcesFolderExists())
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            string configPath = AIConfigUtility.GetConfigAssetPath(configName);
            
            // 检查是否已存在配置文件
            if (AssetDatabase.LoadAssetAtPath<AIConfig>(configPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "AI配置已存在", 
                    $"配置文件 {configName} 已存在，是否要覆盖？", 
                    "覆盖", 
                    "取消"
                );
                
                if (!overwrite)
                {
                    return;
                }
            }
            
            // 创建AIConfig实例
            AIConfig config = ScriptableObject.CreateInstance<AIConfig>();
            
            // 使用工具类填充默认值
            AIConfigUtility.FillDefaultValues(config);
            
            // 保存到Assets
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 选中并聚焦到创建的文件
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"✅ 已创建AI配置文件: {configPath}");
            
            // 显示提示对话框
            EditorUtility.DisplayDialog(
                "AI配置创建成功", 
                $"AI配置文件已创建在:\n{configPath}\n\n请在Inspector中设置您的API Key。", 
                "确定"
            );
        }
        
        [MenuItem("Tools/AI Assistant/Open AI Config Folder")]
        public static void OpenAIConfigFolder()
        {
            string folderPath = Path.GetDirectoryName(AIConfigUtility.GetConfigAssetPath());
            EditorUtility.RevealInFinder(folderPath);
        }
        
        /// <summary>
        /// 验证菜单项是否可用
        /// </summary>
        [MenuItem("Tools/AI Assistant/Create Default AI Config", true)]
        public static bool ValidateCreateDefaultAIConfig()
        {
            return true; // 始终可用
        }
    }
    
    /// <summary>
    /// AIConfig的自定义Inspector
    /// </summary>
    [CustomEditor(typeof(AIConfig))]
    public class AIConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            AIConfig config = (AIConfig)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI助手配置", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // API配置区域
            EditorGUILayout.LabelField("API配置", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            // API Key输入（密码字段）
            EditorGUILayout.LabelField("API Key:");
            string currentApiKey = config.ApiKey;
            string newApiKey = EditorGUILayout.PasswordField(currentApiKey);
            if (newApiKey != currentApiKey)
            {
                config.SetApiKey(newApiKey);
                EditorUtility.SetDirty(config);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            
            // 使用默认Inspector绘制其他字段
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            
            // 配置验证
            if (config.IsValid())
            {
                EditorGUILayout.HelpBox("✅ 配置有效", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("❌ 请设置API Key", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            // 快速操作按钮
            EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("测试连接"))
            {
                TestConnection(config);
            }
            
            if (GUILayout.Button("重置为默认值"))
            {
                if (EditorUtility.DisplayDialog("重置配置", "确定要重置为默认值吗？", "确定", "取消"))
                {
                    AIConfigCreator.CreateDefaultAIConfig();
                }
            }
            
            GUILayout.EndHorizontal();
        }
        
        private async void TestConnection(AIConfig config)
        {
            if (!config.IsValid())
            {
                EditorUtility.DisplayDialog("配置无效", "请先设置有效的API Key", "确定");
                return;
            }
            
            EditorUtility.DisplayProgressBar("测试连接", "正在测试AI服务连接...", 0.5f);
            
            try
            {
                var service = LLMServiceFactory.Create(config);
                var testResponse = await service.ChatAsync("test");
                bool isAvailable = !string.IsNullOrEmpty(testResponse);
                
                EditorUtility.ClearProgressBar();
                
                if (isAvailable)
                {
                    EditorUtility.DisplayDialog("连接成功", "AI服务连接正常！", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("连接失败", "无法连接到AI服务，请检查API Key和网络连接。", "确定");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("连接错误", $"连接测试失败:\n{ex.Message}", "确定");
            }
        }
    }
}