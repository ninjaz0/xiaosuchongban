using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Xiaosu.Bootstrap;

namespace Xiaosu.Platform.Editor
{
    /// <summary>
    /// Builds the FUMI-45 DesktopPetDemo scene with the Windows transparency settings
    /// (D3D11 only, BitBlt model) without changing the BaselineDemo/FUMI-42 build defaults.
    /// </summary>
    public static class DesktopPetBuild
    {
        private const string ScenePath = "Assets/_Project/Scenes/DesktopPetDemo.unity";
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string MaterialFolder = "Assets/_Project/Bootstrap/Resources";
        private const string MaterialPath = MaterialFolder + "/DesktopPetPlaceholder.mat";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        [MenuItem("Xiaosu/FUMI-45/Create Desktop Pet Demo Scene")]
        public static void CreateDemoScene()
        {
            EnsureDemoScene(true);
        }

        [MenuItem("Xiaosu/FUMI-45/Build Windows Desktop Pet")]
        public static void BuildWindows()
        {
            EnsureDemoScene(false);
            EnsurePlaceholderMaterial();

            bool oldDefaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            GraphicsDeviceType[] oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            bool oldFlipModel = PlayerSettings.useFlipModelSwapchain;
            FullScreenMode oldFullScreenMode = PlayerSettings.fullScreenMode;
            int oldWidth = PlayerSettings.defaultScreenWidth;
            int oldHeight = PlayerSettings.defaultScreenHeight;
            bool oldRunInBackground = PlayerSettings.runInBackground;
            bool oldVisibleInBackground = PlayerSettings.visibleInBackground;
            bool oldResizable = PlayerSettings.resizableWindow;
            bool oldAllowFullscreenSwitch = PlayerSettings.allowFullscreenSwitch;
            bool oldForceSingleInstance = PlayerSettings.forceSingleInstance;
            ScriptingImplementation oldBackend =
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);

            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(
                    BuildTarget.StandaloneWindows64,
                    new[] { GraphicsDeviceType.Direct3D11 });
                PlayerSettings.useFlipModelSwapchain = false;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 200;
                PlayerSettings.defaultScreenHeight = 200;
                PlayerSettings.runInBackground = true;
                PlayerSettings.visibleInBackground = true;
                PlayerSettings.resizableWindow = false;
                PlayerSettings.allowFullscreenSwitch = false;
                PlayerSettings.forceSingleInstance = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string outputDir = Path.Combine(projectRoot, "Build", "Win64");
                Directory.CreateDirectory(outputDir);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(outputDir, "XiaosuDesktopPet.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log(string.Format(
                    "[Xiaosu.FUMI-45] Build result={0} size={1} errors={2} output={3}",
                    summary.result, summary.totalSize, summary.totalErrors, summary.outputPath));

                if (summary.result != BuildResult.Succeeded)
                {
                    EditorApplication.Exit(1);
                }
            }
            finally
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, oldDefaultApis);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, oldApis);
                PlayerSettings.useFlipModelSwapchain = oldFlipModel;
                PlayerSettings.fullScreenMode = oldFullScreenMode;
                PlayerSettings.defaultScreenWidth = oldWidth;
                PlayerSettings.defaultScreenHeight = oldHeight;
                PlayerSettings.runInBackground = oldRunInBackground;
                PlayerSettings.visibleInBackground = oldVisibleInBackground;
                PlayerSettings.resizableWindow = oldResizable;
                PlayerSettings.allowFullscreenSwitch = oldAllowFullscreenSwitch;
                PlayerSettings.forceSingleInstance = oldForceSingleInstance;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, oldBackend);
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureDemoScene(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                {
                    AssetDatabase.CreateFolder("Assets", "_Project");
                }
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("DesktopPetHost");
            host.AddComponent<DesktopPetHost>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Xiaosu.FUMI-45] Created " + ScenePath);
        }

        private static void EnsurePlaceholderMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Bootstrap"))
                {
                    Debug.LogWarning("[Xiaosu.FUMI-45] Bootstrap folder missing; cannot create placeholder material.");
                    return;
                }
                AssetDatabase.CreateFolder("Assets/_Project/Bootstrap", "Resources");
            }

            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null)
            {
                Debug.LogWarning("[Xiaosu.FUMI-45] URP Unlit shader not found; placeholder will use a fallback.");
                return;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0.478f, 0.102f, 1f));
            material.SetColor("_Color", new Color(1f, 0.478f, 0.102f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Xiaosu.FUMI-45] Created " + MaterialPath);
        }
    }
}
