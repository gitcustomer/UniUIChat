using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UIReuse.Runtime;
using UIReuse.AI;

namespace UIReuse.Editor
{
    /// <summary>
    /// UI设置向导 - 帮助用户快速在场景中设置UIToolkit运行时UI
    /// </summary>
    public class UISetupWizard : EditorWindow
    {
        private GameObject selectedGameObject;
        private bool autoCreateGameObject = true;
        private string gameObjectName = "AIAssistantUI";
        
        [MenuItem("Tools/AI Assistant/UI Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<UISetupWizard>();
            window.titleContent = new GUIContent("UI Setup Wizard");
            window.minSize = new Vector2(400, 500);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UIToolkit运行时UI设置向导", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "此向导将帮助您在场景中快速设置AIToolkit运行时UI。\n" +
                "设置包括：UIDocument、PanelSettings、VisualTreeAsset和AI组件。", 
                MessageType.Info
            );
            
            EditorGUILayout.Space();
            
            // GameObject选择
            EditorGUILayout.LabelField("1. GameObject设置", EditorStyles.boldLabel);
            autoCreateGameObject = EditorGUILayout.Toggle("自动创建GameObject", autoCreateGameObject);
            
            if (autoCreateGameObject)
            {
                gameObjectName = EditorGUILayout.TextField("GameObject名称", gameObjectName);
            }
            else
            {
                selectedGameObject = (GameObject)EditorGUILayout.ObjectField(
                    "选择GameObject", 
                    selectedGameObject, 
                    typeof(GameObject), 
                    true
                );
            }
            
            EditorGUILayout.Space();
            
            // 资源检查
            EditorGUILayout.LabelField("2. 资源检查", EditorStyles.boldLabel);
            CheckResources();
            
            EditorGUILayout.Space();
            
            // 设置按钮
            EditorGUILayout.LabelField("3. 执行设置", EditorStyles.boldLabel);
            
            GUI.enabled = CanSetup();
            if (GUILayout.Button("开始设置UI", GUILayout.Height(30)))
            {
                SetupUI();
            }
            GUI.enabled = true;
            
            EditorGUILayout.Space();
            
            // 快速操作
            EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);
            
            if (GUILayout.Button("创建AI配置文件"))
            {
                AIConfigCreator.CreateDefaultAIConfig();
            }
            
            if (GUILayout.Button("创建PanelSettings"))
            {
                CreatePanelSettings();
            }
            
            if (GUILayout.Button("打开示例场景"))
            {
                CreateExampleScene();
            }
        }
        
        private void CheckResources()
        {
            // 检查UXML文件
            var uxml = Resources.Load<VisualTreeAsset>("UI/Components/AIAssistantUI");
            DrawResourceStatus("UXML文件", uxml != null, "UI/Components/AIAssistantUI");
            
            // 检查USS文件
            var uss = Resources.Load<StyleSheet>("UI/Styles/CommonStyles");
            DrawResourceStatus("USS样式", uss != null, "UI/Styles/CommonStyles");
            
            // 检查AI配置
            var aiConfig = Resources.Load<AIConfig>("AIConfig");
            DrawResourceStatus("AI配置", aiConfig != null, "AIConfig");
            
            // 检查PanelSettings
            var panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings_RuntimeUI");
            DrawResourceStatus("PanelSettings", panelSettings != null, "UI/PanelSettings_RuntimeUI");
        }
        
        private void DrawResourceStatus(string name, bool exists, string path)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (exists)
            {
                EditorGUILayout.LabelField("✅ " + name, GUILayout.Width(150));
            }
            else
            {
                EditorGUILayout.LabelField("❌ " + name, GUILayout.Width(150));
            }
            
            EditorGUILayout.LabelField($"Resources/{path}", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        private bool CanSetup()
        {
            if (autoCreateGameObject)
            {
                return !string.IsNullOrEmpty(gameObjectName);
            }
            else
            {
                return selectedGameObject != null;
            }
        }
        
        private void SetupUI()
        {
            GameObject targetObject;
            
            // 获取或创建GameObject
            if (autoCreateGameObject)
            {
                targetObject = new GameObject(gameObjectName);
                Undo.RegisterCreatedObjectUndo(targetObject, "Create AI Assistant UI");
            }
            else
            {
                targetObject = selectedGameObject;
            }
            
            // 添加UIDocument组件
            var uiDocument = targetObject.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = Undo.AddComponent<UIDocument>(targetObject);
            }
            
            // 设置PanelSettings
            var panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings_RuntimeUI");
            if (panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
            }
            else
            {
                Debug.LogWarning("未找到PanelSettings，请手动设置");
            }
            
            // 设置VisualTreeAsset
            var visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/Components/AIAssistantUI");
            if (visualTreeAsset != null)
            {
                uiDocument.visualTreeAsset = visualTreeAsset;
            }
            else
            {
                Debug.LogError("未找到VisualTreeAsset，请先创建UI资源");
                return;
            }
            
            // 添加AI组件
            if (targetObject.GetComponent<AIAssistantRuntimeUI>() == null)
            {
                Undo.AddComponent<AIAssistantRuntimeUI>(targetObject);
            }
            
            // 选中创建的对象
            Selection.activeGameObject = targetObject;
            
            Debug.Log($"✅ UI设置完成！GameObject: {targetObject.name}");
            
            EditorUtility.DisplayDialog(
                "设置完成", 
                $"AI助手UI已成功设置在GameObject: {targetObject.name}\n\n" +
                "请检查组件配置，并确保AI配置文件已正确设置。", 
                "确定"
            );
        }
        
        private void CreatePanelSettings()
        {
            var panelSettings = CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            
            string path = "Assets/Resources/UI/PanelSettings_RuntimeUI.asset";
            
            // 确保目录存在
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
            }
            
            AssetDatabase.CreateAsset(panelSettings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Selection.activeObject = panelSettings;
            Debug.Log($"✅ 已创建PanelSettings: {path}");
        }
        
        private void CreateExampleScene()
        {
            // 创建新场景
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, 
                UnityEditor.SceneManagement.NewSceneMode.Single
            );
            
            // 创建AI助手UI
            gameObjectName = "AIAssistantUI";
            autoCreateGameObject = true;
            SetupUI();
            
            // 保存场景
            string scenePath = "Assets/Scenes/AIAssistantDemo.unity";
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            
            Debug.Log($"✅ 已创建示例场景: {scenePath}");
        }
    }
}