using UnityEngine;

public class WaveInfoUITester : MonoBehaviour
{
    [SerializeField] private WaveInfoUI waveInfoUI;

    private void Start()
    {
        string enemiesInfo =
            "Próximos enemigos:\n" +
            "- Chicken x5\n" +
            "  Ataca cuerpo a cuerpo.\n" +
            "- Penguin x2\n" +
            "  Lanza cuchillos desde lejos.";

        waveInfoUI.ShowBuildPhaseWithButton(0, 5, enemiesInfo);
    }
}
