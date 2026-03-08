using UnityEngine;

namespace UIReuse.AI
{
    /// <summary>
    /// LLM服务提供商类型
    /// </summary>
    public enum LLMProviderType
    {
        OpenAI,
        Custom
    }
    
    /// <summary>
    /// AI配置数据 - 可在Inspector中配置
    /// </summary>
    [CreateAssetMenu(fileName = "AIConfig", menuName = "AI/AI Configuration")]
    public class AIConfig : ScriptableObject
    {
        [Header("服务提供商")]
        [SerializeField] private LLMProviderType providerType = LLMProviderType.OpenAI;
        
        [Header("API配置")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private string baseUrl = "https://api.openai.com/v1";
        
        [Header("模型参数")]
        [SerializeField] private string modelName = "gpt-3.5-turbo";
        [SerializeField, Range(0f, 2f)] private float temperature = 0.7f;
        [SerializeField, Range(1, 4000)] private int maxTokens = 1000;
        [SerializeField, Range(0f, 2f)] private float topP = 1f;
        [SerializeField, Range(-2f, 2f)] private float frequencyPenalty = 0f;
        [SerializeField, Range(-2f, 2f)] private float presencePenalty = 0f;
        
        [Header("高级设置")]
        [SerializeField, Range(5, 120)] private int timeoutSeconds = 30;
        [SerializeField, Range(1, 5)] private int retryCount = 3;
        [SerializeField] private bool enableLogging = true;
        
        // 属性访问器
        public LLMProviderType ProviderType => providerType;
        public string ApiKey => apiKey;
        public string BaseUrl => baseUrl;
        public string ModelName => modelName;
        public float Temperature => temperature;
        public int MaxTokens => maxTokens;
        public float TopP => topP;
        public float FrequencyPenalty => frequencyPenalty;
        public float PresencePenalty => presencePenalty;
        public int TimeoutSeconds => timeoutSeconds;
        public int RetryCount => retryCount;
        public bool EnableLogging => enableLogging;
        
        /// <summary>
        /// 设置API Key（运行时修改）
        /// </summary>
        public void SetApiKey(string newApiKey)
        {
            apiKey = newApiKey;
        }
        
        /// <summary>
        /// 设置模型名称（运行时修改）
        /// </summary>
        public void SetModelName(string newModelName)
        {
            modelName = newModelName;
        }
        
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(modelName);
        }
        
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValidForRequest()
        {
            return IsValid() && maxTokens > 0 && temperature >= 0 && temperature <= 2;
        }
    }
}