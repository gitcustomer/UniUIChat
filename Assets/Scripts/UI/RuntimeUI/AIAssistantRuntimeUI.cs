using UnityEngine;
using UnityEngine.UIElements;
using UIReuse.Core;
using UIReuse.AI;
using UIReuse.Utils;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace UIReuse.Runtime
{
    /// <summary>
    /// 运行时 UI 控制器 - 实现延迟调度
    /// </summary>
    public class RuntimeUIController : BaseUIController
    {
        protected override void ScheduleDelayed(Action action, long delayMs)
        {
            _ = DelayedActionAsync(action, delayMs);
        }
        
        private async Task DelayedActionAsync(Action action, long delayMs)
        {
            await Task.Delay((int)delayMs);
            action?.Invoke();
        }
    }

    /// <summary>
    /// AI 助手运行时 UI 组件 - 集成 UI 资源加载和 AI 服务
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AIAssistantRuntimeUI : MonoBehaviour, IDisposable
    {
        [Header("UI 配置")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool showOnStart = true;
        
        [Header("UI 资源（可选，留空则自动加载）")]
        [SerializeField] private VisualTreeAsset uiTemplate;
        [SerializeField] private StyleSheet commonStyles;
        
        [Header("AI 配置")]
        [SerializeField] private AIConfig aiConfig;
        
        [Header("响应设置")]
        [Tooltip("启用流式响应（需要 API 支持）")]
        [SerializeField] private bool useStreamingResponse = true;
        
        private UIDocument uiDocument;
        private ILLMService llmService;
        private bool isDisposed = false;
        private bool isGenerating = false;
        private CancellationTokenSource cancellationTokenSource;
        private RuntimeUIController uiController;
        
        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            uiController = new RuntimeUIController();
            InitializeAIService();
        }
        
        private void Start()
        {
            if (autoInitialize) InitializeUI();
            if (!showOnStart) SetVisible(false);
        }
        
        private void OnDestroy() => Dispose();
        
        #region 初始化
        
        private void InitializeAIService()
        {
            if (aiConfig != null && aiConfig.IsValid())
            {
                llmService = LLMServiceFactory.Create(aiConfig);
                Debug.Log("已使用 Inspector 中的 AI 配置");
                return;
            }
            
            var defaultConfig = AIConfigUtility.LoadDefaultConfig();
            if (AIConfigUtility.ValidateConfig(defaultConfig, false))
            {
                aiConfig = defaultConfig;
                llmService = LLMServiceFactory.Create(aiConfig);
                Debug.Log("已自动加载默认 AI 配置");
                return;
            }
            
            Debug.LogWarning("AI 配置无效，AI 功能将不可用");
        }
        
        private VisualElement CreateUIFromResources()
        {
            if (uiTemplate == null)
                uiTemplate = Resources.Load<VisualTreeAsset>("UI/Components/AIAssistantUI");
            
            if (uiTemplate == null)
            {
                Debug.LogError("❌ 无法加载 UI 模板");
                return null;
            }
            
            var root = uiTemplate.CloneTree();
            
            if (commonStyles == null)
                commonStyles = Resources.Load<StyleSheet>("UI/Styles/CommonStyles");
            
            if (commonStyles != null)
                root.styleSheets.Add(commonStyles);
            
            return root;
        }
        
        public void InitializeUI()
        {
            if (uiDocument == null)
            {
                Debug.LogError("❌ UIDocument 组件未找到");
                return;
            }
            
            var root = CreateUIFromResources();
            if (root == null) return;
            
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.style.flexGrow = 1;
            uiDocument.rootVisualElement.style.width = Length.Percent(100);
            uiDocument.rootVisualElement.style.height = Length.Percent(100);
            uiDocument.rootVisualElement.Add(root);
            
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            
            uiController.Initialize(root);
            uiController.OnPromptSubmitted += HandlePromptSubmitted;
            uiController.OnClearRequested += HandleClearRequested;
            uiController.OnSettingsRequested += HandleSettingsRequested;
            uiController.OnStopRequested += HandleStopRequested;
            uiController.OnCloseRequested += HandleCloseRequested;
            uiController.OnExportRequested += HandleExportRequested;
            uiController.SetModel(llmService?.ModelName ?? "未配置");
        }
        
        private void HandlePromptSubmitted(string prompt)
        {
            if (isDisposed) return;
            StartCoroutine(GenerateResponseCoroutine(prompt));
        }
        
        private void HandleClearRequested()
        {
            uiController?.ClearAll();
            uiController?.SetStatus("✓ 已清空");
        }
        
        private void HandleSettingsRequested()
        {
            Debug.Log("⚙️ 运行时设置请求");
            uiController?.SetStatus("设置功能开发中...");
        }
        
        private void HandleStopRequested()
        {
            if (isGenerating && cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                SetStatus("⏹ 正在停止...");
            }
        }
        
        private void HandleCloseRequested()
        {
            SetVisible(false);
            Debug.Log("🔲 AI 助手窗口已关闭");
        }
        
        private void HandleExportRequested() => ExportChatHistory();
        
        #endregion
        
        #region AI 响应生成与导出
        
        private IEnumerator GenerateResponseCoroutine(string prompt)
        {
            isGenerating = true;
            cancellationTokenSource = new CancellationTokenSource();
            SetStatus("正在生成...");
            SetUIEnabled(false);
            uiController?.SetStopButtonEnabled(true);
            
            if (llmService == null)
            {
                AddResponse("❌ AI 服务未配置，请检查 AI 配置", true);
                ResetGeneratingState();
                yield break;
            }
            
            if (useStreamingResponse && llmService.SupportsStreaming)
                yield return StartCoroutine(StreamingResponseCoroutine(prompt));
            else
                yield return StartCoroutine(NormalResponseCoroutine(prompt));
            
            ResetGeneratingState();
        }
        
        private void ResetGeneratingState()
        {
            isGenerating = false;
            uiController?.SetStopButtonEnabled(false);
            SetUIEnabled(true);
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
        
        private IEnumerator StreamingResponseCoroutine(string prompt)
        {
            var messageElement = uiController.CreateMessageBubble(isAI: true);
            var messageLabel = messageElement?.Q<Label>(className: "message-text");
            
            if (messageLabel == null)
            {
                yield return StartCoroutine(NormalResponseCoroutine(prompt));
                yield break;
            }
            
            var fullText = new System.Text.StringBuilder();
            bool isCompleted = false;
            bool hasError = false;
            string errorMessage = null;
            var token = cancellationTokenSource?.Token ?? CancellationToken.None;
            
            llmService.ChatStreamAsync(
                prompt,
                onChunkReceived: (chunk) => { fullText.Append(chunk); messageLabel.text = fullText.ToString(); },
                token,
                onComplete: (text) => { isCompleted = true; },
                onError: (ex) => { hasError = true; errorMessage = ex.Message; isCompleted = true; }
            );
            
            while (!isCompleted)
            {
                if (token.IsCancellationRequested)
                {
                    SetStatus("⏹ 已停止");
                    uiController.ScrollToBottomPublic();
                    yield break;
                }
                uiController.ScrollToBottomPublic();
                yield return new WaitForSeconds(0.1f);
            }
            
            if (hasError)
            {
                messageElement.AddToClassList("error-message");
                messageLabel.text = $"❌ 生成失败：{errorMessage}";
                SetStatus("生成失败");
            }
            else
            {
                SetStatus("✓ 生成完成");
            }
            
            uiController.ScrollToBottomPublic();
        }
        
        private IEnumerator NormalResponseCoroutine(string prompt)
        {
            Task<string> task = null;
            try { task = llmService.ChatAsync(prompt); }
            catch (Exception ex)
            {
                AddResponse($"❌ 生成失败：{ex.Message}", true);
                SetStatus("生成失败");
                yield break;
            }
            
            while (!task.IsCompleted) yield return null;
            
            if (task.IsCompletedSuccessfully)
            {
                AddResponse(task.Result);
                SetStatus("✓ 生成完成");
            }
            else if (task.IsFaulted)
            {
                AddResponse($"❌ 生成失败：{task.Exception?.GetBaseException().Message}", true);
                SetStatus("生成失败");
            }
        }
        
        private void ExportChatHistory()
        {
            var messagesContainer = uiDocument?.rootVisualElement?.Q<VisualElement>("messages-container");
            if (messagesContainer == null) { SetStatus("❌ 无法导出"); return; }
            
            var messages = messagesContainer.Query<VisualElement>(className: "message-bubble").ToList();
            if (messages.Count == 0) { SetStatus("❌ 无对话可导出"); return; }
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# AI 助手对话记录");
            sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"模型：{llmService?.ModelName ?? "未知"}");
            sb.AppendLine("\n---\n");
            
            foreach (var message in messages)
            {
                var textLabel = message.Q<Label>(className: "message-text");
                if (textLabel == null) continue;
                
                string role = message.ClassListContains("user-message") ? "👤 用户" :
                              message.ClassListContains("ai-message") ? "🤖 AI" :
                              message.ClassListContains("system-message") ? "📢 系统" : "💬 消息";
                
                sb.AppendLine($"### {role}\n\n{textLabel.text}\n");
            }
            
            string fileName = $"chat_export_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            
            try
            {
                System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
                SetStatus($"✓ 已导出到：{fileName}");
                Debug.Log($"📄 对话已导出到：{filePath}");
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.RevealInFinder(filePath);
                #endif
            }
            catch (Exception ex)
            {
                SetStatus($"❌ 导出失败：{ex.Message}");
            }
        }
        
        #endregion
        
        #region 公共 API
        
        public void SetStatus(string status) => uiController?.SetStatus(status);
        public void SetModel(string modelName) => uiController?.SetModel(modelName);
        public void ClearAll() => uiController?.ClearAll();
        public void AddResponse(string response, bool isError = false) => uiController?.AddResponse(response, isError);
        public void AddUserMessage(string message) => uiController?.AddUserMessage(message);
        public void SetUIEnabled(bool enabled) => uiController?.SetUIEnabled(enabled);
        
        public void SetVisible(bool visible)
        {
            if (uiDocument?.rootVisualElement != null)
                uiDocument.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public void ToggleVisible()
        {
            if (uiDocument?.rootVisualElement != null)
            {
                var isVisible = uiDocument.rootVisualElement.style.display == DisplayStyle.Flex;
                SetVisible(!isVisible);
            }
        }
        
        public void SetAIConfig(AIConfig config)
        {
            if (config == null) return;
            aiConfig = config;
            if (config.IsValid())
            {
                llmService = LLMServiceFactory.Create(config);
                uiController?.SetModel(llmService.ModelName);
            }
        }
        
        public void Dispose()
        {
            if (isDisposed) return;
            
            if (uiController != null)
            {
                uiController.OnPromptSubmitted -= HandlePromptSubmitted;
                uiController.OnClearRequested -= HandleClearRequested;
                uiController.OnSettingsRequested -= HandleSettingsRequested;
                uiController.OnStopRequested -= HandleStopRequested;
                uiController.OnCloseRequested -= HandleCloseRequested;
                uiController.OnExportRequested -= HandleExportRequested;
                uiController.Dispose();
                uiController = null;
            }
            
            isDisposed = true;
        }
        
        #endregion
    }
}
