using UnityEngine;
using UnityEngine.SceneManagement;

public class Titlescreen : MonoBehaviour
{

    public void quitGame()
    {
        Application.Quit();
    }

    public void startGame()
    {
        SceneManager.LoadScene(1);
    }
}
