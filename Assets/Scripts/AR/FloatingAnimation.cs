using UnityEngine;

/// <summary>
/// Aplica una animación procedural de levitación (movimiento vertical suave) al objeto 3D
/// utilizando Mathf.Sin y Time.time en espacio local para mantener el anclaje relativo al marcador AR.
/// </summary>
public class FloatingAnimation : MonoBehaviour
{
    [Header("Parámetros de Flotación")]
    [Tooltip("Altura máxima de desplazamiento vertical en unidades locales.")]
    public float amplitude = 0.025f;

    [Tooltip("Velocidad o frecuencia de la oscilación.")]
    public float frequency = 2.0f;

    [Tooltip("Desfase inicial para variar el movimiento entre distintos marcadores.")]
    public float phaseOffset = 0.0f;

    [Header("Rotación Opcional")]
    [Tooltip("Si se activa, añade un giro suave alrededor del eje Y local.")]
    public bool enableGentleRotation = false;
    public float rotationSpeed = 20.0f;

    private Vector3 mInitialLocalPosition;
    private bool mInitialized = false;

    private void Awake()
    {
        CacheInitialPosition();
    }

    private void OnEnable()
    {
        CacheInitialPosition();
    }

    private void CacheInitialPosition()
    {
        if (!mInitialized)
        {
            mInitialLocalPosition = transform.localPosition;
            mInitialized = true;
        }
    }

    private void Update()
    {
        float yOffset = Mathf.Sin((Time.time * frequency) + phaseOffset) * amplitude;
        transform.localPosition = new Vector3(
            mInitialLocalPosition.x,
            mInitialLocalPosition.y + yOffset,
            mInitialLocalPosition.z
        );

        if (enableGentleRotation)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}

