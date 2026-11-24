using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador integral del HUD en pantalla para el Escenario 06 en Android / AR.
/// Permite alternar entre Modo Ataque y Defensa, inspeccionar los 4 días (con o sin marcador físico),
/// asignar responsables, aplicar las 3 respuestas clave y abrir el Plan de Respuesta.
/// </summary>
public class ScenarioHUDController : MonoBehaviour
{
    [Header("Referencias del Sistema")]
    public GameManager gameManager;
    public ActionPicker actionPicker;
    public IncidentTimer incidentTimer;
    public PlanBuilder planBuilder;
    public List<DayTarget> dayTargets = new List<DayTarget>();

    [Header("Elementos de UI del HUD")]
    public TMP_Text statusHeaderText;
    public TMP_Text narrativeBannerText;
    public TMP_Text modeButtonText;
    public TMP_Text responsibleButtonText;

    private int mCurrentResponsibleIndex = 0;
    private readonly string[] mDefaultResponsibles = new[]
    {
        "Hugo Benavides",
        "Equipo de Respuesta a Incidentes",
        "Oficial de Seguridad (CISO)",
        "Operaciones SWIFT"
    };

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();

        // Inicializar con el primer responsable por defecto
        string initialResp = GetCurrentResponsibleName();
        if (actionPicker != null)
        {
            actionPicker.SelectResponsible(initialResp);
        }

        if (incidentTimer != null)
        {
            incidentTimer.UpdateTimerDisplay();
        }

