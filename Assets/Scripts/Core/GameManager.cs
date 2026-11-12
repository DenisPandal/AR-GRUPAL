using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controlador principal del ciclo de vida y estado de la simulación del Escenario 06.
/// Implementa el patrón Singleton accesible mediante GameManager.I.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager I;

    [Serializable]
    public struct DayDefenseEntry
    {
        public IncidentDay day;
        public string defenseId;

        public DayDefenseEntry(IncidentDay day, string defenseId)
        {
            this.day = day;
            this.defenseId = defenseId;
        }
    }

    [Header("Datos del Escenario")]
    [Tooltip("Referencia al ScriptableObject con la configuración y cronología del escenario.")]
    public ScenarioData currentScenarioData;

    [Header("Modo de Simulación")]
    [Tooltip("Define si el usuario está explorando en modo Ataque o implementando medidas en modo Defensa.")]
    public SimMode mode = SimMode.Ataque;

    [Header("Día Activo en AR")]
    [Tooltip("Día del incidente que el jugador está visualizando actualmente mediante el marcador AR.")]
    public IncidentDay currentDay = IncidentDay.Viernes;

    [Header("Referencias del Sistema")]
    public ActionPicker actionPicker;
    public PlanBuilder planBuilder;

    [Header("Estado de la Partida")]
    [SerializeField] private List<string> registeredEvidences = new List<string>();
    [SerializeField] private List<DayDefenseEntry> appliedDefenses = new List<DayDefenseEntry>();
    [SerializeField] private string lastScenarioResult = string.Empty;
    [SerializeField] private bool isVictory = false;

    [Header("Eventos de Fin de Escenario")]
    public UnityEvent<string> onScenarioEnded = new UnityEvent<string>();

    // Las 3 opciones de respuesta clave requeridas en modo Defensa
    public static readonly string[] RequiredKeyDefenses = new[]
    {
        "Plan de respuesta con roles",
        "Conciliación diaria automática",
        "Escalamiento inmediato"
    };

    public string LastScenarioResult => lastScenarioResult;
    public bool IsVictory => isVictory;
    public IReadOnlyList<DayDefenseEntry> AppliedDefenses => appliedDefenses;

    private void Awake()
    {
        // Forzar orientación Horizontal (Landscape) en dispositivos móviles
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

        if (I != null && I != this)
        {
            Debug.LogWarning("[GameManager] Instancia duplicada detectada. Destruyendo este GameObject.");
            Destroy(gameObject);
            return;
        }

        I = this;

        if (actionPicker == null)
        {
            actionPicker = FindAnyObjectByType<ActionPicker>();
        }

        if (planBuilder == null)
        {
            planBuilder = FindAnyObjectByType<PlanBuilder>(FindObjectsInactive.Include);
        }
    }

    /// <summary>
    /// Actualiza el día del incidente que el jugador está visualizando a través del marcador de Vuforia.
    /// </summary>
    /// <param name="day">Día detectado por el marcador AR.</param>
    public void SetCurrentDay(IncidentDay day)
    {
        currentDay = day;
        Debug.Log($"[GameManager] Visualizando día en AR: {day} | Modo: {mode}");
    }

    /// <summary>
    /// Registra una evidencia o indicio descubierto durante la investigación.
    /// </summary>
    /// <param name="id">Identificador único de la evidencia.</param>
    public void RegisterEvidence(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && !registeredEvidences.Contains(id))
        {
            registeredEvidences.Add(id);
        }
        Debug.Log($"[GameManager] Evidencia registrada con éxito: '{id}' | Modo actual: {mode}");
    }

    /// <summary>
    /// Aplica una acción correctiva o de defensa en el sistema simulado para el día actual.
    /// </summary>
    /// <param name="id">Identificador o título de la acción defensiva.</param>
    public void ApplyDefense(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            // Reemplazar si ya existía una defensa para este mismo día e id o agregarla
            appliedDefenses.RemoveAll(e => e.day == currentDay && string.Equals(e.defenseId, id, StringComparison.OrdinalIgnoreCase));
            appliedDefenses.Add(new DayDefenseEntry(currentDay, id));
        }
        Debug.Log($"[GameManager] Acción defensiva aplicada con éxito: '{id}' en día {currentDay} | Modo actual: {mode}");
    }

    /// <summary>
    /// Evalúa si el jugador seleccionó las 3 respuestas clave en los días correctos.
    /// </summary>
    public bool ValidateDefenseResponses()
    {
        // Consolidar elecciones desde appliedDefenses y ActionPicker
        var entries = new List<DayDefenseEntry>(appliedDefenses);

        if (actionPicker == null)
        {
            actionPicker = FindAnyObjectByType<ActionPicker>();
        }

        if (actionPicker != null && actionPicker.ActionHistory != null)
        {
            foreach (var rec in actionPicker.ActionHistory)
            {
                bool alreadyPresent = entries.Exists(e =>
                    e.day == rec.day &&
                    string.Equals(e.defenseId, rec.actionId, StringComparison.OrdinalIgnoreCase));

                if (!alreadyPresent)
                {
                    entries.Add(new DayDefenseEntry(rec.day, rec.actionId));
                }
            }
        }

        // Si el ScenarioData define las acciones correctivas y sus días esperados, validamos contra ellas
        if (currentScenarioData != null && currentScenarioData.possibleCorrectiveActions != null && currentScenarioData.possibleCorrectiveActions.Count >= 3)
        {
            foreach (var requiredAction in currentScenarioData.possibleCorrectiveActions)
            {
                if (!requiredAction.isEffective) continue;

                bool matchedInCorrectDay = entries.Exists(e =>
                    e.day == requiredAction.expectedDay &&
                    (string.Equals(e.defenseId, requiredAction.title, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.defenseId, requiredAction.id, StringComparison.OrdinalIgnoreCase)));

                if (!matchedInCorrectDay)
                {
                    return false;
                }
            }
            return true;
        }

        // Validación por defecto de las 3 respuestas clave en Viernes, Sábado y Lunes
        bool hasPlanRoles = entries.Exists(e =>
            e.day == IncidentDay.Viernes &&
            e.defenseId.IndexOf("Plan de respuesta con roles", StringComparison.OrdinalIgnoreCase) >= 0);

        bool hasConciliacion = entries.Exists(e =>
            e.day == IncidentDay.Sabado &&
            e.defenseId.IndexOf("Conciliación diaria automática", StringComparison.OrdinalIgnoreCase) >= 0);

        bool hasEscalamiento = entries.Exists(e =>
            e.day == IncidentDay.Lunes &&
            e.defenseId.IndexOf("Escalamiento inmediato", StringComparison.OrdinalIgnoreCase) >= 0);

        return hasPlanRoles && hasConciliacion && hasEscalamiento;
    }

    /// <summary>
    /// Reinicia el estado de evidencias y defensas al cambiar entre Modo Ataque y Modo Defensa.
    /// </summary>
    public void ResetScenarioState()
    {
        registeredEvidences.Clear();
        appliedDefenses.Clear();
        lastScenarioResult = string.Empty;
        isVictory = false;
        if (actionPicker != null)
        {
            actionPicker.ClearHistory();
        }
    }

    /// <summary>
    /// Activa el Plan de Respuesta completo en Modo Defensa el día Viernes (VIE 5) para congelar los fondos ese mismo viernes sin daños.
    /// </summary>
    public void ApplyCompleteFridayDefensePlan(string primaryResponsible = "Hugo Benavides")
    {
        mode = SimMode.Defensa;
        appliedDefenses.Clear();
        appliedDefenses.Add(new DayDefenseEntry(IncidentDay.Viernes, "Plan de respuesta con roles"));
        appliedDefenses.Add(new DayDefenseEntry(IncidentDay.Sabado, "Conciliación diaria automática"));
        appliedDefenses.Add(new DayDefenseEntry(IncidentDay.Lunes, "Escalamiento inmediato"));

        if (actionPicker != null)
        {
            actionPicker.ClearHistory();
            currentDay = IncidentDay.Viernes;
            actionPicker.SetSelection("Plan de respuesta con roles", string.IsNullOrEmpty(primaryResponsible) ? "Hugo Benavides" : primaryResponsible);
            actionPicker.ConfirmSelection();

            currentDay = IncidentDay.Sabado;
            actionPicker.SetSelection("Conciliación diaria automática", "Equipo de Respuesta a Incidentes");
            actionPicker.ConfirmSelection();

            currentDay = IncidentDay.Lunes;
            actionPicker.SetSelection("Escalamiento inmediato", "Oficial de Seguridad (CISO)");
            actionPicker.ConfirmSelection();

            currentDay = IncidentDay.Viernes;
        }
    }

    /// <summary>
    /// Finaliza la simulación del Escenario 06 y evalúa las reglas de victoria/derrota según el modo.
    /// </summary>
    public void EndScenario()
    {
        if (mode == SimMode.Ataque)
        {
            // Regla de Ataque: La orden de congelar llega el martes y el dinero se pierde
            isVictory = false;
            lastScenarioResult = "CONGELADO: MARTES 9:11 · TARDE — Ataque: la orden de congelar llega el martes. Retiran US$58 millones.";
        }
        else if (mode == SimMode.Defensa)
        {
            // Regla de Defensa: En modo defensa con el plan activo el viernes, todo acaba el viernes sin daños
            if (ValidateDefenseResponses())
            {
                isVictory = true;
                lastScenarioResult = "CON PLAN: CONGELADO EL VIERNES — Congelado el viernes. Dinero salvado (Todo acaba el viernes y no pasa ningún daño).";
            }
            else
            {
                isVictory = false;
                lastScenarioResult = "No se completó el plan de respuesta el viernes. La orden de congelar llega el martes y el dinero se pierde.";
            }
        }

        Debug.Log($"[GameManager] Fin del Escenario 06 | Modo: {mode} | Victoria: {isVictory} | Resultado: {lastScenarioResult}");

        // Actualizar UI del Plan si existe en la escena
        if (planBuilder == null)
        {
            planBuilder = FindAnyObjectByType<PlanBuilder>(FindObjectsInactive.Include);
        }

        if (planBuilder != null)
        {
            planBuilder.ShowFinalResult(lastScenarioResult, isVictory);
        }

        onScenarioEnded?.Invoke(lastScenarioResult);
    }
}
