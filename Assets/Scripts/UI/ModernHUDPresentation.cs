using UnityEngine;

/// <summary>Jerarquía visual contextual y márgenes seguros para el HUD horizontal.</summary>
public class ModernHUDPresentation : MonoBehaviour
{
    public StoryDirector director;
    public RectTransform safeArea;
    public GameObject header;
    public GameObject navigation;
    public GameObject legacyBanner;
    public GameObject timer;
    private Rect previousSafeArea;
    private Vector2 previousScreen;

    private void LateUpdate()
    {
        if (safeArea != null && Screen.width > 0 && Screen.height > 0 &&
            (previousSafeArea != Screen.safeArea || previousScreen != new Vector2(Screen.width, Screen.height)))
        {
            previousSafeArea = Screen.safeArea;
            previousScreen = new Vector2(Screen.width, Screen.height);
            safeArea.anchorMin = new Vector2(previousSafeArea.xMin / Screen.width, previousSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(previousSafeArea.xMax / Screen.width, previousSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        if (director == null) return;
        var state = director.State;
        bool playing = state != StoryDirector.StoryState.Intro && state != StoryDirector.StoryState.Transition;
        SetVisible(header, playing);
        SetVisible(timer, playing && state != StoryDirector.StoryState.ScanningSurface);
        SetVisible(navigation, state == StoryDirector.StoryState.Finished);
        // La instrucción actual ya se presenta en la tarjeta de subtítulos.
        SetVisible(legacyBanner, false);
    }

    private static void SetVisible(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible) target.SetActive(visible);
    }
}
