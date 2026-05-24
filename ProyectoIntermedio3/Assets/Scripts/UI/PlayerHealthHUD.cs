using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHealthHUD : MonoBehaviour
{
    [SerializeField] private UIDocument screenUIDocument;
    [SerializeField] private Health playerHealth;

    private VisualElement lifeBarFill;

    private void Start()
    {
        if (screenUIDocument == null)
            screenUIDocument = GetComponent<UIDocument>();

        lifeBarFill = screenUIDocument.rootVisualElement.Q<VisualElement>("life-bar-fill");

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += Refresh;
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= Refresh;
    }

    private void Refresh()
    {
        if (lifeBarFill == null || playerHealth == null)
            return;

        var ratio = playerHealth.MaxHealth > 0
            ? Mathf.Clamp01((float)playerHealth.CurrentHealth / playerHealth.MaxHealth)
            : 0f;

        lifeBarFill.style.width = Length.Percent(ratio * 100f);
    }
}