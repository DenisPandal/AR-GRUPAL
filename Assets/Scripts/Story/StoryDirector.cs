using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vuforia;

/// <summary>
/// Línea de subtítulo narrada (quién habla + qué dice).
/// </summary>
[Serializable]
public class SubtitleLine
{
    public string speaker;
    [TextArea(2, 4)] public string text;
    [Tooltip("Clave opcional del clip de voz en Resources/Audio/Narrator para el narrador hablado.")]
    public string audioKey;
    [Tooltip("Clip de audio personalizado opcional para la locución de este diálogo.")]
    public AudioClip customAudioClip;

    public SubtitleLine() { }

    public SubtitleLine(string speaker, string text, string audioKey = null, AudioClip customAudioClip = null)
    {
        this.speaker = speaker;
        this.text = text;
        this.audioKey = audioKey;
        this.customAudioClip = customAudioClip;
    }
}

/// <summary>
/// Definición cinemática de un día del incidente.
/// </summary>
[Serializable]
public class StoryDay
{
    public IncidentDay day;

    [Header("Transición Cinemática")]
    public string transitionTitle = "SIMULACIÓN · ESCENARIO 6 — VIE 5";
    public string transitionSubtitle = "VIERNES 5";
    public string transitionTagline = "Ataque 2016: La primera señal en la sala SWIFT";
    [Tooltip("Horas aproximadas transcurridas desde la primera señal (para el reloj del caso).")]
    public float hoursSinceSignal;

    [Header("Escena 3D sobre la Superficie")]
    public DayTarget dayTarget;
    public GameObject diorama;
    [Tooltip("Escala base de la tarjeta/mesa 3D (por defecto 0.28, 0.02, 0.22).")]
    public Vector3 baseScale = new Vector3(0.28f, 0.02f, 0.22f);
    [Tooltip("Objeto principal que el jugador debe tocar para investigar este día.")]
    public ARInteractableObject keyObject;
    [Tooltip("Renderer que parpadea como alerta (LED, pantalla, barrera...).")]
    public Renderer alertRenderer;
    [Tooltip("Indicador 3D flotante que señala el objeto a investigar sobre la mesa.")]
    public GameObject investigateBeacon;

    [Header("Narración con Subtítulos")]
    public List<SubtitleLine> introLines = new List<SubtitleLine>();
    public string investigatePrompt;
    public List<SubtitleLine> attackLines = new List<SubtitleLine>();
    public List<SubtitleLine> correctLines = new List<SubtitleLine>();
    public List<SubtitleLine> wrongLines = new List<SubtitleLine>();

    [Header("Decisión del Protagonista (Modo Defensa)")]
    public string decisionQuestion;
    [Tooltip("0 = Plan con roles, 1 = Conciliación, 2 = Escalamiento, -1 = sin decisión (Martes).")]
    public int correctActionIndex = -1;
    public string correctResponsible;
}

/// <summary>
/// Director del Escenario 06 en AR:
/// - Al inicio muestra la selección de plan: MODO ATAQUE (Ataque 2016: VIE 5 -> SÁB 6 -> DOM 7 -> LUN 8 -> MAR 9, Congelado Martes 9:11 · Tarde)
///   o MODO DEFENSA (Con Plan: Congelado el Viernes, donde todo acaba el viernes VIE 5 y no ocurre ningún daño).
/// </summary>
public class StoryDirector : MonoBehaviour
{
    public static StoryDirector I { get; private set; }

    public enum StoryState
    {
        Intro,
        ScanningSurface,
        Transition,
        Narrating,
        Investigating,
        Deciding,
        Resolving,
        Outcome,
        Finished
    }

    public const string WaitActionId = "Esperar y no reportar";

    [Header("Referencias del Sistema")]
    public GameManager gameManager;
    public ActionPicker actionPicker;
    public IncidentTimer incidentTimer;
    public ScenarioHUDController hud;
    [Tooltip("Gestor de locución y narración hablada exclusivo para los diálogos.")]
    public DialogueNarrator narrator;

    [Header("Detección de Superficie (Mesa AR)")]
    public PlaneFinderBehaviour planeFinder;
    public ContentPositioningBehaviour contentPositioning;
    public AnchorInputListenerBehaviour anchorInput;
    public AnchorBehaviour groundPlaneStage;
    [Tooltip("Distancia frontal por defecto para apoyar las escenas sobre la mesa (metros).")]
    public float surfaceDistance = 0.56f;
    [Tooltip("Altura relativa a la cámara para simular la superficie de una mesa (metros).")]
    public float surfaceHeightOffset = -0.08f;
    [Tooltip("Tiempo en segundos enfocando la superficie para auto-anclar si el usuario no toca antes.")]
    public float autoAnchorSurfaceDelay = 25f;

    [Header("Secuencia de Días")]
    public List<StoryDay> days = new List<StoryDay>();

    [Header("UI - Selección Inicial de Modo (Ataque vs Defensa)")]
    public GameObject introPanel;
    public Button attackModeButton;
    public Button defenseModeButton;

    [Header("UI - Escaneo de Superficie / Mesa")]
    public GameObject scanPanel;
    public TMP_Text scanHintText;

    [Header("UI - Subtítulos Cinemáticos")]
    public GameObject subtitlePanel;
    public TMP_Text speakerText;
    public TMP_Text subtitleText;
    public TMP_Text continueHintText;
    public float charactersPerSecond = 45f;

    [Header("UI - Transición Cinemática entre Días")]
    public CanvasGroup transitionGroup;
    public TMP_Text transitionTitleText;
    public TMP_Text transitionSubtitleText;
    public TMP_Text transitionTaglineText;

    [Header("UI - Decisión del Protagonista")]
    public GameObject decisionPanel;
    public TMP_Text decisionTitleText;
    public Button[] actionButtons = new Button[4];
    public Button[] responsibleButtons = new Button[4];
    public Button confirmButton;

    [Header("Audio Narrativo, Efectos y Música")]
    public AudioSource audioSource;
    public AudioSource musicSource;
    public AudioClip clueClip;
    public AudioClip backgroundMusicClip;
    public AudioClip uiClickClip;
    public AudioClip cardScanClip;
    public AudioClip decisionCorrectClip;
    public AudioClip decisionWrongClip;
    public AudioClip dayTransitionClip;
    public AudioClip attackAlertClip;
    public AudioClip defenseVictoryClip;
    public AudioClip typewriterTickClip;

    [Header("Estado Actual (Solo Lectura)")]
    [SerializeField] private StoryState state = StoryState.Intro;
    [SerializeField] private int currentDayIndex = -1;

    public StoryState State => state;
    public int CurrentDayIndex => currentDayIndex;

    private static readonly Color SelectedColor = new Color(0.14f, 0.58f, 0.92f, 1f);
    private static readonly Color UnselectedColor = new Color(0.18f, 0.24f, 0.34f, 0.95f);
    private static readonly Color ResolvedColor = new Color(0.18f, 0.85f, 0.45f, 1f);

    private readonly string[] mActionOptions =
    {
        "Plan de respuesta con roles",
        "Conciliación diaria automática",
        "Escalamiento inmediato",
        WaitActionId
    };

    private readonly string[] mDefaultResponsibles =
    {
        "Hugo Benavides",
        "Equipo de Respuesta a Incidentes",
        "Oficial de Seguridad (CISO)",
        "Operaciones SWIFT"
    };

    private bool mTapRequested;
    private bool mKeyObjectInspected;
    private bool mDecisionConfirmed;
    private bool mStagePlaced;
    private bool mBusyWithLine;
    private int mSelectedAction = -1;
    private int mSelectedResponsible = -1;
    private float mScanTimer = 0f;
    private Vector3 mSurfacePosition;
    private Quaternion mSurfaceRotation = Quaternion.identity;
    private Coroutine mStoryRoutine;
    private Coroutine mAlertRoutine;
    private Coroutine mIntroWelcomeRoutine;

