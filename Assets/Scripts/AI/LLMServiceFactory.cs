using System;
using System.Collections.Generic;
using UIReuse.Service;

namespace UIReuse.AI
{
    /// <summary>
    /// LLM服务工厂 - 创建AI服务实例，支持多种模型提供商
    /// </summary>
    public static class LLMServiceFactory
    {
        /// <summary>
        /// 服务创建委托 - 使用 AIConfig 创建，包含所有配置信息
        /// </summary>
        public delegate ILLMService ServiceCreator(AIConfig config);
        
        /// <summary>
        /// 已注册的服务提供商
        /// </summary>
        private static readonly Dictionary<LLMProviderType, ServiceCreator> providers = 
            new Dictionary<LLMProviderType, ServiceCreator>();
        
        /// <summary>
        /// 静态构造函数 - 注册默认提供商
        /// </summary>
        static LLMServiceFactory()
        {
            Register(LLMProviderType.OpenAI, config => new OpenAIService(config));
            Register(LLMProviderType.Custom, config => new OpenAIService(config));
        }
        
        /// <summary>
        /// 注册新的服务提供商
        /// </summary>
        public static void Register(LLMProviderType type, ServiceCreator creator)
        {
            providers[type] = creator;
        }
        
        /// <summary>
        /// 使用配置创建服务
        /// </summary>
        public static ILLMService Create(AIConfig config)
        {
            if (config == null || !config.IsValid())
            {
                throw new ArgumentException("AI配置无效");
            }
            
            if (!providers.TryGetValue(config.ProviderType, out var creator))
            {
                throw new NotSupportedException($"不支持的服务提供商类型: {config.ProviderType}，请先调用 Register 注册");
            }
            
            return creator(config);
        }
    }
}