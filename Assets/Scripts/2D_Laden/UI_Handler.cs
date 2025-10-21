using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_Handler : MonoBehaviour
{
    string menu_scene = "MainMenu";

    public void New_Game()
    {
        SceneManager.LoadScene("3D_GameScene");
        Time.timeScale = 1f;
    }

    public void Retry()
    {

        UnityEngine.SceneManagement.Scene currentScene = SceneManager.GetActiveScene();

        SceneManager.LoadScene(currentScene.name);
        Time.timeScale = 1f;
    }

    public void Go_To_Main_Menu()
    {
        SceneManager.LoadScene(menu_scene);
        Time.timeScale = 1f;
    }

    public void Quit_Game()
    {
        Application.Quit();
    }

}