    private void Awake()
    {
        I = this;

        // Asegurar orientación Horizontal (Landscape) en cualquier teléfono
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        if (Screen.orientation == ScreenOrientation.Portrait ||
            Screen.orientation == ScreenOrientation.PortraitUpsideDown ||
            Screen.width < Screen.height)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (actionPicker == null) actionPicker = FindAnyObjectByType<ActionPicker>();
        if (incidentTimer == null) incidentTimer = FindAnyObjectByType<IncidentTimer>();
        if (hud == null) hud = FindAnyObjectByType<ScenarioHUDController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (narrator == null) narrator = FindAnyObjectByType<DialogueNarrator>();
        if (narrator == null)
        {
            var narratorGo = new GameObject("DialogueNarrator");
            narrator = narratorGo.AddComponent<DialogueNarrator>();
        }
        if (narrator != null && narrator.musicAudioSource == null && musicSource != null)
        {
            narrator.musicAudioSource = musicSource;
        }

        if (days == null || days.Count == 0)
        {
            days = CreateDefaultDays();
        }

        // Registrar la escala original real de cada maqueta 3D (0.28, 0.02, 0.22)
        foreach (var d in days)
        {
            if (d.diorama != null)
            {
                Vector3 s = d.diorama.transform.localScale;
                if (s.y > 0.001f && s.y < 0.5f)
                {
                    d.baseScale = s;
                }
                else
                {
                    d.baseScale = new Vector3(0.28f, 0.02f, 0.22f);
                    d.diorama.transform.localScale = d.baseScale;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
        if (contentPositioning != null)
        {
            contentPositioning.OnContentPlaced.RemoveListener(OnVuforiaContentPlaced);
        }
    }

    private void Start()
    {
        if (contentPositioning != null)
        {
            contentPositioning.OnContentPlaced.AddListener(OnVuforiaContentPlaced);
        }

        if (attackModeButton != null)
        {
            attackModeButton.onClick.RemoveAllListeners();
            attackModeButton.onClick.AddListener(StartAttackMode);
        }

        if (defenseModeButton != null)
        {
            defenseModeButton.onClick.RemoveAllListeners();
            defenseModeButton.onClick.AddListener(StartDefenseMode);
        }

        for (int i = 0; i < actionButtons.Length; i++)
        {
            int idx = i;
            if (actionButtons[i] != null)
            {
                actionButtons[i].onClick.RemoveAllListeners();
                actionButtons[i].onClick.AddListener(() => SelectAction(idx));
            }
        }

        for (int i = 0; i < responsibleButtons.Length; i++)
        {
            int idx = i;
            if (responsibleButtons[i] != null)
            {
                responsibleButtons[i].onClick.RemoveAllListeners();
                responsibleButtons[i].onClick.AddListener(() => SelectResponsible(idx));
            }
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(ConfirmDecision);
        }

        // Conectar sonido de clic a todos los botones de la interfaz y reproducir música de fondo
        RegisterAllButtonsClickAudio();
        StartBackgroundMusic();

        // Posicionar inicialmente la escena del Viernes sobre la mesa de fondo y mostrar el selector de Plan (Ataque vs Defensa)
        if (!mStagePlaced)
        {
            CalculateTablePoseFromCamera(out mSurfacePosition, out mSurfaceRotation);
            ApplySurfacePoseToAllDays(mSurfacePosition, mSurfaceRotation);
        }

        ShowModeSelectionScreen();
    }

    private void Update()
    {
        bool pressedThisFrame = false;
        Vector2 screenPos = Vector2.zero;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pressedThisFrame = true;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressedThisFrame = true;
            screenPos = Mouse.current.position.ReadValue();
        }

        // 1. Si estamos enfocando la mesa/superficie: permitir anclar tocando la mesa o automáticamente al enfocar
        if (state == StoryState.ScanningSurface)
        {
            mScanTimer += Time.deltaTime;

            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (pressedThisFrame && !pointerOverUI)
            {
                if (planeFinder != null && planeFinder.isActiveAndEnabled)
                {
                    planeFinder.PerformHitTest(screenPos);
                }
                PlaceOnTableAtScreenPoint(screenPos);
                return;
            }

            if (!mStagePlaced && autoAnchorSurfaceDelay > 0f && mScanTimer >= autoAnchorSurfaceDelay)
            {
                PlaceInFrontOfCamera();
                return;
            }
        }

        // 2. Si estamos narrando subtítulos: un toque acelera o pasa al siguiente subtítulo
        if ((state == StoryState.Narrating || state == StoryState.Resolving || state == StoryState.Outcome || mBusyWithLine) && pressedThisFrame)
        {
            mTapRequested = true;
        }

        // 3. Garantizar en todo momento que la escena 3D del día actual permanezca activa y visible en la superficie o tarjeta
        if (mStagePlaced && currentDayIndex >= 0 && currentDayIndex < days.Count)
        {
            if (groundPlaneStage != null && groundPlaneStage.gameObject.activeInHierarchy && groundPlaneStage.transform.position.sqrMagnitude > 0.0001f)
            {
                mSurfacePosition = groundPlaneStage.transform.position;
                mSurfaceRotation = groundPlaneStage.transform.rotation;
            }

            var activeDay = days[currentDayIndex];
            if (activeDay.dayTarget != null && !activeDay.dayTarget.IsTracked)
            {
                activeDay.dayTarget.SetSurfacePose(mSurfacePosition, mSurfaceRotation);
            }
            if (activeDay.diorama != null)
            {
                if (!activeDay.diorama.activeSelf) activeDay.diorama.SetActive(true);
                ForceVisible(activeDay.diorama);
            }
        }
    }

    // ───────────────────────── Selección de Plan (Modo Ataque vs Modo Defensa) ─────────────────────────

    /// <summary>
    /// Muestra la pantalla inicial para elegir entre Plan Ataque (2016) o Plan Defensa (Congelado el Viernes).
    /// </summary>
    public void ShowModeSelectionScreen()
    {
        if (mIntroWelcomeRoutine != null)
        {
            StopCoroutine(mIntroWelcomeRoutine);
            mIntroWelcomeRoutine = null;
        }
        if (mStoryRoutine != null) StopCoroutine(mStoryRoutine);
        StopAllCoroutines();
        mBusyWithLine = false;
        if (narrator != null) narrator.StopSpeaking();
        mStagePlaced = false;
        currentDayIndex = 0;

        state = StoryState.Intro;
        SetActiveSafe(introPanel, true);
        SetActiveSafe(scanPanel, false);
        SetActiveSafe(subtitlePanel, false);
        SetActiveSafe(decisionPanel, false);
        SetTransitionVisible(false, 0f);

        if (gameManager != null && gameManager.planBuilder != null)
        {
            gameManager.planBuilder.HidePlan();
        }

        // Mostrar de fondo la maqueta del Viernes en la mesa para que la cámara siempre tenga contenido 3D visible
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i].dayTarget != null)
            {
                if (i == 0)
                {
                    days[i].dayTarget.SetSurfacePose(mSurfacePosition, mSurfaceRotation);
                    days[i].dayTarget.OnTargetFound();
                    if (days[i].diorama != null)
                    {
                        days[i].diorama.transform.localScale = days[i].baseScale;
                        ForceVisible(days[i].diorama);
                    }
                }
                else
                {
                    days[i].dayTarget.OnTargetLost();
                }
            }
        }

