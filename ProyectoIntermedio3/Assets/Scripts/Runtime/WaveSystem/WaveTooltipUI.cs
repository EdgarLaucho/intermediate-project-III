using TMPro;
using UnityEngine;

public class WaveTooltipUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    public void Show(string title, string description)
    {
        panel.SetActive(true);

        titleText.text = title;
        descriptionText.text = description;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
