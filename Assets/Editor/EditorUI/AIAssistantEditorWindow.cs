using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UIReuse.Core;
using UIReuse.AI;
using UIReuse.Utils;
using System;
using System.Threading;

namespace UIReuse.Editor
{
    /// <summary>
    /// AI助手编辑器窗口
    /// </summary>
    public class AIAssistantEditorWindow : EditorWindow
    {
        private EditorUIController uiController;
        private ILLMService llmService;
        private AIConfig aiConfig;
        private bool isGenerating = false;
        private CancellationTokenSource cancellationTokenSource;
        
        [MenuItem("Window/AI Assistant %#a")]
        public static void ShowWindow()
        {
            var window = GetWindow<AIAssistantEditorWindow>();
            window.titleContent = new GUIContent("AI Assistant");
            window.minSize = new Vector2(350, 400);
        }
        
        [MenuItem("Tools/AI Assistant/Open Chat Window")]
        public static void ShowWindowFromTools() => ShowWindow();
        
        private void CreateGUI()
        {
            var uiTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Resources/UI/Components/AIAssistantUI.uxml");
            var commonStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Resources/UI/Styles/CommonStyles.uss");
            
            if (uiTemplate == null)
            {
                var errorLabel = new Label("无法加载UI模板，请确保 Assets/Resources/UI/Components/AIAssistantUI.uxml 存在");
                errorLabel.style.color = Color.red;
                errorLabel.style.whiteSpace = WhiteSpace.Normal;
                rootVisualElement.Add(errorLabel);
                return;
            }
            
            var uiRoot = uiTemplate.CloneTree();
            if (commonStyles != null) uiRoot.styleSheets.Add(commonStyles);
            
            rootVisualElement.style.flexGrow = 1;
            uiRoot.style.flexGrow = 1;
            uiRoot.style.height = Length.Percent(100);
            uiRoot.style.width = Length.Percent(100);
            rootVisualElement.Add(uiRoot);
            
            uiController = new EditorUIController();
            uiController.Initialize(uiRoot);
            
            uiController.OnPromptSubmitted += HandlePromptSubmitted;
            uiController.OnClearRequested += HandleClearRequested;
            uiController.OnSettingsRequested += HandleSettingsRequested;
            uiController.OnStopRequested += HandleStopRequested;
            uiController.OnCloseRequested += HandleCloseRequested;
            uiController.OnExportRequested += HandleExportRequested;
            
            InitializeAIService();
        }
        
        private void OnFocus()
        {
            if (uiController != null && aiConfig != null)
            {
                aiConfig = AIConfigUtility.LoadDefaultConfig();
                if (aiConfig != null && aiConfig.IsValid())
                {
                    llmService = LLMServiceFactory.Create(aiConfig);
                    uiController.SetModel(llmService.ModelName);
                }
            }
        }
        
        private void OnDestroy()
        {
            if (uiController != null)
            {
                uiController.OnPromptSubmitted -= HandlePromptSubmitted;
                uiController.OnClearRequested -= HandleClearRequested;
                uiController.OnSettingsRequested -= HandleSettingsRequested;
                uiController.OnStopRequested -= HandleStopRequested;
                uiController.OnCloseRequested -= HandleCloseRequested;
                uiController.OnExportRequested -= HandleExportRequested;
                uiController.Dispose();
            }
        }
        
        private void InitializeAIService()
        {
            aiConfig = AIConfigUtility.LoadDefaultConfig();
            
            if (aiConfig != null && aiConfig.IsValid())
            {
                llmService = LLMServiceFactory.Create(aiConfig);
                uiController?.SetModel(llmService.ModelName);
                Debug.Log($"编辑器已加载 AI 配置：{aiConfig.ModelName}");
            }
            else
            {
                uiController?.SetModel("未配置");
                Debug.LogWarning("AI 配置无效，请在 Resources/AIConfig.asset 中设置 API Key");
            }
        }
        
        private void HandlePromptSubmitted(string prompt)
        {
            if (uiController == null) return;
            
            isGenerating = true;
            cancellationTokenSource = new CancellationTokenSource();
            uiController.SetStatus("正在生成...");
            uiController.SetUIEnabled(false);
            uiController.SetStopButtonEnabled(true);
            
            EditorApplication.delayCall += () => StartAsyncGeneration(prompt, cancellationTokenSource.Token);
        }
        
        private void HandleClearRequested()
        {
            uiController?.ClearAll();
            uiController?.SetStatus("✓ 已清空");
        }
        