        // Bienvenida breve: las dos tarjetas explican la elección sin una secuencia de cinco párrafos.
        if (narrator != null)
            narrator.Speak(new SubtitleLine("NARRADOR",
                "Bienvenido a la simulación interactiva en Realidad Aumentada del ciberataque financiero de 2016.",
                "intro_welcome_1"));
    }

    /// <summary>
    /// Locución narrativa y subtítulos de bienvenida al entrar al juego: explica qué hará el usuario y las opciones de Ataque vs Defensa.
    /// </summary>
    private IEnumerator IntroWelcomeRoutine()
    {
        yield return new WaitForSeconds(0.4f);

        var welcomeLines = new List<SubtitleLine>
        {
            new SubtitleLine("BIENVENIDO · ESCENARIO 06", "Bienvenido a la simulación interactiva en Realidad Aumentada del ciberataque financiero de 2016.", "intro_welcome_1"),
            new SubtitleLine("¿QUÉ HARÁS EN EL JUEGO?", "Analizarás cronológicamente cómo se desarrolló el ataque desde el Viernes hasta el Martes, interactuando con los objetos en tu mesa.", "intro_welcome_2"),
            new SubtitleLine("OPCIÓN 1 · MODO ATAQUE", "En Modo Ataque revivirás los hechos reales de 2016, donde la falta de respuesta rápida causó la pérdida de US$58 millones.", "intro_welcome_3"),
            new SubtitleLine("OPCIÓN 2 · MODO DEFENSA", "En Modo Defensa aplicarás las 3 medidas clave para congelar los fondos el mismo viernes sin sufrir ningún daño.", "intro_welcome_4"),
            new SubtitleLine("ELIGE TU MODO EN PANTALLA", "Selecciona Modo Ataque o Modo Defensa en pantalla para comenzar tu investigación en la mesa.", "intro_welcome_5")
        };

        foreach (var line in welcomeLines)
        {
            if (state != StoryState.Intro) yield break;
            yield return ShowLine(line, true);
        }

        if (state == StoryState.Intro)
        {
            ShowPersistentPrompt("Selecciona MODO ATAQUE o MODO DEFENSA en los botones de arriba para iniciar la simulación.");
        }
    }

    /// <summary>
    /// Inicia el MODO ATAQUE (Ataque 2016: VIE 5 -> SÁB 6 -> DOM 7 -> LUN 8 -> MAR 9 · 9:11 a.m. Tarde).
    /// </summary>
    public void StartAttackMode()
    {
        BeginStoryWithMode(SimMode.Ataque);
    }

    /// <summary>
    /// Inicia el MODO DEFENSA (Con Plan: Congelado el Viernes, todo acaba el viernes sin ningún daño).
    /// </summary>
    public void StartDefenseMode()
    {
        BeginStoryWithMode(SimMode.Defensa);
    }

    private void BeginStoryWithMode(SimMode selectedMode)
    {
        if (mIntroWelcomeRoutine != null)
        {
            StopCoroutine(mIntroWelcomeRoutine);
            mIntroWelcomeRoutine = null;
        }
        if (narrator != null) narrator.StopSpeaking();
        else if (DialogueNarrator.I != null) DialogueNarrator.I.StopSpeaking();

        if (mStoryRoutine != null) StopCoroutine(mStoryRoutine);
        StopAllCoroutines();
        mBusyWithLine = false;
        mStagePlaced = false;
        currentDayIndex = -1;

        if (gameManager != null)
        {
            gameManager.ResetScenarioState();
            gameManager.mode = selectedMode;
            gameManager.SetCurrentDay(IncidentDay.Viernes);
            if (gameManager.planBuilder != null)
            {
                gameManager.planBuilder.HidePlan();
            }
        }

        // Restablecer el estado de interacción de todos los objetos interactivos 3D
        var allInteractables = FindObjectsByType<ARInteractableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in allInteractables)
        {
            obj.ResetInteractionState();
        }

        if (incidentTimer != null)
        {
            incidentTimer.SetStoryClock(selectedMode == SimMode.Defensa ? 0f : 85f);
        }

        if (hud != null)
        {
            hud.RefreshHUDLabels();
            if (hud.narrativeBannerText != null)
            {
                hud.narrativeBannerText.text = (selectedMode == SimMode.Defensa)
                    ? "<b>[MODO DEFENSA · CON PLAN]</b> TIEMPO HASTA CONGELAR LOS FONDOS: CON PLAN: CONGELADO EL VIERNES."
                    : "<b>[MODO ATAQUE · 2016]</b> VIE 5 · SÁB 6 · DOM 7 · LUN 8 · MAR 9 | CONGELADO: MARTES 9:11 · TARDE.";
            }
        }

        SetActiveSafe(introPanel, false);
        BeginSurfaceScan();
    }

    // ───────────────────────── Detección y Anclaje en Superficie (Mesa) ─────────────────────────

    public void BeginSurfaceScan()
    {
        state = StoryState.ScanningSurface;
        mScanTimer = 0f;
        SetActiveSafe(scanPanel, true);
        SetActiveSafe(subtitlePanel, false);
        SetActiveSafe(decisionPanel, false);

        if (scanHintText != null)
        {
            string modeLabel = (gameManager != null && gameManager.mode == SimMode.Defensa)
                ? "MODO DEFENSA (CON PLAN: CONGELADO EL VIERNES)"
                : "MODO ATAQUE (ATAQUE 2016: VIE 5 -> MAR 9)";
            scanHintText.text = "<size=34><b>Tu mesa es el escenario.</b></size>\nApunta a una superficie plana y tócala para colocar el caso.";
        }

        if (planeFinder != null) planeFinder.gameObject.SetActive(true);
        if (anchorInput != null) anchorInput.enabled = true;
    }

    public void OnVuforiaContentPlaced(GameObject anchor)
    {
        if (anchor == null) return;

        Camera cam = Camera.main;
        Vector3 lookDir = Vector3.forward;
        if (cam != null)
        {
            lookDir = Vector3.ProjectOnPlane(anchor.transform.position - cam.transform.position, Vector3.up);
            if (lookDir.sqrMagnitude < 0.001f) lookDir = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        }
        if (lookDir.sqrMagnitude < 0.001f) lookDir = Vector3.forward;

        AnchorScenarioOnSurface(anchor.transform.position, Quaternion.LookRotation(lookDir.normalized, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f));
    }

    public void PlaceOnTableAtScreenPoint(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            PlaceInFrontOfCamera();
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane tablePlane = new Plane(Vector3.up, cam.transform.position + Vector3.up * surfaceHeightOffset);

        Vector3 hitPoint;
        if (tablePlane.Raycast(ray, out float enter) && enter > 0.2f && enter < 2.5f)
        {
            hitPoint = ray.GetPoint(enter);
        }
        else
        {
            CalculateTablePoseFromCamera(out hitPoint, out _);
        }

        Vector3 flatLook = Vector3.ProjectOnPlane(hitPoint - cam.transform.position, Vector3.up);
        if (flatLook.sqrMagnitude < 0.001f)
        {
            flatLook = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        }
        if (flatLook.sqrMagnitude < 0.001f) flatLook = Vector3.forward;

        AnchorScenarioOnSurface(hitPoint, Quaternion.LookRotation(flatLook.normalized, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f));
    }

    public void PlaceInFrontOfCamera()
    {
        CalculateTablePoseFromCamera(out Vector3 pos, out Quaternion rot);
        AnchorScenarioOnSurface(pos, rot);
    }

    private void CalculateTablePoseFromCamera(out Vector3 pos, out Quaternion rot)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            pos = new Vector3(0f, surfaceHeightOffset, surfaceDistance);
            rot = Quaternion.Euler(-12f, 0f, 0f);
            return;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        pos = cam.transform.position + flatForward * surfaceDistance + Vector3.up * surfaceHeightOffset;
        rot = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f);
    }

    private void AnchorScenarioOnSurface(Vector3 worldPos, Quaternion worldRot)
    {
        mSurfacePosition = worldPos;
        mSurfaceRotation = worldRot;

        if (groundPlaneStage != null)
        {
            groundPlaneStage.transform.position = worldPos;
            groundPlaneStage.transform.rotation = worldRot;
            groundPlaneStage.gameObject.SetActive(true);
        }

        ApplySurfacePoseToAllDays(worldPos, worldRot);

        if (planeFinder != null) planeFinder.gameObject.SetActive(false);
        if (anchorInput != null) anchorInput.enabled = false;
        SetActiveSafe(scanPanel, false);

        if (!mStagePlaced)
        {
            mStagePlaced = true;
            if (mStoryRoutine != null) StopCoroutine(mStoryRoutine);
            mStoryRoutine = StartCoroutine(PlayDayRoutine(0));
        }
        else
        {
            if (currentDayIndex >= 0 && currentDayIndex < days.Count)
            {
                var currentDay = days[currentDayIndex];
                if (currentDay.dayTarget != null)
                {
                    currentDay.dayTarget.SetSurfacePose(worldPos, worldRot);
                    currentDay.dayTarget.OnTargetFound();
                }
            }
            state = StoryState.Investigating;
            SetActiveSafe(subtitlePanel, true);
        }
    }

    private void ApplySurfacePoseToAllDays(Vector3 worldPos, Quaternion worldRot)
    {
        if (groundPlaneStage != null)
        {
            groundPlaneStage.transform.position = worldPos;
            groundPlaneStage.transform.rotation = worldRot;
            groundPlaneStage.gameObject.SetActive(true);
        }

        foreach (var d in days)
        {
            // Mantener los ImageTargets en la raíz de la escena para que Vuforia reconozca las 4 tarjetas físicas sin conflictos
            if (d.dayTarget != null)
            {
                if (d.dayTarget.transform.parent != null)
                {
                    d.dayTarget.transform.SetParent(null, true);
                }
                d.dayTarget.SetSurfacePose(worldPos, worldRot);
            }
            else if (d.diorama != null)
            {
                d.diorama.transform.position = worldPos;
                d.diorama.transform.rotation = worldRot;
            }
        }
    }

    public void RelocateStage()
    {
        mScanTimer = -10f;
        BeginSurfaceScan();
    }

    /// <summary>
    /// Invocado automáticamente cuando la cámara de Vuforia reconoce físicamente una de las 4 tarjetas (RF-01 / Criterio 1).
    /// </summary>
    public void OnPhysicalCardScanned(IncidentDay scannedDay)
    {
        int targetIdx = -1;
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i].day == scannedDay)
            {
                targetIdx = i;
                break;
            }
        }

        if (targetIdx < 0) return;

        // En Modo Defensa todo ocurre y se resuelve el Viernes (VIE 5)
        if (gameManager != null && gameManager.mode == SimMode.Defensa && targetIdx > 0)
        {
            targetIdx = 0;
        }

        SetActiveSafe(introPanel, false);
        SetActiveSafe(scanPanel, false);
        if (planeFinder != null) planeFinder.gameObject.SetActive(false);
        if (anchorInput != null) anchorInput.enabled = false;
        mStagePlaced = true;

        // Si aún no estamos reproduciendo ese día, iniciar su secuencia cinemática
        if (currentDayIndex != targetIdx || state == StoryState.Intro || state == StoryState.ScanningSurface || state == StoryState.Finished)
        {
            if (mStoryRoutine != null) StopCoroutine(mStoryRoutine);
            mStoryRoutine = StartCoroutine(PlayDayRoutine(targetIdx));
        }
    }

    /// <summary>
    /// Paso 6 del Flujo del Jugador: Aplica el Plan de Respuesta armado por el jugador y ejecuta el Modo Defensa
    /// para ver el nuevo resultado (RF-04: el congelamiento ocurre el viernes y el dinero no sale).
    /// </summary>
    public void ApplyPlayerPlanAndShowDefenseOutcome()
    {
        if (mStoryRoutine != null) StopCoroutine(mStoryRoutine);
        StopAllCoroutines();
        mBusyWithLine = false;
        mStagePlaced = true;

        if (gameManager != null)
        {
            gameManager.mode = SimMode.Defensa;
            gameManager.SetCurrentDay(IncidentDay.Viernes);
            if (gameManager.planBuilder != null)
            {
                gameManager.planBuilder.HidePlan();
            }
        }

        if (hud != null)
        {
            hud.RefreshHUDLabels();
        }

        // Mostrar todas las tarjetas salvo Viernes ocultas y activar Viernes con la resolución del plan
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i].dayTarget != null)
            {
                if (i == 0)
                {
                    days[i].dayTarget.SetSurfacePose(mSurfacePosition, mSurfaceRotation);
                    days[i].dayTarget.OnTargetFound();
                }
                else
                {
                    days[i].dayTarget.OnTargetLost();
                }
            }
        }

        currentDayIndex = 0;
        if (days.Count > 0)
        {
            mStoryRoutine = StartCoroutine(DefenseFridayResolutionRoutine(days[0]));
        }
    }

    // ───────────────────────── Secuencia Cinemática Día por Día ─────────────────────────

    private IEnumerator PlayDayRoutine(int index)
    {
        if (index < 0 || index >= days.Count) yield break;

        bool isDefense = gameManager != null && gameManager.mode == SimMode.Defensa;

        // 1. Ocultar con animación el día anterior
        if (currentDayIndex >= 0 && currentDayIndex < days.Count && currentDayIndex != index)
        {
            var prev = days[currentDayIndex];
            StopAlert();
            if (prev.investigateBeacon != null) prev.investigateBeacon.SetActive(false);
            if (prev.diorama != null && prev.diorama.activeInHierarchy)
            {
                Vector3 prevBase = prev.baseScale.sqrMagnitude > 0.0001f ? prev.baseScale : new Vector3(0.28f, 0.02f, 0.22f);
                yield return ScaleRoutine(prev.diorama.transform, prevBase, 1f, 0f, 0.35f);
                prev.diorama.transform.localScale = prevBase;
            }
            if (prev.dayTarget != null) prev.dayTarget.OnTargetLost();
        }

        for (int i = 0; i < days.Count; i++)
        {
            if (i != index && days[i].dayTarget != null)
            {
                days[i].dayTarget.OnTargetLost();
            }
        }

        currentDayIndex = index;
        StoryDay day = days[index];
        mKeyObjectInspected = false;
        Vector3 dayBaseScale = day.baseScale.sqrMagnitude > 0.0001f ? day.baseScale : new Vector3(0.28f, 0.02f, 0.22f);

        // 2. Mostrar pantalla de Transición Cinemática indicando qué día y modo inicia
        state = StoryState.Transition;
        SetActiveSafe(subtitlePanel, false);
        SetActiveSafe(decisionPanel, false);

        if (isDefense && index == 0)
        {
            if (transitionTitleText != null) transitionTitleText.text = "SIMULACIÓN · ESCENARIO 6 — MODO DEFENSA";
            if (transitionSubtitleText != null) transitionSubtitleText.text = "VIE 5 (VIERNES)";
            if (transitionTaglineText != null) transitionTaglineText.text = "TIEMPO HASTA CONGELAR LOS FONDOS · CON PLAN: CONGELADO EL VIERNES";
        }
        else
        {
            if (transitionTitleText != null) transitionTitleText.text = day.transitionTitle;
            if (transitionSubtitleText != null) transitionSubtitleText.text = day.transitionSubtitle;
            if (transitionTaglineText != null) transitionTaglineText.text = day.transitionTagline;
        }

        PlayDayTransition();
        yield return FadeTransition(0f, 1f, 0.4f);

        if (!mStagePlaced)
        {
            CalculateTablePoseFromCamera(out mSurfacePosition, out mSurfaceRotation);
            ApplySurfacePoseToAllDays(mSurfacePosition, mSurfaceRotation);
        }

        if (gameManager != null) gameManager.SetCurrentDay(day.day);
        if (incidentTimer != null) incidentTimer.SetStoryClock(isDefense ? 0f : day.hoursSinceSignal);
        if (hud != null) hud.RefreshHUDLabels();

        if (day.dayTarget != null)
        {
            day.dayTarget.SetSurfacePose(mSurfacePosition, mSurfaceRotation);
            day.dayTarget.OnTargetFound();
        }

        if (day.diorama != null)
        {
            day.diorama.SetActive(true);
            ForceVisible(day.diorama);
            day.diorama.transform.localScale = dayBaseScale * 0.1f;
        }

        yield return new WaitForSeconds(1.35f);
        yield return FadeTransition(1f, 0f, 0.4f);

        // 3. Animación de aparición de los objetos 3D sobre la superficie de la mesa
        if (day.diorama != null)
        {
            yield return ScaleRoutine(day.diorama.transform, dayBaseScale, 0.1f, 1f, 0.45f);
            day.diorama.transform.localScale = dayBaseScale;
            ForceVisible(day.diorama);
        }
        StartAlert(day.alertRenderer);

        // 4. Narración de subtítulos de introducción del día
        state = StoryState.Narrating;
        if (isDefense && index == 0)
        {
            yield return PlayLines(new List<SubtitleLine>
            {
                new SubtitleLine("SIMULACIÓN · ESCENARIO 6", "MODO DEFENSA (VIE 5): Hugo Benavides ve la impresora de confirmaciones SWIFT apagada sobre la mesa.", "def_intro_1"),
                new SubtitleLine("PLAN DE RESPUESTA", "A diferencia del ataque de 2016, hoy cuentas con un Plan de Respuesta con roles, conciliación automática y escalamiento 24/7.", "def_intro_2")
            });
        }
        else
        {
            yield return PlayLines(day.introLines);
        }

        // 5. Fase de Interacción en la Superficie: el jugador toca los objetos 3D sobre la mesa
        state = StoryState.Investigating;
        if (day.investigateBeacon != null) day.investigateBeacon.SetActive(true);

        string promptMsg = (isDefense && index == 0)
            ? ">> TOCA LA IMPRESORA O A HUGO BENAVIDES para elegir la acción y responsable del Plan de Respuesta."
            : day.investigatePrompt;
        ShowPersistentPrompt(promptMsg);

        while (!mKeyObjectInspected)
        {
            yield return null;
        }

        if (day.investigateBeacon != null) day.investigateBeacon.SetActive(false);

        // 6. En MODO DEFENSA: el jugador elige/confirma acción y responsable en Viernes (RF-02) y todo se congela el viernes sin daños (RF-04)
        if (isDefense)
        {
            yield return DecisionRoutine(day);
            yield return DefenseFridayResolutionRoutine(day);
            yield break;
        }

        // 7. En MODO ATAQUE: si es Martes (MAR 9), reproducir el desenlace del ataque de 2016 y mostrar el Plan armado
        if (day.correctActionIndex < 0)
        {
            yield return AttackOutcomeRoutine(day);
            yield break;
        }

        // 8. RF-02 (Pasos 4 y 5): En cada tarjeta (Viernes, Sábado, Lunes), el jugador elige qué debió hacerse y quién debía hacerlo
        yield return DecisionRoutine(day);

        // 9. En MODO ATAQUE (VIE 5, SÁB 6, LUN 8): narrar lo que ocurrió en el ataque real de 2016 y avanzar a la siguiente tarjeta
        state = StoryState.Resolving;
        yield return PlayLines(day.attackLines);

        mStoryRoutine = StartCoroutine(PlayDayRoutine(index + 1));
    }

    /// <summary>
    /// En Modo Defensa: aplica el Plan de Respuesta el Viernes (VIE 5), congela los fondos ese mismo viernes (0h) y finaliza sin daños (RF-04 y RF-05).
    /// </summary>
    private IEnumerator DefenseFridayResolutionRoutine(StoryDay viernes)
    {
        state = StoryState.Resolving;

        string chosenResp = "Hugo Benavides";
        var responsibles = GetResponsibles();
        if (mSelectedResponsible >= 0 && mSelectedResponsible < responsibles.Count)
        {
            chosenResp = responsibles[mSelectedResponsible];
        }

        // Aplicar el Plan de Respuesta completo en Viernes (VIE 5) para que el congelamiento ocurra ese mismo viernes y el dinero no salga
        if (gameManager != null)
        {
            gameManager.ApplyCompleteFridayDefensePlan(chosenResp);
        }

        if (incidentTimer != null)
        {
            incidentTimer.SetStoryClock(0f);
        }

        if (hud != null)
        {
            hud.RefreshHUDLabels();
            if (hud.narrativeBannerText != null)
            {
                hud.narrativeBannerText.text = "<color=#4ADE80><b>[RF-04 · CON PLAN: CONGELADO EL VIERNES]</b></color> El congelamiento ocurre el viernes (0h) y el dinero no sale.";
            }
        }

        StopAlert();
        SetRendererColor(viernes.alertRenderer, ResolvedColor);
        PlayDefenseVictory();

        var victoryLines = new List<SubtitleLine>
        {
            new SubtitleLine("CON PLAN: CONGELADO EL VIERNES", $"¡Plan de Respuesta ejecutado en VIE 5! {chosenResp} activa el protocolo y reporta la falla de la impresora de inmediato.", "def_victory_1"),
            new SubtitleLine("RF-04 · DINERO NO SALE", "La conciliación diaria automática y el escalamiento inmediato bloquean las transferencias el mismo viernes (0 horas perdidas).", "def_victory_2"),
            new SubtitleLine("NUEVO RESULTADO", "El plan armado cambió el resultado: el congelamiento ocurre el viernes y los US$58 millones NO salen del banco.", "def_victory_3")
        };

        yield return PlayLines(victoryLines);

        state = StoryState.Finished;
        SetActiveSafe(subtitlePanel, false);

        if (gameManager != null)
        {
            gameManager.EndScenario();
        }
    }

    /// <summary>
    /// RF-02 (Pasos 4 y 5): Presenta las opciones en cada tarjeta para que el jugador elija qué debió hacerse y quién debía hacerlo,
    /// valida si las 2 respuestas son acertadas o incorrectas (mostrando las respuestas correctas) y continúa el flujo normal del caso.
    /// </summary>
    private IEnumerator DecisionRoutine(StoryDay day)
    {
        state = StoryState.Deciding;
        mDecisionConfirmed = false;

        var responsibles = GetResponsibles();
        int expectedActionIdx = Mathf.Clamp(day.correctActionIndex, 0, mActionOptions.Length - 1);
        string correctAction = mActionOptions[expectedActionIdx];

        int expectedRespIdx = 0;
        for (int i = 0; i < responsibles.Count; i++)
        {
            if (string.Equals(responsibles[i], day.correctResponsible, StringComparison.OrdinalIgnoreCase))
            {
                expectedRespIdx = i;
                break;
            }
        }
        string correctResp = responsibles[expectedRespIdx];

        // Iniciar sin selección previa para que el jugador elija sus 2 respuestas (1 Acción + 1 Responsable)
        mSelectedAction = -1;
        mSelectedResponsible = -1;

        bool isDefense = gameManager != null && gameManager.mode == SimMode.Defensa;
        ShowPersistentPrompt($"<color=#38BDF8><b>[TARJETA {day.day.ToString().ToUpper()} - RF-02]:</b></color> Elige <b>1 ACCIÓN</b> (izquierda) y <b>1 RESPONSABLE</b> (derecha) y presiona Confirmar.");
        SetActiveSafe(decisionPanel, true);

        if (decisionTitleText != null)
        {
            string baseQuestion = !string.IsNullOrEmpty(day.decisionQuestion)
                ? day.decisionQuestion
                : $"TARJETA {day.day.ToString().ToUpper()}: ¿Qué debió hacerse y quién debía hacerlo?";
            decisionTitleText.text = $"<color=#38BDF8><b>[{day.transitionSubtitle}]</b></color> {baseQuestion}\n<size=17><color=#94A3B8>Selecciona las 2 respuestas correctas (1 Acción + 1 Responsable) y presiona el botón inferior:</color></size>";
        }

        string[] actionIcons = { "[1]", "[2]", "[3]", "[4]" };
        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] != null) actionButtons[i].interactable = true;
            string icon = i < actionIcons.Length ? actionIcons[i] : "-";
            string txt = i < mActionOptions.Length ? $"{icon} <b>{mActionOptions[i]}</b>" : "-";
            SetButtonLabel(actionButtons[i], txt);
        }

        for (int i = 0; i < responsibleButtons.Length; i++)
        {
            if (responsibleButtons[i] != null) responsibleButtons[i].interactable = true;
            string txt = i < responsibles.Count ? $"[RESP] <b>{responsibles[i]}</b>" : "-";
            SetButtonLabel(responsibleButtons[i], txt);
        }

        RefreshDecisionVisuals();

        while (!mDecisionConfirmed)
        {
            yield return null;
        }

        // Evaluar las 2 respuestas elegidas por el jugador
        int chosenActionIdx = Mathf.Clamp(mSelectedAction, 0, mActionOptions.Length - 1);
        int chosenRespIdx = Mathf.Clamp(mSelectedResponsible, 0, responsibles.Count - 1);
        string chosenAction = mActionOptions[chosenActionIdx];
        string chosenResp = responsibles[chosenRespIdx];

        bool isActionCorrect = (chosenActionIdx == expectedActionIdx);
        bool isRespCorrect = (chosenRespIdx == expectedRespIdx);
        bool isBothCorrect = isActionCorrect && isRespCorrect;

        PlayDecisionResult(isBothCorrect);

        // Bloquear cambio de botones durante la retroalimentación y colorear aciertos (verde) / errores (rojo)
        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] == null) continue;
            actionButtons[i].interactable = false;
            string icon = i < actionIcons.Length ? actionIcons[i] : "-";
            string label = i < mActionOptions.Length ? mActionOptions[i] : "-";

            if (i == expectedActionIdx)
            {
                SetButtonFeedbackColor(actionButtons[i], new Color(0.06f, 0.56f, 0.28f, 0.98f), new Color(0.29f, 0.87f, 0.50f, 1f));
                SetButtonLabel(actionButtons[i], $"[CORRECTA] <b>{label}</b>");
            }
            else if (i == chosenActionIdx && !isActionCorrect)
            {
                SetButtonFeedbackColor(actionButtons[i], new Color(0.68f, 0.14f, 0.18f, 0.98f), new Color(0.97f, 0.44f, 0.44f, 1f));
                SetButtonLabel(actionButtons[i], $"[INCORRECTA] <b>{label}</b>");
            }
            else
            {
                SetButtonFeedbackColor(actionButtons[i], new Color(0.07f, 0.11f, 0.18f, 0.75f), new Color(0.20f, 0.28f, 0.40f, 0.45f));
                SetButtonLabel(actionButtons[i], $"{icon} {label}");
            }
        }

        for (int i = 0; i < responsibleButtons.Length; i++)
        {
            if (responsibleButtons[i] == null) continue;
            responsibleButtons[i].interactable = false;
            string label = i < responsibles.Count ? responsibles[i] : "-";

            if (i == expectedRespIdx)
            {
                SetButtonFeedbackColor(responsibleButtons[i], new Color(0.06f, 0.56f, 0.28f, 0.98f), new Color(0.29f, 0.87f, 0.50f, 1f));
                SetButtonLabel(responsibleButtons[i], $"[CORRECTO] <b>{label}</b>");
            }
            else if (i == chosenRespIdx && !isRespCorrect)
            {
                SetButtonFeedbackColor(responsibleButtons[i], new Color(0.68f, 0.14f, 0.18f, 0.98f), new Color(0.97f, 0.44f, 0.44f, 1f));
                SetButtonLabel(responsibleButtons[i], $"[INCORRECTO] <b>{label}</b>");
            }
            else
            {
                SetButtonFeedbackColor(responsibleButtons[i], new Color(0.07f, 0.11f, 0.18f, 0.75f), new Color(0.20f, 0.28f, 0.40f, 0.45f));
                SetButtonLabel(responsibleButtons[i], $"[RESP] {label}");
            }
        }

        // Mostrar mensaje de confirmación o corrección directamente en el panel de la tarjeta
        if (isBothCorrect)
        {
            if (decisionTitleText != null)
            {
                decisionTitleText.text = $"<color=#4ADE80><b>[¡ELECCIÓN ACERTADA! - {day.day.ToString().ToUpper()}] ¡Tus 2 respuestas son correctas!</b></color>\n<size=18><color=#E2E8F0>Acción: <b>{correctAction}</b>   |   Responsable: <b>{correctResp}</b></color></size>";
            }
            if (confirmButton != null)
            {
                confirmButton.interactable = false;
                SetButtonFeedbackColor(confirmButton, new Color(0.06f, 0.56f, 0.28f, 0.98f), new Color(0.29f, 0.87f, 0.50f, 1f));
                SetButtonLabel(confirmButton, "<b>[OK] ¡ELECCIÓN ACERTADA! CONTINUANDO CON EL CASO...</b>");
            }
        }
        else
        {
            if (decisionTitleText != null)
            {
                decisionTitleText.text = $"<color=#F87171><b>[ELECCIÓN INCORRECTA - {day.day.ToString().ToUpper()}] Respuesta equivocada</b></color>\n<size=17><color=#4ADE80><b>RESPUESTAS CORRECTAS -></b></color> Acción: <b>{correctAction}</b>   |   Responsable: <b>{correctResp}</b></size>";
            }
            if (confirmButton != null)
            {
                confirmButton.interactable = false;
                SetButtonFeedbackColor(confirmButton, new Color(0.68f, 0.14f, 0.18f, 0.98f), new Color(0.97f, 0.44f, 0.44f, 1f));
                SetButtonLabel(confirmButton, $"<b>[INCORRECTO] Correcto era: {correctAction} + {correctResp}</b>");
            }
        }

        // Pausa breve en el panel para que el jugador lea claramente qué acertó o cuál era la respuesta correcta
        yield return new WaitForSeconds(isBothCorrect ? 2.1f : 2.8f);

        SetActiveSafe(decisionPanel, false);

        // Actualizar el letrero flotante 3D (ContextBubble) de la tarjeta con el resultado de la elección
        if (day.dayTarget != null && day.dayTarget.contextBubble != null)
        {
            string bubbleFeedback = isBothCorrect
                ? $"[ELECCION ACERTADA - {day.day.ToString().ToUpper()}] Correcto: {correctAction} | {correctResp}"
                : $"[CORRECCION - {day.day.ToString().ToUpper()}] Correcto era: {correctAction} | {correctResp}";
            day.dayTarget.contextBubble.ShowText(bubbleFeedback);
        }

        // Registrar las respuestas correctas validadas en el Plan (ActionPicker) para continuar el flujo normal
        mSelectedAction = expectedActionIdx;
        mSelectedResponsible = expectedRespIdx;

        if (gameManager != null)
        {
            gameManager.SetCurrentDay(day.day);
        }

        if (actionPicker != null)
        {
            actionPicker.SetSelection(correctAction, correctResp);
            actionPicker.ConfirmSelection();
        }

        if (hud != null)
        {
            hud.RefreshHUDLabels();
            if (hud.narrativeBannerText != null)
            {
                hud.narrativeBannerText.text = isBothCorrect
                    ? $"<color=#4ADE80><b>[ELECCIÓN ACERTADA · {day.day}]</b></color> Acción: <b>{correctAction}</b> | Responsable: <b>{correctResp}</b>"
                    : $"<color=#F87171><b>[RESPUESTA CORREGIDA · {day.day}]</b></color> Correcto era: <b>{correctAction}</b> | Responsable: <b>{correctResp}</b>";
            }
        }

        // Mostrar subtítulo narrativo confirmando el acierto o explicando la corrección antes de continuar con el caso
        state = StoryState.Narrating;
        if (isBothCorrect)
        {
            yield return ShowLine(new SubtitleLine(
                $"ELECCIÓN ACERTADA · {day.day.ToString().ToUpper()}",
                $"¡Excelente elección! Confirmaste correctamente que debía aplicarse '{correctAction}' a cargo de '{correctResp}'. Continuando con el caso...",
                "decision_correct"
            ), true);
        }
        else
        {
            yield return ShowLine(new SubtitleLine(
                $"RESPUESTA INCORRECTA · {day.day.ToString().ToUpper()}",
                $"Tu elección ('{chosenAction}' / '{chosenResp}') fue incorrecta. Las respuestas correctas eran: Acción '{correctAction}' y Responsable '{correctResp}'. Continuando con el caso...",
                "decision_wrong"
            ), true);
        }
    }

    /// <summary>
    /// Desenlace del Modo Ataque el Martes (MAR 9 a las 9:11 a.m. · Tarde).
    /// </summary>
    private IEnumerator AttackOutcomeRoutine(StoryDay martes)
    {
        state = StoryState.Outcome;

        if (incidentTimer != null)
        {
            incidentTimer.SetStoryClock(85f);
        }

        var lines = new List<SubtitleLine>
        {
            new SubtitleLine("MAR 9 · 9:11 A.M.", "Martes 9, 9:11 a.m. En Filipinas por fin se lee la orden de congelar los fondos...", "mar_outcome_1")
        };
        yield return PlayLines(lines);

        StartCoroutine(MoneyLeavesRoutine(martes));

        var lost = new List<SubtitleLine>
        {
            new SubtitleLine("CONGELADO: MARTES 9:11 · TARDE", "Ataque: la orden de congelar llega el martes. Es demasiado tarde: se acaban de retirar US$58 millones.", "mar_outcome_2"),
            new SubtitleLine("COMPARACIÓN", "CON PLAN: CONGELADO EL VIERNES. Ahora puedes cambiar a Modo Defensa para detener el ataque el viernes sin daños.", "mar_outcome_3")
        };
        yield return PlayLines(lost);

        state = StoryState.Finished;
        SetActiveSafe(subtitlePanel, false);

        if (gameManager != null)
        {
            gameManager.EndScenario();
        }
    }

    // ───────────────────────── Interacción con Objetos 3D en la Mesa ─────────────────────────

    public bool HandleObjectInteraction(ARInteractableObject obj)
    {
        if (obj == null || currentDayIndex < 0 || currentDayIndex >= days.Count) return false;

        StoryDay day = days[currentDayIndex];

        if (state == StoryState.Deciding)
        {
            return true;
        }

        if (state == StoryState.Narrating)
        {
            mTapRequested = true;
            return true;
        }

        if (state != StoryState.Investigating) return false;

        PlayClue();
        StartCoroutine(InspectRoutine(obj, day));
        return true;
    }

    private IEnumerator InspectRoutine(ARInteractableObject obj, StoryDay day)
    {
        state = StoryState.Narrating;
        bool isDefense = gameManager != null && gameManager.mode == SimMode.Defensa;

        string inspectionText;
        if (isDefense)
        {
            inspectionText = !string.IsNullOrEmpty(obj.defenseInspectionMessage)
                ? obj.defenseInspectionMessage
                : (!string.IsNullOrEmpty(obj.firstPersonResolvedMessage) ? obj.firstPersonResolvedMessage : obj.firstPersonInspectionMessage);
        }
        else
        {
            inspectionText = !string.IsNullOrEmpty(obj.attackInspectionMessage)
                ? obj.attackInspectionMessage
                : obj.firstPersonInspectionMessage;
        }

        if (string.IsNullOrEmpty(inspectionText))
        {
            inspectionText = $"Inspeccionas {obj.objectTitle} sobre la mesa.";
        }

        string speaker = isDefense ? $"DEFENSA ({obj.objectTitle.ToUpper()})" : $"TÚ ({obj.objectTitle.ToUpper()})";
        yield return ShowLine(new SubtitleLine(speaker, inspectionText), true);

        mKeyObjectInspected = true;
    }

    public void NotifyHudActionApplied(int actionIndex, string responsibleName)
    {
        if (state == StoryState.Investigating)
        {
            mKeyObjectInspected = true;
        }

        if (state == StoryState.Deciding || state == StoryState.Investigating)
        {
            mSelectedAction = Mathf.Clamp(actionIndex, 0, mActionOptions.Length - 1);
            var responsibles = GetResponsibles();
            for (int i = 0; i < responsibles.Count; i++)
            {
                if (string.Equals(responsibles[i], responsibleName, StringComparison.OrdinalIgnoreCase))
                {
                    mSelectedResponsible = i;
                    break;
                }
            }
            mDecisionConfirmed = true;
        }
    }

    // ───────────────────────── Selección de Decisiones ─────────────────────────

    public void SelectAction(int index)
    {
        if (state != StoryState.Deciding || mDecisionConfirmed) return;
        mSelectedAction = index;
        RefreshDecisionVisuals();
    }

    public void SelectResponsible(int index)
    {
        if (state != StoryState.Deciding || mDecisionConfirmed) return;
        mSelectedResponsible = index;
        RefreshDecisionVisuals();
    }

    public void ConfirmDecision()
    {
        if (state != StoryState.Deciding || mDecisionConfirmed) return;
        if (mSelectedAction < 0 || mSelectedResponsible < 0) return;
        mDecisionConfirmed = true;
    }

    private void RefreshDecisionVisuals()
    {
        for (int i = 0; i < actionButtons.Length; i++)
        {
            SetButtonColor(actionButtons[i], i == mSelectedAction);
        }
        for (int i = 0; i < responsibleButtons.Length; i++)
        {
            SetButtonColor(responsibleButtons[i], i == mSelectedResponsible);
        }
        if (confirmButton != null)
        {
            bool bothSelected = mSelectedAction >= 0 && mSelectedResponsible >= 0;
            confirmButton.interactable = bothSelected;
            if (bothSelected)
            {
                SetButtonFeedbackColor(confirmButton, new Color(0.06f, 0.50f, 0.32f, 0.98f), new Color(0.29f, 0.87f, 0.50f, 1f));
                SetButtonLabel(confirmButton, "<b>[OK] CONFIRMAR TUS 2 RESPUESTAS Y VERIFICAR</b>");
            }
            else
            {
                SetButtonFeedbackColor(confirmButton, new Color(0.14f, 0.20f, 0.30f, 0.92f), new Color(0.28f, 0.38f, 0.52f, 0.70f));
                SetButtonLabel(confirmButton, "<b>[SELECCIONA 1 ACCIÓN Y 1 RESPONSABLE ARRIBA PARA CONFIRMAR]</b>");
            }
        }
    }

    // ───────────────────────── Sistema de Subtítulos ─────────────────────────

    private IEnumerator PlayLines(List<SubtitleLine> lines)
    {
        if (lines == null) yield break;
        foreach (var line in lines)
        {
            yield return ShowLine(line, true);
        }
    }

    private IEnumerator ShowLine(SubtitleLine line, bool autoAdvance)
    {
        mBusyWithLine = true;
        SetActiveSafe(subtitlePanel, true);

        if (speakerText != null)
        {
            string spk = string.IsNullOrEmpty(line.speaker) ? "NARRADOR" : line.speaker.ToUpper();
            speakerText.text = $"[ {spk} ]";
        }
        if (continueHintText != null)
        {
            continueHintText.gameObject.SetActive(false);
        }

        if (subtitleText != null)
        {
            subtitleText.text = line.text;
            subtitleText.maxVisibleCharacters = 0;
            subtitleText.ForceMeshUpdate();
        }

        int total = subtitleText != null ? subtitleText.textInfo.characterCount : 0;
        float visible = 0f;
        float tickTimer = 0f;
        yield return null;
        mTapRequested = false;

        // Iniciar la locución por voz del narrador para este diálogo específico
        float speechDuration = 0f;
        if (narrator != null)
        {
            speechDuration = narrator.Speak(line);
        }
        else if (DialogueNarrator.I != null)
        {
            speechDuration = DialogueNarrator.I.Speak(line);
        }

        // Armonizar la velocidad de la máquina de escribir para que acompañe a la locución hablada
        float effectiveCPS = charactersPerSecond;
        float speechStartedAt = Time.time;
        if (speechDuration > 0.5f && total > 0)
        {
            float targetCPS = (float)total / Mathf.Max(1.0f, speechDuration * 0.85f);
            effectiveCPS = Mathf.Clamp(targetCPS, 22f, 65f);
        }

        while (subtitleText != null && visible < total)
        {
            if (mTapRequested)
            {
                mTapRequested = false;
                break;
            }
            visible += effectiveCPS * Time.deltaTime;
            tickTimer += Time.deltaTime;
            if (tickTimer >= 0.08f)
            {
                tickTimer = 0f;
                if (speechDuration <= 0f) PlayTypewriterTick();
            }
            subtitleText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(visible));
            yield return null;
        }

        if (subtitleText != null)
        {
            subtitleText.maxVisibleCharacters = int.MaxValue;
        }
        if (continueHintText != null)
        {
            continueHintText.gameObject.SetActive(true);
        }

        float baseHold = Mathf.Clamp(1.6f + total * 0.032f, 2.2f, 5.2f);
        float remainingSpeech = Mathf.Max(0f, speechDuration - (Time.time - speechStartedAt));
        float hold = autoAdvance ? (speechDuration > 0f ? Mathf.Max(0.65f, remainingSpeech + 0.35f) : baseHold) : float.MaxValue;
        float t = 0f;
        while (t < hold && !mTapRequested)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (narrator != null) narrator.StopSpeaking();
        else if (DialogueNarrator.I != null) DialogueNarrator.I.StopSpeaking();

        mTapRequested = false;
        mBusyWithLine = false;
    }

    private void ShowPersistentPrompt(string prompt)
    {
        if (narrator != null) narrator.StopSpeaking();
        else if (DialogueNarrator.I != null) DialogueNarrator.I.StopSpeaking();

        SetActiveSafe(subtitlePanel, true);
        if (speakerText != null) speakerText.text = "TU TURNO  /  INVESTIGA";
        if (subtitleText != null)
        {
            subtitleText.text = prompt;
            subtitleText.maxVisibleCharacters = int.MaxValue;
        }
        if (continueHintText != null)
        {
            continueHintText.gameObject.SetActive(false);
        }
    }

    // ───────────────────────── Efectos Visuales y Transiciones ─────────────────────────

    private IEnumerator FadeTransition(float from, float to, float duration)
    {
        if (transitionGroup == null) yield break;
        transitionGroup.gameObject.SetActive(true);
        transitionGroup.blocksRaycasts = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transitionGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        SetTransitionVisible(to > 0.5f, to);
    }

    private void SetTransitionVisible(bool visible, float alpha)
    {
        if (transitionGroup == null) return;
        transitionGroup.alpha = alpha;
        transitionGroup.blocksRaycasts = visible;
        transitionGroup.gameObject.SetActive(visible);
    }

    private IEnumerator ScaleRoutine(Transform target, Vector3 baseScale, float from, float to, float duration)
    {
        if (target == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = to > from
                ? 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f)
                : k * k * (3f - 2f * k);
            target.localScale = baseScale * Mathf.LerpUnclamped(from, to, eased);
            yield return null;
        }
        target.localScale = baseScale * to;
    }

    private void StartAlert(Renderer r)
    {
        StopAlert();
        if (r != null) mAlertRoutine = StartCoroutine(AlertBlinkRoutine(r));
    }

    private void StopAlert()
    {
        if (mAlertRoutine != null) StopCoroutine(mAlertRoutine);
        mAlertRoutine = null;
    }

    private IEnumerator AlertBlinkRoutine(Renderer r)
    {
        if (r == null || r.material == null) yield break;
        Color baseColor = r.material.HasProperty("_BaseColor") ? r.material.GetColor("_BaseColor") : r.material.color;
        while (r != null)
        {
            float k = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            SetRendererColor(r, Color.Lerp(baseColor * 0.35f, baseColor, k));
            yield return null;
        }
    }

    private IEnumerator MoneyLeavesRoutine(StoryDay martes)
    {
        PlayAttackAlert();
        if (martes.diorama == null) yield break;
        var money = new List<Transform>();
        foreach (Transform child in martes.diorama.transform)
        {
            if (child.name.StartsWith("Lote_Dinero")) money.Add(child);
        }

        float t = 0f;
        var startPos = new List<Vector3>();
        var startScale = new List<Vector3>();
        foreach (var m in money)
        {
            startPos.Add(m.localPosition);
            startScale.Add(m.localScale);
        }

        float exitDuration = 1.5f + Mathf.Max(0, money.Count - 1) * 0.18f;
        while (t < exitDuration)
        {
            t += Time.deltaTime;
            for (int i = 0; i < money.Count; i++)
            {
                if (money[i] == null) continue;
                float k = Mathf.Clamp01((t - i * 0.18f) / 1.5f);
                float eased = k * k * (3f - 2f * k);
                money[i].localPosition = startPos[i] + new Vector3(0.55f * eased, 2f * Mathf.Sin(k * Mathf.PI) + 3f * eased, -0.3f * eased);
                money[i].localScale = startScale[i] * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, k)));
            }
            yield return null;
        }
    }

    private static void SetRendererColor(Renderer r, Color c)
    {
        if (r == null || r.material == null) return;
        var mat = r.material;
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
    }

    private void StartBackgroundMusic()
    {
        if (backgroundMusicClip == null) return;
        if (musicSource == null)
        {
            var musicGo = transform.Find("MusicSource");
            if (musicGo != null)
            {
                musicSource = musicGo.GetComponent<AudioSource>();
            }
            if (musicSource == null)
            {
                var go = new GameObject("MusicSource");
                go.transform.SetParent(transform, false);
                musicSource = go.AddComponent<AudioSource>();
            }
        }
        musicSource.clip = backgroundMusicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0.34f;
        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void RegisterAllButtonsClickAudio()
    {
        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn == null) continue;
            btn.onClick.AddListener(PlayUiClick);
        }
    }

    public void PlayUiClick()
    {
        if (audioSource == null) return;
        AudioClip clip = uiClickClip != null ? uiClickClip : clueClip;
        if (clip != null) audioSource.PlayOneShot(clip, 0.75f);
    }

    private void PlayClue()
    {
        if (audioSource == null) return;
        AudioClip clip = cardScanClip != null ? cardScanClip : clueClip;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, 0.85f);
        }
    }

    private void PlayDayTransition()
    {
        if (audioSource == null) return;
        AudioClip clip = dayTransitionClip != null ? dayTransitionClip : clueClip;
        if (clip != null) audioSource.PlayOneShot(clip, 0.85f);
    }

    private void PlayDecisionResult(bool isCorrect)
    {
        if (audioSource == null) return;
        AudioClip clip = isCorrect
            ? (decisionCorrectClip != null ? decisionCorrectClip : clueClip)
            : (decisionWrongClip != null ? decisionWrongClip : clueClip);
        if (clip != null) audioSource.PlayOneShot(clip, 0.95f);
    }

    private void PlayAttackAlert()
    {
        if (audioSource == null) return;
        AudioClip clip = attackAlertClip != null ? attackAlertClip : clueClip;
        if (clip != null) audioSource.PlayOneShot(clip, 0.9f);
    }

    private void PlayDefenseVictory()
    {
        if (audioSource == null) return;
        AudioClip clip = defenseVictoryClip != null ? defenseVictoryClip : clueClip;
        if (clip != null) audioSource.PlayOneShot(clip, 0.95f);
    }

    private void PlayTypewriterTick()
    {
        // Si el narrador está hablando, silenciar el tecleo para que la locución se escuche con máxima claridad
        if (narrator != null && narrator.IsSpeaking) return;
        if (DialogueNarrator.I != null && DialogueNarrator.I.IsSpeaking) return;

        if (audioSource != null && typewriterTickClip != null)
        {
            audioSource.PlayOneShot(typewriterTickClip, 0.12f);
        }
    }

    // ───────────────────────── Navegación y Utilidades ─────────────────────────

    public void RestartStory()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void JumpToDay(int index)
    {
        mStagePlaced = true;
        SetActiveSafe(introPanel, false);
        SetActiveSafe(scanPanel, false);
        SetActiveSafe(decisionPanel, false);
        SetTransitionVisible(false, 0f);
        mBusyWithLine = false;

        if (mStoryRoutine != null) StopCoroutine(mStoryRoutine);
        StopAllCoroutines();

        mStoryRoutine = StartCoroutine(PlayDayRoutine(Mathf.Clamp(index, 0, days.Count - 1)));
    }

    private List<string> GetResponsibles()
    {
        if (gameManager != null && gameManager.currentScenarioData != null &&
            gameManager.currentScenarioData.responsibleNames != null &&
            gameManager.currentScenarioData.responsibleNames.Count >= 4)
        {
            return gameManager.currentScenarioData.responsibleNames;
        }
        return new List<string>(mDefaultResponsibles);
    }

    private static void ForceVisible(GameObject root)
    {
        if (root == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        foreach (var c in root.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }

    private static void SetButtonLabel(Button b, string label)
    {
        if (b == null) return;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = label;
    }

    private static void SetButtonColor(Button b, bool selected)
    {
        if (b == null || b.targetGraphic == null) return;
        var cb = b.colors;
        cb.disabledColor = Color.white;
        b.colors = cb;
        b.targetGraphic.color = selected ? new Color(0.08f, 0.42f, 0.72f, 0.98f) : new Color(0.09f, 0.14f, 0.24f, 0.92f);
        var outline = b.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = selected ? new Color(0.22f, 0.78f, 0.98f, 1f) : new Color(0.25f, 0.36f, 0.52f, 0.65f);
        }
    }

    private static void SetButtonFeedbackColor(Button b, Color bgColor, Color borderColor)
    {
        if (b == null || b.targetGraphic == null) return;
        var cb = b.colors;
        cb.disabledColor = Color.white;
        b.colors = cb;
        b.targetGraphic.color = bgColor;
        var outline = b.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = borderColor;
        }
    }

    /// <summary>
    /// Crea el guion cinemático completo de los días del Escenario 06 (Ataque 2016: VIE 5 -> SÁB 6 -> DOM 7 / LUN 8 -> MAR 9).
    /// </summary>
    public static List<StoryDay> CreateDefaultDays()
    {
        return new List<StoryDay>
        {
            new StoryDay
            {
                day = IncidentDay.Viernes,
                transitionTitle = "SIMULACIÓN · ESCENARIO 6 · ATAQUE 2016",
                transitionSubtitle = "VIE 5 (VIERNES)",
                transitionTagline = "Hugo Benavides ve la impresora apagada y cree que es una avería común",
                hoursSinceSignal = 0f,
                introLines = new List<SubtitleLine>
                {
                    new SubtitleLine("ATAQUE 2016 · VIE 5", "Viernes 5. Sala SWIFT del banco sobre tu mesa. Hugo Benavides ve la impresora de confirmaciones apagada.", "vie_intro_1"),
                    new SubtitleLine("HUGO BENAVIDES", "La impresora está apagada otra vez... seguro es una avería común. No hace falta reportarlo hoy.", "vie_intro_2")
                },
                investigatePrompt = ">> TOCA LA IMPRESORA APAGADA O A HUGO BENAVIDES sobre la mesa para inspeccionar qué ocurrió el Viernes 5.",
                attackLines = new List<SubtitleLine>
                {
                    new SubtitleLine("VIE 5 -> SÁB 6", "Sin plan de respuesta, Hugo no reporta la falla. Nadie revisa las transferencias y el ataque pasa al Sábado 6...", "vie_attack")
                },
                decisionQuestion = "MODO DEFENSA (VIE 5): Activa el Plan de Respuesta para congelar los fondos el viernes",
                correctActionIndex = 0,
                correctResponsible = "Hugo Benavides"
            },
            new StoryDay
            {
                day = IncidentDay.Sabado,
                transitionTitle = "AL DÍA SIGUIENTE · ATAQUE 2016",
                transitionSubtitle = "SÁB 6 (SÁBADO)",
                transitionTagline = "Aparece el error: 'A file is missing or changed'",
                hoursSinceSignal = 24f,
                introLines = new List<SubtitleLine>
                {
                    new SubtitleLine("ATAQUE 2016 · SÁB 6", "Sábado 6. Al intentar reiniciar el sistema aparece en el monitor: 'A file is missing or changed'.", "sab_intro_1"),
                    new SubtitleLine("NARRADOR", "Nadie investiga qué archivo del servidor SWIFT fue alterado.", "sab_intro_2")
                },
                investigatePrompt = ">> TOCA EL MONITOR CON ERROR ROJO O EL SERVIDOR sobre la mesa para ver lo ocurrido el Sábado 6.",
                attackLines = new List<SubtitleLine>
                {
                    new SubtitleLine("SÁB 6 -> DOM 7 -> LUN 8", "Pasa el Sábado 6 y todo el Domingo 7 sin conciliación automática. El caso avanza hasta el Lunes 8...", "sab_attack")
                },
                decisionQuestion = "SÁB 6: Aparece 'A file is missing or changed'",
                correctActionIndex = 1,
                correctResponsible = "Equipo de Respuesta a Incidentes"
            },
            new StoryDay
            {
                day = IncidentDay.Lunes,
                transitionTitle = "TRAS PASAR DOM 7 · ATAQUE 2016",
                transitionSubtitle = "LUN 8 (LUNES)",
                transitionTagline = "Piden congelar fondos, pero en Filipinas es feriado",
                hoursSinceSignal = 72f,
                introLines = new List<SubtitleLine>
                {
                    new SubtitleLine("ATAQUE 2016 · LUN 8", "Lunes 8. Tras perder todo el fin de semana (SÁB 6 y DOM 7), el banco intenta pedir que congelen los fondos.", "lun_intro_1"),
                    new SubtitleLine("BANCO EN FILIPINAS", "Hoy es feriado en Filipinas. Nadie contesta del otro lado.", "lun_intro_2")
                },
                investigatePrompt = ">> TOCA EL BANCO DE FILIPINAS O LA BARRERA DE FERIADO sobre la mesa para ver el bloqueo del Lunes 8.",
                attackLines = new List<SubtitleLine>
                {
                    new SubtitleLine("LUN 8 -> MAR 9", "Sin canal de escalamiento inmediato 24/7, la orden de congelar queda sin leerse hasta el Martes 9...", "lun_attack")
                },
                decisionQuestion = "LUN 8: Piden congelar fondos, pero en Filipinas es feriado",
                correctActionIndex = 2,
                correctResponsible = "Oficial de Seguridad (CISO)"
            },
            new StoryDay
            {
                day = IncidentDay.Martes,
                transitionTitle = "DESENLACE · ATAQUE 2016",
                transitionSubtitle = "MAR 9 · 9:11 A.M.",
                transitionTagline = "CONGELADO: MARTES 9:11 · TARDE (Retiran US$58 millones)",
                hoursSinceSignal = 85f,
                introLines = new List<SubtitleLine>
                {
                    new SubtitleLine("ATAQUE 2016 · MAR 9", "Martes 9, 9:11 a.m. Frente a ti en la mesa está la bóveda bancaria cuando por fin se lee la orden.", "mar_intro_1")
                },
                investigatePrompt = ">> TOCA LA BÓVEDA BANCARIA O LOS LOTES DE DINERO sobre la mesa para ver el desenlace del Martes 9.",
                correctActionIndex = -1
            }
        };
    }
}
