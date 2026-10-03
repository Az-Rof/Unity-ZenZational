using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Titlescreen : MonoBehaviour
{
    public GameObject pausePanel, gameOverPanel;

    void Start()
    {
    }

    void Update()
    {
        pauseGame();
    }

    public void quitGame()
    {
        Application.Quit();
    }

    public void startGame()
    {
        SceneManager.LoadScene(1);
    }

    public void Awake()
    {

    }








    // All function about gameplay/GUI is inside this region
    #region onGUI
    public void continueGame()
    {
        if (Time.timeScale == 0)
        {
            Time.timeScale = 1f;
            // Set to non-active the others pop-up/panel
            closePopUp();
            closePanel();
        }
    }

    public void pauseGame()
    {

        // Just in case in Main-Menu scene.
        if (Keyboard.current == null)
        {
            Debug.Log("No Input Detected");
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame
        && (gameOverPanel.activeSelf != true || gameOverPanel != null)
        && SceneManager.GetActiveScene().buildIndex != 0)
        {
            if (Time.timeScale == 1f)
            {
                Time.timeScale = 0f;
                pausePanel.gameObject.SetActive(true);
            }
            else if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
                pausePanel.gameObject.SetActive(false);
                // Set to non-active the others pop-up/panel
                closePopUp();
                closePanel();
            }
        }
    }

    public void restartGame()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f; // Resume time before restarting
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void gameOver()
    {
        Time.timeScale = 0;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    public void toMainMenu()
    {
        if (Time.timeScale == 1f)
        {
            return;
        }
        else
        {
            SceneManager.LoadScene(0);
            Time.timeScale = 1f;
        }
    }
    #endregion






    #region onGUI pop-up/panel
    public List<GameObject> PopUp = new List<GameObject>(); // Childs Panel
    public List<GameObject> Panel = new List<GameObject>(); // Parents Panel

    // Popup is called
    public void popUp_Settings()
    {
        foreach (GameObject PopUp in PopUp)
        {
            if (PopUp.name == "settingsPanel")
            {
                PopUp.SetActive(true);
            }
            else
            {
                PopUp.SetActive(false);
            }
        }
    }

    public void closePopUp()
    {
        foreach (GameObject PopUp in PopUp)
        {
            PopUp.SetActive(false);
        }
    }

    public void closePanel()
    {

        foreach (GameObject Panel in Panel)
        {
            Panel.SetActive(false);
        }
    }

    #endregion
}