using UnityEngine;
using UnityEngine.Events;
using Vuforia;

/// <summary>
/// Define los 4 días analizados en el Escenario 06 de respuesta a incidentes.
/// </summary>
public enum IncidentDay
{
    Viernes,
    Sabado,
    Lunes,
    Martes
}

/// <summary>
/// Controlador AR para marcadores Vuforia (ImageTarget) correspondientes a cada día del incidente.
/// Detecta la visualización del marcador mediante ObserverBehaviour o activación desde el HUD,
/// conmuta el contenido 3D, muestra la burbuja de diálogo narrativo y notifica al GameManager.
/// </summary>
[RequireComponent(typeof(ObserverBehaviour))]
public class DayTarget : MonoBehaviour
{
    [Header("Identificación del Día")]
    [Tooltip("Día del incidente que representa este marcador AR.")]
    public IncidentDay targetDay = IncidentDay.Viernes;

    [Header("Contenido AR (Escena en miniatura)")]
    [Tooltip("Objeto raíz que contiene la escena en miniatura o elementos 3D correspondientes a este día.")]
    [SerializeField] private GameObject dayContentRoot;

    [Header("Diálogo Narrativo en World Space")]
    [Tooltip("Referencia al componente ContextBubble hijo de este marcador.")]
    public ContextBubble contextBubble;

    [Tooltip("Texto narrativo que se muestra en el ContextBubble al detectar este marcador.")]
    [TextArea(2, 4)]
    public string narrativeText;

    [Header("Eventos de Detección")]
    [Tooltip("Evento disparado al detectar y comenzar a rastrear el marcador.")]
    public UnityEvent onTargetFound = new UnityEvent();

    [Tooltip("Evento disparado al perder el rastreo del marcador.")]
    public UnityEvent onTargetLost = new UnityEvent();

    private ObserverBehaviour mObserver;
    private bool mIsTracked = false;
    private bool mHasCustomSurfacePose = false;
    private Vector3 mCustomSurfacePosition;
    private Quaternion mCustomSurfaceRotation;

    public GameObject DayContentRoot => dayContentRoot;
    public bool IsTracked => mIsTracked;
    public bool HasCustomSurfacePose => mHasCustomSurfacePose;

    private void Awake()
    {
        mObserver = GetComponent<ObserverBehaviour>();

        if (dayContentRoot == null && transform.childCount > 0)
        {
            dayContentRoot = transform.GetChild(0).gameObject;
        }

        if (contextBubble == null)
        {
            contextBubble = GetComponentInChildren<ContextBubble>(true);
        }
    }

    private void Start()
    {
        // Si es el día Viernes (día inicial), mostrar su vista al arrancar para que el jugador vea elementos 3D de inmediato
        if (targetDay == IncidentDay.Viernes)
        {
            PositionInFrontOfCamera();
            OnTargetFound();
        }
        else
        {
            if (dayContentRoot != null)
            {
                dayContentRoot.SetActive(false);
            }

            if (contextBubble != null)
            {
                contextBubble.HideText();
            }
        }
    }

    private void OnEnable()
    {
        if (mObserver != null)
        {
            mObserver.OnTargetStatusChanged += OnObserverStatusChanged;
        }
    }

    private void OnDisable()
    {
        if (mObserver != null)
        {
            mObserver.OnTargetStatusChanged -= OnObserverStatusChanged;
        }
    }

    private void OnObserverStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
    {
        bool isTracked = targetStatus.Status == Status.TRACKED ||
                         targetStatus.Status == Status.EXTENDED_TRACKED;

        if (isTracked && !mIsTracked)
        {
            mIsTracked = true;
            OnTargetFound();
            if (StoryDirector.I != null)
            {
                StoryDirector.I.OnPhysicalCardScanned(targetDay);
            }
        }
        else if (!isTracked && mIsTracked)
        {
            mIsTracked = false;
            OnTargetLost();
        }
    }

    /// <summary>
    /// Fija la posición y rotación del escenario 3D sobre una superficie plana (mesa/escritorio) detectada o tocada.
    /// </summary>
    public void SetSurfacePose(Vector3 worldPos, Quaternion worldRot)
    {
        mHasCustomSurfacePose = true;
        mCustomSurfacePosition = worldPos;
        mCustomSurfaceRotation = worldRot;

        if (!mIsTracked)
        {
            transform.position = worldPos;
            transform.rotation = worldRot;
        }
    }

    /// <summary>
    /// Posiciona el marcador sobre una superficie horizontal (mesa/escritorio) frente a la cámara AR.
    /// </summary>
    public void PositionInFrontOfCamera(bool forceRecalculateFromCamera = false)
    {
        if (mIsTracked) return;

        if (mHasCustomSurfacePose && !forceRecalculateFromCamera)
        {
            transform.position = mCustomSurfacePosition;
            transform.rotation = mCustomSurfaceRotation;
            return;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();

            Vector3 tablePos = cam.transform.position + flatForward * 0.56f + Vector3.down * 0.08f;
            Quaternion tableRot = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f);
            SetSurfacePose(tablePos, tableRot);
        }
    }

    /// <summary>
    /// Fuerza la habilitación de Renderers, Colliders y Canvas hijos (por si Vuforia los deshabilitó).
    /// </summary>
    public void SetChildRenderersAndCanvasesEnabled(bool enabledState)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = enabledState;
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = enabledState;
        }

        var canvases = GetComponentsInChildren<Canvas>(true);
        foreach (var c in canvases)
        {
            c.enabled = enabledState;
        }
    }

    /// <summary>
    /// Invocado cuando el marcador de Vuforia es encontrado o cuando el día se selecciona en el HUD.
    /// </summary>
    public virtual void OnTargetFound()
    {
        // 1. Activar la escena en miniatura del día
        if (dayContentRoot != null)
        {
            dayContentRoot.SetActive(true);
        }

        // 2. Mostrar burbuja de contexto
        if (contextBubble != null && !string.IsNullOrEmpty(narrativeText))
        {
            contextBubble.ShowText(narrativeText);
        }

        // 3. Garantizar que los MeshRenderers y Canvas WorldSpace estén activos
        SetChildRenderersAndCanvasesEnabled(true);

        // 4. Notificar al GameManager el día activo
        if (GameManager.I != null)
        {
            GameManager.I.SetCurrentDay(targetDay);
        }

        // 5. Invocar evento de Unity
        onTargetFound?.Invoke();

        Debug.Log($"[DayTarget] Marcador activo: {targetDay}");
    }

    /// <summary>
    /// Invocado cuando el marcador se desactiva o se cambia a otro día.
    /// </summary>
    public virtual void OnTargetLost()
    {
        if (dayContentRoot != null)
        {
            dayContentRoot.SetActive(false);
        }

        if (contextBubble != null)
        {
            contextBubble.HideText();
        }

        SetChildRenderersAndCanvasesEnabled(false);

        onTargetLost?.Invoke();

        Debug.Log($"[DayTarget] Marcador oculto: {targetDay}");
    }
}
