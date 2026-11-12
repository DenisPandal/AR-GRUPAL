using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Recopila las decisiones tomadas por el jugador durante la simulación y construye
/// un resumen detallado del Plan de Respuesta a Incidentes en formato de texto.
/// </summary>
public class PlanBuilder : MonoBehaviour
{
    [Header("Componentes de UI")]
    [Tooltip("Componente TextMeshPro donde se renderizará el resumen del plan de respuesta.")]
    public TMP_Text planText;

    [Tooltip("Botón final dentro de Panel_Plan encargado de ejecutar GameManager.I.EndScenario().")]
    public Button finalButton;

    [Header("Referencias Lógicas")]
    [Tooltip("Referencia al selector de acciones y responsables.")]
    public ActionPicker actionPicker;

    [Tooltip("Referencia al calculador de impacto temporal del incidente.")]
    public IncidentTimer incidentTimer;

    private string mLastOutcomeMessage = string.Empty;

    private void Awake()
    {
        ResolveReferences();

        if (finalButton != null)
        {
            finalButton.onClick.RemoveListener(TriggerEndScenario);
            finalButton.onClick.AddListener(TriggerEndScenario);
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshPlanDisplay();
    }

    private void ResolveReferences()
    {
        if (planText == null)
        {
            planText = GetComponentInChildren<TMP_Text>(true);
        }

        if (finalButton == null)
        {
            finalButton = GetComponentInChildren<Button>(true);
        }

        if (actionPicker == null)
        {
            actionPicker = FindAnyObjectByType<ActionPicker>();
        }

        if (incidentTimer == null)
        {
            incidentTimer = FindAnyObjectByType<IncidentTimer>();
        }
    }

    /// <summary>
    /// Método invocado por el botón final del plan para ejecutar el Plan de Respuesta y ver el nuevo resultado (Paso 6).
    /// </summary>
    public void TriggerEndScenario()
    {
        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();

        // Si estamos en Modo Ataque, el botón final ejecuta el Paso 6: aplicar el plan armado en Modo Defensa y ver el nuevo resultado (Congelado el Viernes)
        if (gm != null && gm.mode == SimMode.Ataque && StoryDirector.I != null)
        {
            HidePlan();
            StoryDirector.I.ApplyPlayerPlanAndShowDefenseOutcome();
            return;
        }

        if (gm != null)
        {
            gm.EndScenario();
        }
        else
        {
            Debug.LogWarning("[PlanBuilder] GameManager.I no encontrado al presionar el botón final.");
        }
    }

    /// <summary>
    /// Muestra el resultado final evaluado por GameManager.EndScenario() en el panel.
    /// </summary>
    public void ShowFinalResult(string resultMessage, bool isVictory)
    {
        string colorHex = isVictory ? "#4ADE80" : "#F87171";
        mLastOutcomeMessage = $"\n<b>NUEVO RESULTADO (RF-04 / RF-05):</b>\n<color={colorHex}>{resultMessage}</color>";
        gameObject.SetActive(true);
        RefreshPlanDisplay();
    }

    /// <summary>
    /// Activa el panel del plan y actualiza el resumen de decisiones.
    /// </summary>
    public void ShowPlan()
    {
        gameObject.SetActive(true);
        RefreshPlanDisplay();
    }

    /// <summary>
    /// Oculta el panel del plan de respuesta.
    /// </summary>
    public void HidePlan()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Recopila las decisiones del jugador y actualiza el texto del plan en pantalla.
    /// </summary>
    public void RefreshPlanDisplay()
    {
        if (planText == null)
        {
            Debug.LogWarning("[PlanBuilder] No hay un componente TMP_Text asignado a 'planText'.");
            return;
        }

        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        bool isDefense = gm != null && gm.mode == SimMode.Defensa;

        if (finalButton != null)
        {
            var btnLabel = finalButton.GetComponentInChildren<TMP_Text>(true);
            if (btnLabel != null)
            {
                btnLabel.text = isDefense
                    ? "<b>✓ VERIFICAR RESULTADO FINAL DEL PLAN</b>"
                    : "<b>▶ VER NUEVO RESULTADO CON ESTE PLAN (MODO DEFENSA)</b>";
            }
        }

        planText.text = GeneratePlanSummary();
    }

    /// <summary>
    /// Construye la cadena de texto formateada con el resumen de acciones, responsables y tiempo perdido (RF-03, RF-04, RF-05).
    /// </summary>
    public string GeneratePlanSummary()
    {
        var sb = new StringBuilder();
        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        bool isDefense = gm != null && gm.mode == SimMode.Defensa;

        if (!isDefense)
        {
            sb.AppendLine("<size=30><color=#38BDF8><b>[DOSSIER EJECUTIVO - PLAN DE RESPUESTA (RF-05)]</b></color></size>");
            sb.AppendLine("<color=#475569>--------------------------------------------------------</color>");
            sb.AppendLine("<b>ESTADO ACTUAL:</b> <color=#F87171><b>MODO ATAQUE 2016 (VIE 5 -> SAB 6 -> LUN 8 -> MAR 9)</b></color>");
            sb.AppendLine("  - <b>Cronometro (RF-03):</b> <color=#F87171><b>85 horas perdidas - CONGELADO: MARTES 9:11 - TARDE</b></color>");
            sb.AppendLine("  - <b>Impacto sin plan:</b> La orden llega el martes y se retiran <b>US$58 millones</b>.\n");
        }
        else
        {
            sb.AppendLine("<size=30><color=#4ADE80><b>[DOSSIER EJECUTIVO - PLAN DE RESPUESTA ACTIVO (RF-05)]</b></color></size>");
            sb.AppendLine("<color=#475569>--------------------------------------------------------</color>");
            sb.AppendLine("<b>ESTADO ACTUAL:</b> <color=#4ADE80><b>MODO DEFENSA - CONGELADO EL VIERNES (VIE 5)</b></color>");
            sb.AppendLine("  - <b>Cronometro (RF-03):</b> <color=#4ADE80><b>00 horas perdidas - CON PLAN: CONGELADO EL VIERNES</b></color>");
            sb.AppendLine("  - <b>Impacto con plan (RF-04):</b> El congelamiento ocurre el viernes y <b>el dinero NO sale (US$58M salvados)</b>.\n");
        }

        sb.AppendLine("<color=#38BDF8><b>PASOS 4 Y 5: ACCIONES ORDENADAS Y RESPONSABLES ASIGNADOS (RF-02):</b></color>");
        if (actionPicker != null && actionPicker.ActionHistory != null && actionPicker.ActionHistory.Count > 0)
        {
            int index = 1;
            foreach (var record in actionPicker.ActionHistory)
            {
                string responsible = string.IsNullOrWhiteSpace(record.responsibleName)
                    ? "Sin asignar"
                    : record.responsibleName;
                string statusTag = record.isEffective ? "<color=#4ADE80><b>[EFECTIVA]</b></color>" : "<color=#FBBF24><b>[REVISAR]</b></color>";
                sb.AppendLine($"   <b>{index}. [{record.day.ToString().ToUpper()}]</b> {statusTag} <b>{record.actionId}</b> <color=#94A3B8>|</color> Resp: <color=#E2E8F0>{responsible}</color>");
                index++;
            }
        }
        else
        {
            sb.AppendLine("   <color=#94A3B8><i>Aún no se han registrado acciones. Escanea e interactúa con las tarjetas para elegir qué debió hacerse y quién debía hacerlo.</i></color>");
        }

        if (!string.IsNullOrEmpty(mLastOutcomeMessage))
        {
            sb.AppendLine("<color=#475569>--------------------------------------------------------</color>");
            sb.AppendLine(mLastOutcomeMessage);
        }

        return sb.ToString();
    }
}
