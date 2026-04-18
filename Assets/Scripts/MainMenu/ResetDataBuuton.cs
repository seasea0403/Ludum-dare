using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetDataButton : InteractiveUI
{
    protected override void HandleClick()
    {
        SaveAPI.SetReachedLevel(0);
        Debug.Log("level0");
    }
}
