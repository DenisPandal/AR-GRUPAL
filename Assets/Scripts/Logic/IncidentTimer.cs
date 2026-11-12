using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// RF-03: Muestra y calcula el tiempo perdido entre la primera señal del ataque (Viernes)
/// y la acción de respuesta (hasta Martes 9:11 a.m. = 85 horas en el caso original).
/// </summary>
public class IncidentTimer : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Referencia al componente ActionPicker. Si está vacía, se auto-obtendrá en Awake.")]
    [SerializeField] private ActionPicker actionPicker;

    [Tooltip("Componente TextMeshPro (Cronometro_3D) que muestra el tiempo perdido en pantalla.")]
    public TMP_Text timerText;

    [Header("Parámetros de Tiempo del Caso (Horas)")]
    [Tooltip("Horas perdidas en el caso real entre el Viernes y el Martes a las 9:11 a.m.")]
    [SerializeField] private float baseAttackHoursLost = 85.0f;

    [Tooltip("Horas reducidas por cada una de las 3 medidas clave aplicadas en su día correcto.")]
    [SerializeField] private float hoursSavedPerCorrectDefense = 28.33f;

    [Tooltip("Penalización adicional en horas si no se asignó un responsable.")]
    [SerializeField] private float unassignedResponsiblePenaltyHours = 6.0f;

    [Header("Métricas Acumuladas")]
    [SerializeField] private float totalSimulatedHoursLost = 85.0f;

    [Header("Eventos")]
    public UnityEvent<float> onTimeImpactCalculated = new UnityEvent<float>();

    public float TotalSimulatedHoursLost => totalSimulatedHoursLost;
    public float TotalSimulatedDaysLost => totalSimulatedHoursLost / 24.0f;
    public ActionPicker ActionPicker => actionPicker;

    private void Awake()
    {
        if (actionPicker == null)
        {
            actionPicker = GetComponent<ActionPicker>();
        }
    }

    private void Start()
    {
        if (actionPicker != null)
        {
            actionPicker.onSelectionConfirmed.AddListener(OnActionConfirmed);
        }
        CalculateTimeImpact();
    }

    private void OnDestroy()
    {
        if (actionPicker != null)
        {
            actionPicker.onSelectionConfirmed.RemoveListener(OnActionConfirmed);
        }
    }

    private void OnActionConfirmed(string actionId, string responsible)
    {
        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        // En Modo Ataque el cronómetro muestra las horas históricas perdidas en cada día (0h -> 24h -> 72h -> 85h)
        if (gm != null && gm.mode == SimMode.Ataque)
        {
            UpdateTimerDisplay();
            return;
        }
        CalculateTimeImpact();
    }

    /// <summary>
    /// Calcula la diferencia de tiempo (horas/días perdidos entre la señal del viernes y la acción).
    /// </summary>
    public float CalculateTimeImpact()
    {
        if (actionPicker == null)
        {
            actionPicker = GetComponent<ActionPicker>();
        }

        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();

        // En Modo Ataque se muestran las horas perdidas del caso real hasta el martes 9:11 a.m.
        if (gm != null && gm.mode == SimMode.Ataque)
        {
            UpdateTimerDisplay();
            onTimeImpactCalculated?.Invoke(totalSimulatedHoursLost);
            return totalSimulatedHoursLost;
        }

        // En Modo Defensa: si el jugador completó el plan de respuesta, el congelamiento ocurre el viernes (0h perdidas)
        if (gm != null && gm.ValidateDefenseResponses())
        {
            totalSimulatedHoursLost = 0.0f;
            UpdateTimerDisplay();
            onTimeImpactCalculated?.Invoke(totalSimulatedHoursLost);
            return totalSimulatedHoursLost;
        }

        float hoursLost = baseAttackHoursLost;

        if (actionPicker != null && actionPicker.ActionHistory != null)
        {
            int effectiveCount = 0;
            foreach (var record in actionPicker.ActionHistory)
            {
                if (record.isEffective)
                {
                    effectiveCount++;
                }

                if (string.IsNullOrWhiteSpace(record.responsibleName))
                {
                    hoursLost += unassignedResponsiblePenaltyHours;
                }
            }

            hoursLost -= effectiveCount * hoursSavedPerCorrectDefense;
        }

        totalSimulatedHoursLost = Mathf.Max(0.0f, Mathf.Round(hoursLost * 10f) / 10f);
        UpdateTimerDisplay();
        onTimeImpactCalculated?.Invoke(totalSimulatedHoursLost);

        Debug.Log($"[IncidentTimer] Tiempo perdido calculado: {GetFormattedTimeLost()}");
        return totalSimulatedHoursLost;
    }

    /// <summary>
    /// Actualiza el texto visual del Cronometro_3D en la interfaz.
    /// </summary>
    public void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
            bool isDefenseMode = gm != null && gm.mode == SimMode.Defensa;

            if (isDefenseMode)
            {
                if (totalSimulatedHoursLost <= 0.05f || (gm != null && gm.ValidateDefenseResponses()))
                {
                    timerText.text = "<color=#4ADE80><b>TIEMPO PERDIDO: 00h / 85h</b></color>\n<size=17><color=#86EFAC>[CON PLAN: CONGELADO EL VIERNES - DINERO A SALVO]</color></size>";
                }
                else
                {
                    timerText.text = "<color=#38BDF8><b>CRONOMETRO - MODO DEFENSA</b></color>\n<size=17><color=#BAE6FD>[VIE 5: Activa el plan para congelar en 0h]</color></size>";
                }
            }
            else
            {
                timerText.text = $"<color=#F87171><b>TIEMPO PERDIDO: {totalSimulatedHoursLost:00}h / 85h</b></color>\n<size=17><color=#FCA5A5>[SIN PLAN: CONGELADO MARTES 9:11 - TARDE]</color></size>";
            }
        }
    }

    /// <summary>
    /// Retorna una cadena formateada con los días y horas simulados perdidos.
    /// </summary>
    public string GetFormattedTimeLost()
    {
        if (totalSimulatedHoursLost <= 0.05f)
        {
            return "0 horas (Respuesta inmediata el Viernes)";
        }

        int days = Mathf.FloorToInt(totalSimulatedHoursLost / 24f);
        float remainingHours = totalSimulatedHoursLost % 24f;

        if (days > 0)
        {
            return $"{days}d {remainingHours:F0}h ({totalSimulatedHoursLost:F0}h totales)";
        }
        return $"{remainingHours:F1} hora(s)";
    }

    /// <summary>
    /// Fija el reloj narrativo de la secuencia cinemática en una cantidad específica de horas transcurridas.
    /// </summary>
    public void SetStoryClock(float hours)
    {
        totalSimulatedHoursLost = Mathf.Max(0f, hours);
        UpdateTimerDisplay();
        onTimeImpactCalculated?.Invoke(totalSimulatedHoursLost);
    }

    public TimeSpan GetSimulatedTimeSpan()
    {
        return TimeSpan.FromHours(totalSimulatedHoursLost);
    }
}