        // Si no hay StoryDirector activo, activar el día Viernes directamente
        if (FindAnyObjectByType<StoryDirector>() == null)
        {
            SelectDay((int)IncidentDay.Viernes);
        }
        RefreshHUDLabels();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        }

        if (actionPicker == null)
        {
            actionPicker = FindAnyObjectByType<ActionPicker>();
        }

        if (incidentTimer == null)
        {
            incidentTimer = FindAnyObjectByType<IncidentTimer>();
        }

        if (planBuilder == null)
        {
            planBuilder = FindAnyObjectByType<PlanBuilder>(FindObjectsInactive.Include);
        }

        if (dayTargets == null || dayTargets.Count == 0)
        {
            dayTargets = new List<DayTarget>(FindObjectsByType<DayTarget>(FindObjectsSortMode.None));
        }
    }

    /// <summary>
    /// Abre la pantalla de selección para que el usuario elija explícitamente entre Modo Ataque y Modo Defensa.
    /// </summary>
    public void OpenModeSelection()
    {
        if (StoryDirector.I != null)
        {
            StoryDirector.I.ShowModeSelectionScreen();
            return;
        }

        ToggleSimMode();
    }

    /// <summary>
    /// Alterna entre SimMode.Ataque y SimMode.Defensa.
    /// </summary>
    public void ToggleSimMode()
    {
        ResolveReferences();
        if (gameManager == null) return;

        SimMode targetMode = (gameManager.mode == SimMode.Ataque) ? SimMode.Defensa : SimMode.Ataque;
        gameManager.mode = targetMode;

        if (StoryDirector.I != null)
        {
            if (targetMode == SimMode.Defensa)
            {
                StoryDirector.I.StartDefenseMode();
            }
            else
            {
                StoryDirector.I.StartAttackMode();
            }
            return;
        }

        RefreshHUDLabels();

        if (narrativeBannerText != null)
        {
            string modeDesc = (gameManager.mode == SimMode.Defensa)
                ? "MODO DEFENSA (CON PLAN): Todo acaba el Viernes (VIE 5) y no pasa ningún daño."
                : "MODO ATAQUE (2016): Recorre VIE 5 -> SÁB 6 -> DOM 7 -> LUN 8 -> MAR 9 (Congelado Martes 9:11 · Tarde).";
            narrativeBannerText.text = modeDesc;
        }
    }

    /// <summary>
    /// Selecciona y muestra en pantalla (y en el espacio 3D frente a la cámara) uno de los 4 días.
    /// </summary>
    /// <param name="dayIndex">0=Viernes, 1=Sabado, 2=Lunes, 3=Martes</param>
    public void SelectDay(int dayIndex)
    {
        ResolveReferences();
        IncidentDay selectedDay = (IncidentDay)Mathf.Clamp(dayIndex, 0, 3);

        if (gameManager != null)
        {
            gameManager.SetCurrentDay(selectedDay);
        }

        DayTarget activeTarget = null;
        foreach (var dt in dayTargets)
        {
            if (dt == null) continue;
            if (dt.targetDay == selectedDay)
            {
                activeTarget = dt;
            }
            else
            {
                dt.OnTargetLost();
            }
        }

        if (activeTarget != null)
        {
            activeTarget.PositionInFrontOfCamera();
            activeTarget.OnTargetFound();

            if (narrativeBannerText != null && !string.IsNullOrEmpty(activeTarget.narrativeText))
            {
                narrativeBannerText.text = $"<b>[{selectedDay}]</b> {activeTarget.narrativeText}";
            }
        }

        if (StoryDirector.I != null && StoryDirector.I.CurrentDayIndex != dayIndex)
        {
            StoryDirector.I.JumpToDay(dayIndex);
        }

        RefreshHUDLabels();
    }

    public void SelectViernes() => SelectDay(0);
    public void SelectSabado() => SelectDay(1);
    public void SelectLunes() => SelectDay(2);
    public void SelectMartes() => SelectDay(3);

    /// <summary>
    /// Cambia al siguiente responsable disponible en la lista del escenario.
    /// </summary>
    public void CycleResponsible()
    {
        ResolveReferences();
        var list = GetResponsiblesList();
        mCurrentResponsibleIndex = (mCurrentResponsibleIndex + 1) % list.Count;

        string selectedResp = list[mCurrentResponsibleIndex];
        if (actionPicker != null)
        {
            actionPicker.SelectResponsible(selectedResp);
        }

        RefreshHUDLabels();
    }

    /// <summary>
    /// Aplica una de las 3 opciones de respuesta clave al día activo.
    /// </summary>
    public void ApplyKeyActionByIndex(int actionIndex)
    {
        ResolveReferences();
        if (gameManager == null || actionPicker == null) return;

        // Si no estamos en modo Defensa, no aplicar acciones defensivas para no alterar el Modo Ataque
        if (gameManager.mode != SimMode.Defensa)
        {
            return;
        }

        string[] keyActions = GameManager.RequiredKeyDefenses;
        int idx = Mathf.Clamp(actionIndex, 0, keyActions.Length - 1);
        string chosenAction = keyActions[idx];
        string chosenResp = GetCurrentResponsibleName();

        // Si el StoryDirector está esperando la decisión de este día, delegar la confirmación para que avance la cinemática
        if (StoryDirector.I != null &&
            (StoryDirector.I.State == StoryDirector.StoryState.Deciding ||
             StoryDirector.I.State == StoryDirector.StoryState.Investigating))
        {
            StoryDirector.I.NotifyHudActionApplied(idx, chosenResp);
        }
        else
        {
            actionPicker.SetSelection(chosenAction, chosenResp);
            actionPicker.ConfirmSelection();
        }

        if (planBuilder != null && planBuilder.gameObject.activeSelf)
        {
            planBuilder.RefreshPlanDisplay();
        }

        if (narrativeBannerText != null)
        {
            narrativeBannerText.text = $"<color=#4ADE80><b>[Acción Registrada en {gameManager.currentDay}]</b></color> '{chosenAction}' | Responsable: {chosenResp}";
        }

        RefreshHUDLabels();
    }

    public void ApplyActionPlanRoles() => ApplyKeyActionByIndex(0);
    public void ApplyActionConciliacion() => ApplyKeyActionByIndex(1);
    public void ApplyActionEscalamiento() => ApplyKeyActionByIndex(2);

    /// <summary>
    /// Abre o cierra el panel del Plan de Respuesta (Panel_Plan).
    /// </summary>
    public void TogglePlanPanel()
    {
        ResolveReferences();
        if (planBuilder == null) return;

        if (planBuilder.gameObject.activeSelf)
        {
            planBuilder.HidePlan();
        }
        else
        {
            planBuilder.ShowPlan();
        }
    }

    public void RefreshHUDLabels()
    {
        if (gameManager == null) return;

        string dayBadge = "VIE 5 - DIA 1";
        switch (gameManager.currentDay)
        {
            case IncidentDay.Viernes: dayBadge = "VIE 5 - SALA SWIFT"; break;
            case IncidentDay.Sabado: dayBadge = "SAB 6 - SERVIDOR"; break;
            case IncidentDay.Lunes: dayBadge = "LUN 8 - FILIPINAS"; break;
            case IncidentDay.Martes: dayBadge = "MAR 9 - 9:11 A.M."; break;
        }

        if (statusHeaderText != null)
        {
            if (gameManager.mode == SimMode.Defensa)
            {
                statusHeaderText.text = $"<color=#59DEC9>06 / DEFENSA</color>\n<b>{dayBadge}</b>";
            }
            else
            {
                statusHeaderText.text = $"<color=#F4AA8B>06 / ATAQUE 2016</color>\n<b>{dayBadge}</b>";
            }
        }

        if (modeButtonText != null)
        {
            if (gameManager.mode == SimMode.Defensa)
            {
                modeButtonText.text = "Cambiar modo";
            }
            else
            {
                modeButtonText.text = "Cambiar modo";
            }
        }

        if (responsibleButtonText != null)
        {
            responsibleButtonText.text = $"Resp: {GetCurrentResponsibleName()}";
        }
    }

    private List<string> GetResponsiblesList()
    {
        if (gameManager != null &&
            gameManager.currentScenarioData != null &&
            gameManager.currentScenarioData.responsibleNames != null &&
            gameManager.currentScenarioData.responsibleNames.Count > 0)
        {
            return gameManager.currentScenarioData.responsibleNames;
        }
        return new List<string>(mDefaultResponsibles);
    }

    private string GetCurrentResponsibleName()
    {
        var list = GetResponsiblesList();
        if (list.Count == 0) return "Equipo CSIRT";
        mCurrentResponsibleIndex = Mathf.Clamp(mCurrentResponsibleIndex, 0, list.Count - 1);
        return list[mCurrentResponsibleIndex];
    }
}
