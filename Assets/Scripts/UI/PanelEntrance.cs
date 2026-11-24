using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class PanelEntrance : MonoBehaviour
{
    private CanvasGroup group;
    private float elapsed;

    private void OnEnable()
    {
        group = GetComponent<CanvasGroup>();
        elapsed = 0f;
        group.alpha = 0f;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        group.alpha = Mathf.SmoothStep(0f, 1f, elapsed / .4f);
    }

    private void OnDisable()
    {
        if (group != null) group.alpha = 1f;
    }
}
