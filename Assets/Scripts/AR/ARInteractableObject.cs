using System.Collections;
using UnityEngine;

/// <summary>
/// Componente para objetos 3D interactivos sobre la superficie AR.
/// Permite que el jugador toque directamente los elementos del escenario (impresora, Hugo, servidor,
/// banco, bóveda) para investigar pistas en primera persona y ejecutar acciones de defensa en 3D.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ARInteractableObject : MonoBehaviour
{
    [Header("Datos del Objeto Interactivo")]
    public string objectTitle = "Objeto del Caso";
    public string evidenceId = "Evidencia_01";
    [Tooltip("Indica si interactuar con este objeto hace avanzar la secuencia cinemática del día.")]
    public bool isKeyObject = true;

    [Tooltip("Mensaje en primera persona cuando el jugador inspecciona el objeto al tocarlo por primera vez.")]
    [TextArea(2, 4)]
    public string firstPersonInspectionMessage;

    [Tooltip("Mensaje en primera persona cuando el jugador ejecuta la acción correctiva sobre el objeto (segundo toque).")]
    [TextArea(2, 4)]
    public string firstPersonResolvedMessage;

    [Header("Mensajes Específicos por Modo")]
    [Tooltip("Mensaje narrado al tocar este objeto durante el Modo Ataque (relato de lo ocurrido en 2016).")]
    [TextArea(2, 4)]
    public string attackInspectionMessage;

    [Tooltip("Mensaje narrado al tocar este objeto durante el Modo Defensa (aplicación del plan de respuesta).")]
    [TextArea(2, 4)]
    public string defenseInspectionMessage;

    [Header("Acción de Defensa Asociada")]
    [Tooltip("0 = Plan de respuesta con roles, 1 = Conciliación diaria automática, 2 = Escalamiento inmediato, -1 = Ninguna")]
    public int associatedDefenseIndex = -1;

    [Tooltip("Responsable sugerido al interactuar directamente con este objeto en 3D.")]
    public string suggestedResponsible = "Hugo Benavides";

    [Header("Feedback Visual 3D")]
    public Renderer statusRenderer;
    public Color resolvedColor = new Color(0.18f, 0.85f, 0.45f, 1f);

    private bool mHasBeenInspected = false;
    private bool mHasBeenResolved = false;
    private Vector3 mBaseLocalScale;
    private bool mScaleCached = false;

    public bool HasBeenInspected => mHasBeenInspected;
    public bool HasBeenResolved => mHasBeenResolved;

    private void Awake()
    {
        CacheScale();
    }

    private void OnEnable()
    {
        CacheScale();
    }

    private void CacheScale()
    {
        if (!mScaleCached)
        {
            mBaseLocalScale = transform.localScale;
            mScaleCached = true;
        }
    }

    /// <summary>
    /// Invocado cuando el jugador toca este objeto 3D en la escena AR.
    /// </summary>
    public void OnPlayerInteract()
    {
        CacheScale();
        StopAllCoroutines();
        StartCoroutine(AnimateTouchPulse());

        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        var hud = FindAnyObjectByType<ScenarioHUDController>();
        var dayTarget = GetComponentInParent<DayTarget>();

        // 1. Registrar la evidencia en el GameManager
        if (gm != null && !string.IsNullOrEmpty(evidenceId))
        {
            gm.RegisterEvidence(evidenceId);
        }

        // 2. Determinar el mensaje según el modo de simulación (Ataque 2016 vs Defensa con Plan)
        mHasBeenInspected = true;
        string messageToShow;
        bool isAttackMode = gm == null || gm.mode == SimMode.Ataque;

        if (isAttackMode)
        {
            // MODO ATAQUE: Solo narra los hechos reales del ataque de 2016. NUNCA cambia a modo Defensa.
            string attackMsg = !string.IsNullOrEmpty(attackInspectionMessage)
                ? attackInspectionMessage
                : firstPersonInspectionMessage;

            messageToShow = $"<b>[ATAQUE 2016 - {objectTitle}]</b> {attackMsg}";
        }
        else
        {
            // MODO DEFENSA: Aplica la medida correctiva y actualiza el estado visual a resuelto
            mHasBeenResolved = true;

            if (statusRenderer != null && statusRenderer.material != null)
            {
                statusRenderer.material.color = resolvedColor;
                if (statusRenderer.material.HasProperty("_BaseColor"))
                {
                    statusRenderer.material.SetColor("_BaseColor", resolvedColor);
                }
            }

            string defMsg = !string.IsNullOrEmpty(defenseInspectionMessage)
                ? defenseInspectionMessage
                : (!string.IsNullOrEmpty(firstPersonResolvedMessage) ? firstPersonResolvedMessage : firstPersonInspectionMessage);

            messageToShow = $"<color=#4ADE80><b>[CON PLAN: CONGELADO EL VIERNES - {objectTitle}]</b></color> {defMsg}";
        }

        // 3. Mostrar el diálogo en el ContextBubble 3D y en el banner del HUD
        if (dayTarget != null && dayTarget.contextBubble != null)
        {
            dayTarget.contextBubble.ShowText(messageToShow);
        }

        if (hud != null && hud.narrativeBannerText != null)
        {
            hud.narrativeBannerText.text = messageToShow;
            hud.RefreshHUDLabels();
        }

        // 4. Notificar al StoryDirector para avanzar la secuencia cinemática correspondiente
        if (StoryDirector.I != null)
        {
            StoryDirector.I.HandleObjectInteraction(this);
        }

        Debug.Log($"[ARInteractableObject] Interacción con '{objectTitle}' | Modo: {(isAttackMode ? "Ataque" : "Defensa")}");
    }

    public void ResetInteractionState()
    {
        mHasBeenInspected = false;
        mHasBeenResolved = false;
    }

    private IEnumerator AnimateTouchPulse()
    {
        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scaleFactor = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            transform.localScale = mBaseLocalScale * scaleFactor;
            yield return null;
        }
        transform.localScale = mBaseLocalScale;
    }
}

