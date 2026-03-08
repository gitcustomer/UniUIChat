using UnityEngine;
using UIReuse.AI;

namespace UIReuse.Utils
{
    /// <summary>
    /// AI配置工具类 - 运行时和编辑器共用
    /// </summary>
    public static class AIConfigUtility
    {
        private const string DEFAULT_CONFIG_NAME = "AIConfig";
        private const string RESOURCES_PATH = "AIConfig";
        
        /// <summary>
        /// 运行时加载默认AI配置
        /// </summary>
        public static AIConfig LoadDefaultConfig()
        {
            var config = Resources.Load<AIConfig>(RESOURCES_PATH);
            if (config == null)
            {
                Debug.LogWarning($"未找到默认AI配置文件: Resources/{RESOURCES_PATH}.asset");
                Debug.LogWarning("请使用 Tools > AI Assistant > Create Default AI Config 创建配置文件");
            }
            return config;
        }
        
        /// <summary>
        /// 运行时加载指定名称的AI配置
        /// </summary>
        public static AIConfig LoadConfig(string configName)
        {
            var config = Resources.Load<AIConfig>(configName);
            if (config == null)
            {
                Debug.LogWarning($"未找到AI配置文件: Resources/{configName}.asset");
            }
            return config;
        }
        
        /// <summary>
        /// 创建运行时临时配置
        /// </summary>
        public static AIConfig CreateRuntimeConfig(string apiKey, string modelName = "gpt-3.5-turbo")
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            config.SetApiKey(apiKey);
            config.SetModelName(modelName);
            return config;
        }
        
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public static bool ValidateConfig(AIConfig config, bool logErrors = true)
        {
            if (config == null)
            {
                if (logErrors) Debug.LogError("AI配置为空");
                return false;
            }
            
            if (!config.IsValid())
            {
                if (logErrors) Debug.LogError("AI配置无效：API Key未设置或模型名称为空");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取所有可用的AI配置文件
        /// </summary>
        public static AIConfig[] LoadAllConfigs()
        {
            var configs = Resources.LoadAll<AIConfig>("");
            return configs;
        }
        
        /// <summary>
        /// 填充默认值到配置对象
        /// </summary>
        public static void FillDefaultValues(AIConfig config)
        {
            if (config == null) return;
            
            SetPrivateField(config, "modelName", "gpt-3.5-turbo");
            SetPrivateField(config, "temperature", 0.7f);
            SetPrivateField(config, "maxTokens", 1000);
            SetPrivateField(config, "topP", 1f);
            SetPrivateField(config, "frequencyPenalty", 0f);
            SetPrivateField(config, "presencePenalty", 0f);
            SetPrivateField(config, "baseUrl", "https://api.openai.com/v1");
            SetPrivateField(config, "timeoutSeconds", 30);
            SetPrivateField(config, "retryCount", 3);
            SetPrivateField(config, "enableLogging", true);
        }
        
        private static void SetPrivateField(AIConfig config, string fieldName, object value)
        {
            var field = typeof(AIConfig).GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(config, value);
            }
        }
        
        /// <summary>
        /// 获取配置文件的完整路径（编辑器用）
        /// </summary>
        public static string GetConfigAssetPath(string configName = DEFAULT_CONFIG_NAME)
        {
            return $"Assets/Resources/{configName}.asset";
        }
        
        /// <summary>
        /// 检查Resources文件夹是否存在（编辑器用）
        /// </summary>
        public static bool ResourcesFolderExists()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources");
#else
            return true;
#endif
        }
    }
}
