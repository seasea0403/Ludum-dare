using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] GameObject StartPage;
    [SerializeField] GameObject LoadingPage;
    bool firstTime = true;
    void Start()
    {
        if(firstTime)
        {
            firstTime = false;
            StartPage.SetActive(true);
            StartCoroutine(WaitForPress());
        }
    }
    IEnumerator WaitForPress()
    {
        while(!Input.anyKeyDown)
        {
            yield return null;
        }
        StartPage.SetActive(false);
        StartCoroutine(LoadLevelAsync(2));
    }
    IEnumerator LoadLevelAsync(int sceneIndex)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        operation.allowSceneActivation = false;
        LoadingPage.SetActive(true);
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log("╪сть╫Ь╤х: " + (progress * 100) + "%");
            LoadingPage.GetComponent<LoadingPageController>().applyPercent(progress);
            if (operation.progress >= 0.9f && Input.anyKeyDown)
            {
                operation.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}
