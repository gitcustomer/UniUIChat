using UnityEngine;
using UnityEngine.UIElements;
using UIReuse.Runtime;

namespace UIReuse.Demo
{
    /// <summary>
    /// AI助手演示组件 - 展示如何在运行时使用AI助手
    /// 可直接挂载到场景中的GameObject上作为使用示例
    /// </summary>
    public class AIAssistantDemo : MonoBehaviour
    {
        [Header("AI助手引用")]
        [SerializeField] private AIAssistantRuntimeUI aiAssistant;
        
        [Header("快捷键设置")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        
        [Header("调试选项")]
        [SerializeField] private bool showInstructionsOnStart = true;
        [SerializeField] private bool validateSetupOnStart = true;
        
        private void Start()
        {
            if (showInstructionsOnStart)
            {
                PrintUsageInstructions();
            }
            
            if (validateSetupOnStart)
            {
                ValidateSetup();
            }
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleAIAssistant();
            }
        }
        
        private void PrintUsageInstructions()
        {
            Debug.Log("=== AI助手使用说明 ===");
            Debug.Log($"按 {toggleKey} 键切换AI助手界面");
            Debug.Log("请确保已在 Resources/AIConfig.asset 中配置 API Key");
            Debug.Log("=======================");
        }
        
        private void ValidateSetup()
        {
            if (aiAssistant == null)
            {
                aiAssistant = FindObjectOfType<AIAssistantRuntimeUI>();
            }
            
            if (aiAssistant == null)
            {
                Debug.LogWarning("⚠️ 未找到 AIAssistantRuntimeUI 组件，请添加到场景中");
                return;
            }
            
            var uiDocument = aiAssistant.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("❌ AIAssistantRuntimeUI 缺少 UIDocument 组件");
                return;
            }
            
            if (uiDocument.panelSettings == null)
            {
                Debug.LogWarning("⚠️ UIDocument 缺少 PanelSettings");
            }
            
            Debug.Log("✅ AI助手设置验证完成");
        }
        
        /// <summary>
        /// 切换AI助手显示状态
        /// </summary>
        public void ToggleAIAssistant()
        {
            if (aiAssistant != null)
            {
                aiAssistant.ToggleVisible();
            }
        }
        
        /// <summary>
        /// 显示AI助手
        /// </summary>
        public void ShowAIAssistant()
        {
            aiAssistant?.SetVisible(true);
        }
        
        /// <summary>
        /// 隐藏AI助手
        /// </summary>
        public void HideAIAssistant()
        {
            aiAssistant?.SetVisible(false);
        }
        
        /// <summary>
        /// 发送测试消息（用于验证功能）
        /// </summary>
        [ContextMenu("发送测试消息")]
        public void SendTestMessage()
        {
            if (aiAssistant != null)
            {
                aiAssistant.AddResponse("这是一条测试消息，AI助手功能正常！");
                aiAssistant.SetStatus("✓ 测试完成");
                Debug.Log("✅ 测试消息已发送");
            }
            else
            {
                Debug.LogError("❌ 未找到 AIAssistantRuntimeUI 组件");
            }
        }
    }
}
