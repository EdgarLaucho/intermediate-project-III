using UnityEngine;
using UnityEngine.UI;

public class EnemyWaveIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    private EnemyIconInfo info;
    private WaveTooltipUI tooltip;
    private RectTransform rectTransform;
    private bool isHovering;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(EnemyIconInfo newInfo, WaveTooltipUI newTooltip)
    {
        info = newInfo;
        tooltip = newTooltip;

        if (iconImage != null && info != null)
        {
            iconImage.sprite = info.icon;
        }
    }

    private void Update()
    {
        if (info == null || tooltip == null || rectTransform == null)
            return;

        bool mouseOver = RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            Input.mousePosition
        );

        if (mouseOver && !isHovering)
        {
            isHovering = true;
            tooltip.Show(info.enemyName, info.description);
        }
        else if (!mouseOver && isHovering)
        {
            isHovering = false;
            tooltip.Hide();
        }
    }
}