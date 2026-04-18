using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingController : MonoBehaviour
{
    [SerializeField] GameObject StartPage;
    [SerializeField] GameObject LoadingPage;
    [SerializeField] GameObject TutorPage;
    [SerializeField] List<PictureAndText> pictureAndTexts=new List<PictureAndText>() ;
    
    
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
        TutorPage.SetActive(true);
        StartCoroutine(WaitForPress2());
    }
    IEnumerator WaitForPress2()
    {
        yield return new WaitForSeconds(0.5f);
        while (!Input.anyKeyDown)
        {
            yield return null;
        }
        TutorPage.SetActive(false);
        StartCoroutine(LoadLevelAsync(SaveAPI.GetReachedLevel()));
    }
    IEnumerator LoadLevelAsync(int sceneIndex)
    {
        yield return new WaitForSeconds(0.5f);
        AsyncOperation operation = SceneManager.LoadSceneAsync(SaveAPI.GetReachedLevel()+1);
        LoadingPage.SetActive(true);
        LoadingPage.GetComponent<LoadingPageController>().applyImage(pictureAndTexts.ElementAt(sceneIndex).picture);
        operation.allowSceneActivation = false;

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
[System.Serializable] class PictureAndText
{
    public Sprite picture;
    public string text;
}