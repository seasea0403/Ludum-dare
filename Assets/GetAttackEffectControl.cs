using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetAttackEffectControl : MonoBehaviour
{
    [SerializeField] Material shader;
    float time=0f;
    private void Update()
    {
        time -= Time.deltaTime;
        
        if(time<=-1)
            time=0.5f;
        shader.SetFloat("_Float", time);
        if (time <= 0.1)
        {
            shader.SetFloat("_Float", 0.1f);
        }
        if(time <= 0f)
        {
            shader.SetFloat("_Float", 0.5f);
        }
    }
}