using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestiona la selección interactiva de acciones correctivas y sus responsables asignados.
/// Almacena las decisiones tomadas durante el incidente y las notifica al GameManager.
/// </summary>
public class ActionPicker : MonoBehaviour
{
    [Serializable]
    public class ActionRecord
    {
        public string actionId;
        public string responsibleName;
        public IncidentDay day;
        public DateTime timestamp;
        public bool isEffective;

        public ActionRecord(string actionId, string responsibleName, IncidentDay day, bool isEffective)
        {
            this.actionId = actionId;
            this.responsibleName = responsibleName;
            this.day = day;
            this.timestamp = DateTime.Now;
            this.isEffective = isEffective;
        }
    }

    [Header("Referencias")]
    [Tooltip("Referencia al GameManager del escenario. Si está vacía, se auto-asignará en Awake.")]
    [SerializeField] private GameManager gameManager;
    public GameManager GameManager => gameManager;

    [Header("Selección Activa")]
    [SerializeField] private string currentActionId = string.Empty;
    [SerializeField] private string currentResponsible = string.Empty;

    [Header("Historial de Acciones Tomadas")]
    [SerializeField] private List<ActionRecord> actionHistory = new List<ActionRecord>();

    [Header("Eventos")]
    public UnityEvent<string, string> onSelectionConfirmed = new UnityEvent<string, string>();

    // Getters públicos
    public string CurrentActionId => currentActionId;
    public string CurrentResponsible => currentResponsible;
    public IReadOnlyList<ActionRecord> ActionHistory => actionHistory;

    private void Awake()
    {
        // Enlazar referencia al GameManager si no fue asignada manualmente
        if (gameManager == null)
        {
            gameManager = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        }
    }

    /// <summary>
    /// Asigna la referencia al GameManager de manera programática.
    /// </summary>
    public void SetGameManager(GameManager gm)
    {
        gameManager = gm;
    }

    /// <summary>
    /// Establece la acción correctiva seleccionada por el jugador.
    /// </summary>
    /// <param name="actionId">Identificador de la acción.</param>
    public void SelectAction(string actionId)
    {
        currentActionId = actionId;
        Debug.Log($"[ActionPicker] Acción seleccionada: '{actionId}'");
    }

    /// <summary>
    /// Establece el responsable seleccionado para ejecutar la acción.
    /// </summary>
    /// <param name="responsibleName">Nombre o rol del responsable.</param>
    public void SelectResponsible(string responsibleName)
    {
        currentResponsible = responsibleName;
        Debug.Log($"[ActionPicker] Responsable seleccionado: '{responsibleName}'");
    }

    /// <summary>
    /// Asigna tanto la acción como el responsable simultáneamente.
    /// </summary>
    public void SetSelection(string actionId, string responsibleName)
    {
        SelectAction(actionId);
        SelectResponsible(responsibleName);
    }

    /// <summary>
    /// Limpia el historial de decisiones al reiniciar o cambiar de modo.
    /// </summary>
    public void ClearHistory()
    {
        actionHistory.Clear();
        currentActionId = string.Empty;
        currentResponsible = string.Empty;
    }

    /// <summary>
    /// Guarda la elección actual, la registra en el historial y notifica al GameManager.
    /// </summary>
    public void ConfirmSelection()
    {
        if (string.IsNullOrEmpty(currentActionId))
        {
            Debug.LogWarning("[ActionPicker] No se puede confirmar: No hay ninguna acción seleccionada.");
            return;
        }

        // Obtener el día actual según el GameManager si está disponible
        IncidentDay activeDay = gameManager != null ? gameManager.currentDay : IncidentDay.Viernes;

        // Verificar si la acción es efectiva según el ScenarioData
        bool effective = CheckIfActionIsEffective(currentActionId);

        // Reemplazar si ya existía una elección para este día y mantener orden cronológico (Viernes -> Martes)
        actionHistory.RemoveAll(r => r.day == activeDay);
        var record = new ActionRecord(currentActionId, currentResponsible, activeDay, effective);
        actionHistory.Add(record);
        actionHistory.Sort((a, b) => a.day.CompareTo(b.day));

        // Notificar al GameManager
        NotifyGameManager(currentActionId);

        // Disparar evento de confirmación
        onSelectionConfirmed?.Invoke(currentActionId, currentResponsible);

        Debug.Log($"[ActionPicker] Elección confirmada: Acción='{currentActionId}', Responsable='{currentResponsible}', Día={activeDay}");
    }

    private void NotifyGameManager(string actionId)
    {
        if (gameManager == null)
        {
            gameManager = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.ApplyDefense(actionId);
        }
        else
        {
            Debug.LogWarning("[ActionPicker] No se pudo notificar al GameManager (no encontrado en la escena).");
        }
    }

    private bool CheckIfActionIsEffective(string actionId)
    {
        if (gameManager != null && gameManager.currentScenarioData != null)
        {
            var match = gameManager.currentScenarioData.possibleCorrectiveActions
                .Find(a => string.Equals(a.id, actionId, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(a.title, actionId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match.isEffective && match.expectedDay == gameManager.currentDay;
            }
        }
        return true;
    }
}

