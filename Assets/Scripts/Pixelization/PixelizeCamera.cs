using UnityEngine;

/// <summary>
/// Script basado en el tutorial de pixelación.
/// Va en la cámara PRINCIPAL (la que ve el quad y los modelos 3D).
/// 
/// Setup:
/// - pixelCamera: cámara que renderiza el escenario a la RenderTexture (Old Texture).
///   Su Culling Mask EXCLUYE el layer de los modelos 3D y el layer del Quad.
/// - spriteTransform: el Quad que muestra la RenderTexture pixelada.
/// - La cámara principal (donde va este script): renderiza el Quad + los modelos 3D.
///   Su Culling Mask EXCLUYE las capas del escenario (solo ve Quad + modelos).
/// </summary>
public class PixelizeCamera : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Transform del Quad que muestra la RenderTexture")]
    public Transform spriteTransform;

    [Tooltip("Transform de la cámara que renderiza a baja resolución")]
    public Transform pixelCamera;

    [Header("Configuración del Quad")]
    [Tooltip("Ancho del Quad en unidades de mundo (escala Y del quad si es vertical)")]
    public float spriteWidth = 1f;

    [Header("Frame Stutter (opcional, efecto retro)")]
    public bool frameStutter = false;
    public int frames = 5;

    private int i;

    void Start()
    {
        i = frames + 1;
        Debug.Log(spriteWidth);
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (frameStutter) {
            if (i > frames) {
                GetComponent<Camera>().enabled = true;
                i = 0;
            }
            else {
                GetComponent<Camera>().enabled = false;
            }
            i++;
        }

        spriteTransform.forward = spriteTransform.position - transform.position;
        pixelCamera.forward = spriteTransform.position - transform.position;
        pixelCamera.GetComponent<Camera>().fieldOfView = Mathf.Atan2(spriteWidth, (pixelCamera.transform.position - spriteTransform.position).magnitude) * 2f * Mathf.Rad2Deg;
    }
}
