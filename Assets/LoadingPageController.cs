using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.HDROutputUtils;

public class LoadingPageController : MonoBehaviour
{
    [SerializeField] Image progressBar;
    [SerializeField] TMP_Text progressText;
    [SerializeField] TMP_Text showText;
    [SerializeField] Image showImage;
    public void applyPercent(float percent)
    {
        progressBar.rectTransform.position = new Vector2(percent*500, progressBar.rectTransform.position.y);
        float progress = Mathf.Clamp01(percent / 0.9f);
        if(progress>=0.9999f) progressText.text = "Press any key to continue";
        else progressText.text = "Loading... " + (progress * 100).ToString("F2") + "%";
    }
    public void applyText(string text)
    {
          showText.text = text;
    }
    public void applyImage(Sprite image)
    {
        showImage.sprite = image;
    }
}