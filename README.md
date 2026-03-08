# UniUIChat

基于 **UIToolkit** 的 AI 助手组件，实现**编辑器与运行时 UI 复用**，支持流式响应。

## 📖 项目简介

本项目探索 **UIToolkit 的 UI 复用架构**：使用同一套 UXML/USS，让编辑器和运行时共享 UI 界面。

以 AI 助手为示例，展示如何：
- 编辑器和运行时复用同一套 UI
- 集成大模型 API 并支持流式响应
- 实现可中断的对话生成

## ✨ 特性

- 🎨 **UI 复用** - 编辑器/运行时共享界面
- 🚀 **流式响应** - 打字机效果实时输出
- ⏹ **可中断** - 随时停止生成
- 📤 **导出对话** - Markdown 格式

## 🚀 快速开始

### 环境要求
- Unity **2021.3+**
- 无额外依赖

### 安装
克隆项目或复制 `Assets/` 到你的项目

### 配置 API
1. 菜单 `Tools > AI Assistant > Create Default AI Config`
2. 在 Inspector 中填写：
   - **API Key**：你的密钥
   - **Base URL**：API 地址（如 `https://api.openai.com/v1`）
   - **Model Name**：模型名（如 `gpt-3.5-turbo`）

### 使用方式

**编辑器中使用：**
- 菜单 `Window > AI Assistant`
- 快捷键 `Ctrl+Shift+A`

**运行时使用：**
1. 菜单 `Tools > AI Assistant > UI Setup Wizard`
2. 点击"开始设置 UI"
3. 运行场景，按 `F1` 切换界面

## 📁 项目结构

```
Assets/
├── Editor/                    # 编辑器代码
│   ├── EditorUI/              # 编辑器窗口
│   └── Utils/                 # 配置创建、设置向导
├── Scripts/
│   ├── AI/                    # AI 服务接口和工厂
│   ├── Service/               # 服务实现
│   ├── UI/                    # UI 控制器
│   └── Demo/                  # 演示组件
└── Resources/
    ├── AIConfig.asset         # AI 配置文件
    └── UI/                    # UXML/USS 资源
```

## 🎨 自定义样式

修改 `Resources/UI/Styles/CommonStyles.uss` 即可全局生效。

---

## 🔌 扩展说明

### 当前支持

项目当前只实现了 **OpenAI 兼容接口**（`OpenAIService.cs`）。

如果你的模型 API 兼容 OpenAI 格式（如 Azure OpenAI、Ollama、vLLM），只需修改 `AIConfig.asset` 的 Base URL 即可使用。

### 接入其他大模型

如需接入不兼容 OpenAI 格式的模型（如文心一言等），需要：

1. **新建服务类**
   - 位置：`Assets/Scripts/Service/`
   - 实现 `ILLMService` 接口（定义在 `Assets/Scripts/AI/ILLMService.cs`）
   - 需实现的成员：`ModelName`、`SupportsStreaming`、`ChatAsync()`、`ChatStreamAsync()`
   - 参考：`OpenAIService.cs`

2. **注册到工厂**
   - 修改 `Assets/Scripts/AI/LLMServiceFactory.cs` 的 `Create()` 方法
   - 添加判断逻辑，返回你的服务实例

3. **配置使用**
   - 在 `AIConfig.asset` 中填写新模型的 API 地址和密钥

## 📄 许可证

[MIT License](LICENSE)
