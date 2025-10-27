using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Laden_GameManager : Base_GameManager
{
    public override bool CanProceed()
    {
        return enemy_Num <= 0;
    }
}
