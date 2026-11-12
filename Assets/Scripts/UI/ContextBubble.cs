using TMPro;
using UnityEngine;

/// <summary>
/// Controla el cuadro de diálogo flotante en World Space sobre los marcadores AR.
/// Muestra pistas narrativas del incidente y reproduce un feedback auditivo de notificación.
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(AudioSource))]
public class ContextBubble : MonoBehaviour
{
    [Header("Componentes de UI")]
    [Tooltip("Componente TextMeshPro donde se muestra el mensaje narrativo del día.")]
    public TMP_Text messageText;

    [Tooltip("Contenedor visual del panel de fondo y texto.")]
    public GameObject bubbleVisualRoot;

    [Header("Feedback Sensorial")]
    [Tooltip("AudioSource que reproduce el sonido digital de notificación al mostrar una pista.")]
    public AudioSource notificationAudio;

    [Tooltip("Mensaje por defecto asignado a este marcador si se invoca ShowText sin argumentos.")]
    [TextArea(2, 4)]
    public string defaultMessage;

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (messageText == null)
        {
            messageText = GetComponentInChildren<TMP_Text>(true);
        }

        if (notificationAudio == null)
        {
            notificationAudio = GetComponent<AudioSource>();
        }

        if (bubbleVisualRoot == null && transform.childCount > 0)
        {
            bubbleVisualRoot = transform.GetChild(0).gameObject;
        }
    }

    /// <summary>
    /// Muestra el cuadro de texto flotante con el mensaje indicado y reproduce el sonido de notificación.
    /// </summary>
    /// <param name="message">Texto narrativo de la pista del día.</param>
    public void ShowText(string message)
    {
        ResolveReferences();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (bubbleVisualRoot != null)
        {
            bubbleVisualRoot.SetActive(true);
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (notificationAudio != null && notificationAudio.clip != null)
        {
            notificationAudio.Stop();
            notificationAudio.Play();
        }

        Debug.Log($"[ContextBubble] Pista mostrada: \"{message}\"");
    }

    /// <summary>
    /// Sobrecarga que muestra el mensaje por defecto configurado en el Inspector.
    /// </summary>
    public void ShowDefaultText()
    {
        ShowText(defaultMessage);
    }

    /// <summary>
    /// Oculta el cuadro de texto flotante cuando se pierde el marcador.
    /// </summary>
    public void HideText()
    {
        if (bubbleVisualRoot != null)
        {
            bubbleVisualRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

