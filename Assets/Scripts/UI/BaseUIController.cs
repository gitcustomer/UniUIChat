using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace UIReuse.Core
{
    /// <summary>
    /// UI 复用的核心控制器基类
    /// 编辑器和运行时都继承此类来实现 UI 逻辑复用
    /// </summary>
    public abstract class BaseUIController
    {
        protected VisualElement rootElement;
        protected TextField promptInput;
        protected Button generateButton;
        protected Button clearButton;
        protected Button settingsButton;
        protected Button sendButton;
        protected Button attachButton;
        protected Button clearChatButton;
        protected Button exportButton;
        protected Button minimizeButton;
        protected Button closeButton;
        protected Button stopButton;
        protected VisualElement responseContainer;
        protected Label statusLabel;
        protected Label modelLabel;
        
        public event Action<string> OnPromptSubmitted;
        public event Action OnClearRequested;
        public event Action OnSettingsRequested;
        public event Action OnStopRequested;
        public event Action OnCloseRequested;
        public event Action OnExportRequested;
        
        #region 初始化与事件绑定
        
        /// <summary>
        /// 初始化 UI 控制器，获取 UI 元素引用并绑定事件
        /// </summary>
        public virtual void Initialize(VisualElement root)
        {
            rootElement = root;
            
            // 获取 UI 元素引用
            promptInput = root.Q<TextField>("prompt-input");
            sendButton = root.Q<Button>("send-button");
            attachButton = root.Q<Button>("attach-button");
            clearChatButton = root.Q<Button>("clear-chat-button");
            exportButton = root.Q<Button>("export-button");
            settingsButton = root.Q<Button>("settings-button");
            minimizeButton = root.Q<Button>("minimize-button");
            closeButton = root.Q<Button>("close-button");
            stopButton = root.Q<Button>("stop-button");
            responseContainer = root.Q<VisualElement>("messages-container");
            statusLabel = root.Q<Label>("status-label");
            modelLabel = root.Q<Label>("model-label");
            
            // 兼容旧的按钮名称
            generateButton = sendButton;
            clearButton = clearChatButton;
            
            BindEvents();
            SetStatus("就绪");
        }
        
        /// <summary>
        /// 绑定 UI 事件
        /// </summary>
        protected virtual void BindEvents()
        {
            sendButton?.RegisterCallback<ClickEvent>(evt => OnGenerateClicked());
            if (generateButton != null && generateButton != sendButton)
                generateButton.RegisterCallback<ClickEvent>(evt => OnGenerateClicked());
            
            clearChatButton?.RegisterCallback<ClickEvent>(evt => OnClearClicked());
            if (clearButton != null && clearButton != clearChatButton)
                clearButton.RegisterCallback<ClickEvent>(evt => OnClearClicked());
            
            settingsButton?.RegisterCallback<ClickEvent>(evt => OnSettingsClicked());
            exportButton?.RegisterCallback<ClickEvent>(evt => OnExportClicked());
            minimizeButton?.RegisterCallback<ClickEvent>(evt => OnMinimizeClicked());
            closeButton?.RegisterCallback<ClickEvent>(evt => OnCloseClicked());
            
            if (stopButton != null)
            {
                stopButton.RegisterCallback<ClickEvent>(evt => OnStopClicked());
                stopButton.SetEnabled(false);
            }
            
            promptInput?.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }
        
        protected virtual void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                OnGenerateClicked();
                evt.StopPropagation();
            }
        }
        
        protected virtual void OnGenerateClicked()
        {
            string prompt = promptInput?.value?.Trim();
            if (!string.IsNullOrEmpty(prompt))
            {
                AddUserMessage(prompt);
                if (promptInput != null) promptInput.value = string.Empty;
                OnPromptSubmitted?.Invoke(prompt);
            }
        }
        
        protected virtual void OnClearClicked() => OnClearRequested?.Invoke();
        protected virtual void OnSettingsClicked() => OnSettingsRequested?.Invoke();
        protected virtual void OnExportClicked() => OnExportRequested?.Invoke();
        protected virtual void OnMinimizeClicked() { }
        protected virtual void OnCloseClicked() => OnCloseRequested?.Invoke();
        protected virtual void OnStopClicked() => OnStopRequested?.Invoke();
        
        #endregion
        
        #region 公共 API
        
        public virtual void SetStopButtonEnabled(bool enabled) => stopButton?.SetEnabled(enabled);
        
        public virtual void SetStatus(string status)
        {
            if (statusLabel != null) statusLabel.text = status;
        }
        
        public virtual void SetModel(string modelName)
        {
            if (modelLabel != null) modelLabel.text = $"模型：{modelName}";
        }
        
        public virtual void SetUIEnabled(bool enabled)
        {
            generateButton?.SetEnabled(enabled);
            promptInput?.SetEnabled(enabled);
            
            if (enabled)
                rootElement?.RemoveFromClassList("loading");
            else
                rootElement?.AddToClassList("loading");
        }
        
        public virtual void ClearAll()
        {
            if (promptInput != null) promptInput.value = string.Empty;
            responseContainer?.Clear();
        }
        
        public virtual void AddResponse(string response, bool isError = false)
        {
            if (responseContainer == null) return;
            
            var messageElement = CreateMessageBubble(isAI: true, isError: isError);
            var messageLabel = messageElement.Q<Label>(className: "message-text");
            if (messageLabel != null) messageLabel.text = response;
            
            responseContainer.Add(messageElement);
            ScrollToBottom();
        }
        
        public virtual void AddUserMessage(string message)
        {
            if (responseContainer == null) return;
            
            var messageElement = CreateMessageBubble(isAI: false);
            var messageLabel = messageElement.Q<Label>(className: "message-text");
            if (messageLabel != null) messageLabel.text = message;
            
            responseContainer.Add(messageElement);
            ScrollToBottom();
        }
        
        public virtual VisualElement CreateMessageBubble(bool isAI, bool isError = false)
        {
            var messageElement = new VisualElement();
            messageElement.AddToClassList("message-bubble");
            
            if (isAI)
            {
                messageElement.AddToClassList("ai-message");
                if (isError) messageElement.AddToClassList("error-message");
            }
            else
            {
                messageElement.AddToClassList("user-message");
            }
            
            var messageLabel = new Label();
            messageLabel.AddToClassList("message-text");
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageElement.Add(messageLabel);
            
            responseContainer?.Add(messageElement);
            return messageElement;
        }
        
        public void ScrollToBottomPublic() => ScrollToBottom();
        
        protected virtual void ScrollToBottom()
        {
            var scrollView = rootElement?.Q<ScrollView>("chat-scroll");
            if (scrollView == null) return;
            
            ScheduleDelayed(() => DoScrollToBottom(scrollView), 20);
            ScheduleDelayed(() => DoScrollToBottom(scrollView), 100);
            ScheduleDelayed(() => DoScrollToBottom(scrollView), 250);
        }
        
        private void DoScrollToBottom(ScrollView scrollView)
        {
            if (scrollView == null) return;
            
            try
            {
                scrollView.MarkDirtyRepaint();
                
                if (scrollView.verticalScroller != null)
                {
                    float maxValue = scrollView.verticalScroller.highValue;
                    if (maxValue > 0) scrollView.verticalScroller.value = maxValue;
                }
                
                if (scrollView.contentContainer != null && scrollView.contentViewport != null)
                {
                    float contentHeight = scrollView.contentContainer.layout.height;
                    float viewportHeight = scrollView.contentViewport.layout.height;
                    
                    if (contentHeight > viewportHeight && contentHeight > 0)
                    {
                        scrollView.scrollOffset = new Vector2(0, contentHeight - viewportHeight + 100);
                    }
                }
            }
            catch (Exception) { }
        }
        
        #endregion
        
        protected abstract void ScheduleDelayed(Action action, long delayMs);
        
        public virtual void Dispose()
        {
            promptInput?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }
    }
}
