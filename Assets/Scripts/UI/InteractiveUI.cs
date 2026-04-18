using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

public class InteractiveUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField]
    private float transitionSpeed = 2f;
    [SerializeField]
    private float hoverGray = 0.2f;

    private Image image;
    private Color baseColor;
    private float grayScale = 1f;
    private float targetGrayScale = 1f;

    private bool isPointerDown;
    public static bool mouseOn;

    void Awake()
    {
        image = GetComponent<Image>();
        if (image == null)
        {
            Debug.LogWarning($"{nameof(InteractiveUI)} requires an Image component on the same GameObject.");
            baseColor = Color.white;
        }
        else
        {
            baseColor = image.color;
        }

        grayScale = 1f;
        targetGrayScale = 1f;
        mouseOn=false;
    }

    void Update()
    {
        if (Mathf.Approximately(grayScale, targetGrayScale)) return;

        grayScale = Mathf.MoveTowards(grayScale, targetGrayScale, transitionSpeed * Time.deltaTime);
        if (image != null)
        {
            image.color = new Color(baseColor.r * grayScale, baseColor.g * grayScale, baseColor.b * grayScale, baseColor.a);
        }
    }

    void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        Debug.Log("Pointer Down");
    }

    void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
    {
        if (isPointerDown)
        {
            HandleClick();
        }

        isPointerDown = false;
        Debug.Log("Pointer Up");
    }

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        targetGrayScale = hoverGray;
        mouseOn=true;
        Debug.Log("Pointer Enter");
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        targetGrayScale = 1f;
        isPointerDown = false;
        mouseOn=false;
        Debug.Log("Pointer Exit");
    }

    protected virtual void HandleClick()
    {
        Debug.Log($"{this.gameObject}�ѵ��");
    }
}