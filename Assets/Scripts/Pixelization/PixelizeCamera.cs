using UnityEngine;

/// <summary>
/// Pixeliza todo el escenario EXCEPTO los modelos en highResLayers.
/// Pon este script en tu Main Camera.
/// TODO se crea automáticamente (Pixel Camera, Quad, Material, RenderTexture).
/// </summary>
[RequireComponent(typeof(Camera))]
public class PixelizeCamera : MonoBehaviour
{
    [Header("Layers de modelos que NO se pixelan")]
    [Tooltip("Selecciona el layer de tus modelos hi-res (ej: NormalQuality)")]
    public LayerMask highResLayers;

    [Header("Resolución de pixelación")]
    [Range(64, 480)]
    [Tooltip("Altura en pixeles de la imagen pixelada")]
    public int verticalResolution = 180;

    Camera _mainCam;
    Camera _pixelCam;
    GameObject _quad;
    RenderTexture _rt;
    Material _quadMat;
    int _lastResolution;
    float _lastAspect;

    void Start()
    {
        _mainCam = GetComponent<Camera>();
        _lastResolution = verticalResolution;
        _lastAspect = _mainCam.aspect;

        CreateRenderTexture();
        CreatePixelCamera();
        CreateQuad();
        ConfigureCameras();
    }

    void CreateRenderTexture()
    {
        int h = verticalResolution;
        int w = Mathf.RoundToInt(h * _mainCam.aspect);
        _rt = new RenderTexture(w, h, 24);
        _rt.filterMode = FilterMode.Point;
        _rt.wrapMode = TextureWrapMode.Clamp;
    }

    void CreatePixelCamera()
    {
        var go = new GameObject("_PixelCam_Auto");
        go.transform.SetParent(transform, false);

        _pixelCam = go.AddComponent<Camera>();
        _pixelCam.clearFlags = CameraClearFlags.Skybox;
        _pixelCam.fieldOfView = _mainCam.fieldOfView;
        _pixelCam.nearClipPlane = _mainCam.nearClipPlane;
        _pixelCam.farClipPlane = _mainCam.farClipPlane;
        _pixelCam.targetTexture = _rt;
        _pixelCam.depth = _mainCam.depth - 1;
    }

    void CreateQuad()
    {
        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quad.name = "_PixelQuad_Auto";
        _quad.layer = 31;
        Destroy(_quad.GetComponent<Collider>());
        _quad.transform.SetParent(transform, false);

        _quadMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _quadMat.SetTexture("_BaseMap", _rt);

        var rend = _quad.GetComponent<MeshRenderer>();
        rend.material = _quadMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    void ConfigureCameras()
    {
        int quadBit = 1 << 31;
        int uiBit = 1 << 5;

        _pixelCam.cullingMask = ~(highResLayers.value | quadBit);

        _mainCam.cullingMask = highResLayers.value | quadBit | uiBit;
        _mainCam.clearFlags = CameraClearFlags.Skybox;
    }

    void LateUpdate()
    {
        if (_pixelCam == null || _quad == null) return;

        // Actualizar culling masks cada frame (por si cambian layers en runtime)
        int quadBit = 1 << 31;
        int uiBit = 1 << 5;
        _pixelCam.cullingMask = ~(highResLayers.value | quadBit);
        _mainCam.cullingMask = highResLayers.value | quadBit | uiBit;

        // Recrear RT si cambió la resolución o el aspect ratio
        float currentAspect = _mainCam.aspect;
        if (verticalResolution != _lastResolution || Mathf.Abs(currentAspect - _lastAspect) > 0.01f)
        {
            _lastResolution = verticalResolution;
            _lastAspect = currentAspect;
            _rt.Release();
            int h = verticalResolution;
            int w = Mathf.RoundToInt(h * currentAspect);
            _rt.width = w;
            _rt.height = h;
            _rt.Create();
        }

        // Sincronizar FOV
        _pixelCam.fieldOfView = _mainCam.fieldOfView;

        // Quad cerca del near clip
        float dist = _mainCam.nearClipPlane * 1.1f + 0.05f;
        float halfH = dist * Mathf.Tan(_mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * currentAspect;

        float margin = 1.1f;
        _quad.transform.localPosition = new Vector3(0f, 0f, dist);
        _quad.transform.localScale = new Vector3(halfW * 2f * margin, halfH * 2f * margin, 1f);
    }

    void OnDestroy()
    {
        if (_quad != null) Destroy(_quad);
        if (_pixelCam != null) Destroy(_pixelCam.gameObject);
        if (_rt != null) _rt.Release();
    }
}
