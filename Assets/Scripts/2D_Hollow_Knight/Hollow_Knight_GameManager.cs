using TMPro;
using UnityEngine;

public class Hollow_Knight_GameManager : Base_GameManager
{
    public override bool CanProceed()
    {
        return enemy_Num <= 0;
    }
}
