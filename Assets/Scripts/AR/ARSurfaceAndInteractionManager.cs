using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona la colocación de las escenas 3D sobre una superficie plana (mesa/escritorio)
/// y la interacción táctil directa (Raycast 3D) con los objetos del escenario en AR.
/// </summary>
public class ARSurfaceAndInteractionManager : MonoBehaviour
{
    [Header("Configuración de Superficie AR")]
    [Tooltip("Distancia frontal desde la cámara donde se apoya la superficie 3D (metros).")]
    public float surfaceDistance = 0.58f;

    [Tooltip("Altura relativa a la cámara para simular el apoyo sobre una mesa o escritorio (metros).")]
    public float surfaceVerticalOffset = -0.16f;

    [Header("Referencias")]
    public ScenarioHUDController hudController;

    private Camera mMainCamera;

    private void Awake()
    {
        mMainCamera = Camera.main;
        if (hudController == null)
        {
            hudController = FindAnyObjectByType<ScenarioHUDController>();
        }
    }

    private void Update()
    {
        if (mMainCamera == null)
        {
            mMainCamera = Camera.main;
            if (mMainCamera == null) return;
        }

        // Detectar toque en pantalla (Android) o clic izquierdo (Editor) usando Input System
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

        if (!pressedThisFrame) return;

        // Ignorar si el usuario tocó un botón de la interfaz 2D (Canvas)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Lanzar Raycast 3D desde la cámara AR hacia el punto tocado
        Ray ray = mMainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 10.0f))
        {
            var interactable = hit.collider.GetComponent<ARInteractableObject>();
            if (interactable == null)
            {
                interactable = hit.collider.GetComponentInParent<ARInteractableObject>();
            }

            if (interactable != null)
            {
                interactable.OnPlayerInteract();
            }
        }
    }

    /// <summary>
    /// Reposiciona la escena 3D del día actual sobre la superficie plana frente al jugador.
    /// </summary>
    public void RepositionActiveSceneOnSurface()
    {
        if (StoryDirector.I != null)
        {
            StoryDirector.I.PlaceInFrontOfCamera();
        }

        if (hudController == null)
        {
            hudController = FindAnyObjectByType<ScenarioHUDController>();
        }

        var gm = GameManager.I != null ? GameManager.I : FindAnyObjectByType<GameManager>();
        if (gm == null || hudController == null) return;

        foreach (var dt in hudController.dayTargets)
        {
            if (dt != null && dt.targetDay == gm.currentDay)
            {
                dt.PositionInFrontOfCamera(true);
                if (hudController.narrativeBannerText != null)
                {
                    hudController.narrativeBannerText.text =
                        $"<b>[Superficie Anclada - {gm.currentDay}]</b> Toca los objetos 3D sobre la mesa (impresora, monitor, banco, bóveda) para investigar y actuar.";
                }
                break;
            }
        }
    }
}