        private void HandleSettingsRequested()
        {
            var config = AssetDatabase.LoadAssetAtPath<AIConfig>("Assets/Resources/AIConfig.asset");
            
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
                uiController?.SetStatus("已打开配置文件");
            }
            else if (EditorUtility.DisplayDialog("配置文件不存在", "AIConfig.asset 不存在，是否创建？", "创建", "取消"))
            {
                AIConfigCreator.CreateDefaultAIConfig();
            }
        }
        
        private void HandleStopRequested()
        {
            if (isGenerating && cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                uiController?.SetStatus("⏹ 正在停止...");
            }
        }
        
        private void HandleCloseRequested() => Close();
        
        private void HandleExportRequested() => ExportChatHistory();
        
        #region 响应生成与导出
        
        private async void StartAsyncGeneration(string prompt, CancellationToken token)
        {
            try
            {
                if (llmService == null)
                {
                    uiController.AddResponse("❌ AI 服务未配置，请在 Resources/AIConfig.asset 中设置 API Key", true);
                    uiController.SetStatus("配置缺失");
                    ResetGeneratingState();
                    return;
                }
                
                if (llmService.SupportsStreaming)
                    await GenerateStreamingResponse(prompt, token);
                else
                    await GenerateNormalResponse(prompt);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    uiController.AddResponse($"❌ 生成失败：{ex.Message}", true);
                    uiController.SetStatus("生成失败");
                    Debug.LogError($"AI 生成失败：{ex}");
                }
            }
            finally
            {
                ResetGeneratingState();
            }
        }
        
        private async System.Threading.Tasks.Task GenerateStreamingResponse(string prompt, CancellationToken token)
        {
            var messageElement = uiController.CreateMessageBubble(isAI: true);
            var messageLabel = messageElement?.Q<Label>(className: "message-text");
            
            if (messageLabel == null)
            {
                await GenerateNormalResponse(prompt);
                return;
            }
            
            var fullText = new System.Text.StringBuilder();
            bool hasError = false;
            bool wasCancelled = false;
            
            await llmService.ChatStreamAsync(
                prompt,
                onChunkReceived: (chunk) => { fullText.Append(chunk); messageLabel.text = fullText.ToString(); uiController.ScrollToBottomPublic(); },
                token,
                onComplete: (text) => { wasCancelled = token.IsCancellationRequested; },
                onError: (ex) => { if (!token.IsCancellationRequested) { hasError = true; messageElement.AddToClassList("error-message"); messageLabel.text = $"❌ 生成失败：{ex.Message}"; } }
            );
            
            uiController.SetStatus(wasCancelled ? "⏹ 已停止" : (hasError ? "生成失败" : "✓ 生成完成"));
        }
        
        private async System.Threading.Tasks.Task GenerateNormalResponse(string prompt)
        {
            var response = await llmService.ChatAsync(prompt);
            uiController.AddResponse(response);
            uiController.SetStatus("✓ 生成完成");
        }
        
        private void ResetGeneratingState()
        {
            isGenerating = false;
            uiController?.SetUIEnabled(true);
            uiController?.SetStopButtonEnabled(false);
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
        
        private void ExportChatHistory()
        {
            var messagesContainer = rootVisualElement?.Q<VisualElement>("messages-container");
            if (messagesContainer == null) { uiController?.SetStatus("❌ 无法导出"); return; }
            
            var messages = messagesContainer.Query<VisualElement>(className: "message-bubble").ToList();
            if (messages.Count == 0) { uiController?.SetStatus("❌ 无对话可导出"); return; }
            
            string defaultPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"chat_export_{DateTime.Now:yyyyMMdd_HHmmss}.md"
            );
            
            string filePath = EditorUtility.SaveFilePanel("导出对话记录", 
                System.IO.Path.GetDirectoryName(defaultPath), 
                System.IO.Path.GetFileName(defaultPath), "md");
            
            if (string.IsNullOrEmpty(filePath)) { uiController?.SetStatus("已取消导出"); return; }
            
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
            
            try
            {
                System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
                uiController?.SetStatus("✓ 已导出");
                Debug.Log($"📄 对话已导出到：{filePath}");
                EditorUtility.RevealInFinder(filePath);
            }
            catch (Exception ex)
            {
                uiController?.SetStatus($"❌ 导出失败：{ex.Message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 编辑器 UI 控制器 - 实现延迟调度
    /// </summary>
    public class EditorUIController : BaseUIController
    {
        protected override void ScheduleDelayed(Action action, long delayMs)
        {
            EditorApplication.delayCall += () => action?.Invoke();
        }
    }
}
