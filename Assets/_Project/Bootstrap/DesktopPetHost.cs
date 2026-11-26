using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Xiaosu.Platform;

namespace Xiaosu.Bootstrap
{
    /// <summary>
    /// Unity-side host for the FUMI-45 desktop pet overlay. Keeps all UnityEngine usage
    /// out of Xiaosu.Platform; the Platform layer owns the native window and tracking.
    /// Uses a placeholder disc so window behavior can be verified before the real avatar.
    /// </summary>
    public sealed class DesktopPetHost : MonoBehaviour
    {
        private const int WindowSizeDip = 200;
        private const float DiscRadiusWorld = 0.25f;
        private const float FlashSeconds = 0.18f;
        private const float HopSeconds = 0.14f;
        private const float HopHeightWorld = 0.02f;

        private IDesktopWindowOverlay _overlay;
        private Camera _camera;
        private MeshRenderer _renderer;
        private Material _material;
        private Color _baseColor;
        private Vector3 _baseLocalPosition;
        private float _flashTimer;
        private float _hopTimer;
        private bool _disposed;

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            CreateCamera();
            CreatePlaceholderDisc();
        }

        private void Start()
        {
            StartCoroutine(InitializeAfterWindowSettles());
        }

        private IEnumerator InitializeAfterWindowSettles()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            int pixels = DesktopWindowMetrics.DipToPixels(WindowSizeDip);
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(pixels, pixels, false);
            yield return null;
            yield return null;
#else
            yield return null;
#endif

            var settings = new DesktopWindowSettings
            {
                WindowSizeDip = WindowSizeDip,
                InitialMarginDip = 40,
                HitRadiusRatio = 0.25f,
                AttachToleranceDip = 24,
                Log = Debug.Log
            };

            _overlay = DesktopWindowFactory.Create(!Application.isEditor);
            _overlay.StateChanged += OnWindowStateChanged;
            _overlay.Initialize(settings);

            if (_overlay.IsInitialized)
            {
                _overlay.SetAlwaysOnTop(true);
                _overlay.SetClickThrough(true);
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (_overlay != null) _overlay.Tick(deltaTime);
            TickPlaceholder(deltaTime);
        }

        private void OnWindowStateChanged(DesktopWindowEvent windowEvent)
        {
            switch (windowEvent.Type)
            {
                case DesktopWindowEventType.Clicked:
                    Flash();
                    break;

                case DesktopWindowEventType.QuitRequested:
                    Application.Quit();
                    break;

                case DesktopWindowEventType.Error:
                    Debug.LogWarning("[Xiaosu.FUMI-45] " + windowEvent.Message);
                    break;

                default:
                    Debug.Log("[Xiaosu.FUMI-45] " + windowEvent.Type + ": " + windowEvent.Message);
                    break;
            }
        }

        private void CreateCamera()
        {
            var go = new GameObject("PetCamera");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -1f);
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 0.5f; // window height = 1 world unit
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 10f;
            go.tag = "MainCamera";
        }

        private void CreatePlaceholderDisc()
        {
            var go = new GameObject("PetPlaceholder");
            go.transform.SetParent(transform, false);
            _baseLocalPosition = go.transform.localPosition;

            var filter = go.AddComponent<MeshFilter>();
            filter.mesh = BuildDiscMesh(DiscRadiusWorld, 96);

            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            Material template = Resources.Load<Material>("DesktopPetPlaceholder");
            if (template != null)
            {
                _material = new Material(template);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _material = new Material(shader);
            }

            _baseColor = new Color(1f, 0.478f, 0.102f, 1f);
            _material.SetColor("_BaseColor", _baseColor);
            _material.SetColor("_Color", _baseColor);
            _material.SetFloat("_Surface", 1f);   // transparent
            _material.SetFloat("_Blend", 0f);     // alpha
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_ZWrite", 0f);
            _material.SetFloat("_Cull", (float)CullMode.Off);
            _material.SetFloat("_AlphaClip", 0f);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.DisableKeyword("_ALPHATEST_ON");
            _material.renderQueue = (int)RenderQueue.Transparent;
            _renderer.material = _material;
        }

        private void TickPlaceholder(float deltaTime)
        {
            if (_material == null) return;

            if (_flashTimer > 0f)
            {
                _flashTimer -= deltaTime;
                float t = Mathf.Clamp01(_flashTimer / FlashSeconds);
                Color color = Color.Lerp(_baseColor, new Color(1f, 0.8f, 0.47f, 1f), t);
                _material.SetColor("_BaseColor", color);
                _material.SetColor("_Color", color);
            }
            else
            {
                _material.SetColor("_BaseColor", _baseColor);
                _material.SetColor("_Color", _baseColor);
            }

            var visual = _renderer != null ? _renderer.transform : null;
            if (visual == null) return;

            if (_hopTimer > 0f)
            {
                _hopTimer -= deltaTime;
                float t = 1f - Mathf.Clamp01(_hopTimer / HopSeconds);
                float y = Mathf.Sin(Mathf.PI * t) * HopHeightWorld;
                visual.localPosition = _baseLocalPosition + new Vector3(0f, y, 0f);
            }
            else
            {
                visual.localPosition = _baseLocalPosition;
            }
        }

        private void Flash()
        {
            _flashTimer = FlashSeconds;
            _hopTimer = HopSeconds;
        }

        private void OnDestroy()
        {
            if (_disposed) return;
            _disposed = true;

            if (_overlay != null)
            {
                _overlay.StateChanged -= OnWindowStateChanged;
                _overlay.Dispose();
                _overlay = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }

        private static Mesh BuildDiscMesh(float radius, int segments)
        {
            var vertices = new Vector3[segments + 2];
            var colors = new Color32[segments + 2];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            colors[0] = new Color32(255, 255, 255, 255);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                colors[i + 1] = new Color32(255, 255, 255, 255);
            }

            for (int i = 0; i < segments; i++)
            {
                int index = i * 3;
                triangles[index] = 0;
                triangles[index + 1] = i + 1;
                triangles[index + 2] = i + 2;
            }

            var mesh = new Mesh { name = "DesktopPetPlaceholder" };
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
