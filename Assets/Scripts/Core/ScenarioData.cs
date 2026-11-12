using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que almacena la información estática y de configuración para el Escenario 06: Respuesta a Incidentes.
/// Contiene la cronología de los días del incidente, las acciones de mitigación posibles y los responsables.
/// </summary>
[CreateAssetMenu(fileName = "ScenarioData_Escenario06", menuName = "Incident Response/Scenario Data", order = 1)]
public class ScenarioData : ScriptableObject
{
    [Serializable]
    public class DayInfo
    {
        public string dayName;
        [TextArea(2, 4)]
        public string description;
        public List<string> eventsOrClues = new List<string>();

        public DayInfo(string name, string desc = "")
        {
            dayName = name;
            description = desc;
            eventsOrClues = new List<string>();
        }
    }

    [Serializable]
    public class CorrectiveAction
    {
        public string id;
        public string title;
        public IncidentDay expectedDay;
        [TextArea(2, 4)]
        public string description;
        public bool isEffective = true;

        public CorrectiveAction() { }

        public CorrectiveAction(string id, string title, IncidentDay expectedDay, string description, bool isEffective = true)
        {
            this.id = id;
            this.title = title;
            this.expectedDay = expectedDay;
            this.description = description;
            this.isEffective = isEffective;
        }
    }

    [Header("Información General")]
    public string scenarioTitle = "Escenario 06: Respuesta a Incidentes";
    [TextArea(2, 4)]
    public string scenarioDescription = "Simulación del ciberataque financiero: análisis cronológico de Viernes a Martes y medidas de respuesta oportuna.";

    [Header("Cronología de Días del Incidente")]
    public DayInfo viernes = new DayInfo("Viernes", "Hugo Benavides ve la impresora apagada y cree que es una avería común");
    public DayInfo sabado = new DayInfo("Sábado", "Aparece el error: A file is missing or changed");
    public DayInfo lunes = new DayInfo("Lunes", "Piden congelar fondos, pero en Filipinas es feriado");
    public DayInfo martes = new DayInfo("Martes", "La orden se lee a las 9:11 a.m. Retiran US$58 millones");

    [Header("Acciones Correctivas Posibles")]
    public List<CorrectiveAction> possibleCorrectiveActions = new List<CorrectiveAction>();

    [Header("Nombres de los Responsables")]
    public List<string> responsibleNames = new List<string>();

    /// <summary>
    /// Retorna una secuencia con la información cronológica de los 4 días del incidente.
    /// </summary>
    public DayInfo[] GetAllDays()
    {
        return new[] { viernes, sabado, lunes, martes };
    }
}
