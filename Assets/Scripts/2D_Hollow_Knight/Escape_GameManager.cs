using TMPro;
using UnityEngine;

public class Escape_GameManager : Base_GameManager
{
    public float timeleft = 30.0f;
    private TextMeshProUGUI timerText = null;
    private bool countdownStarted = false;

    protected override void Start()
    {
        base.Start();

        // Find and setup timer text
        if (gameplayHUD != null)
        {
            Transform timerTextObj = gameplayHUD.transform.Find("TimerText");
            if (timerTextObj != null)
                timerText = timerTextObj.GetComponent<TextMeshProUGUI>();
        }

        StartCoroutine(CountdownStart(3f)); 
    }

    private void Update()
    {
        if (countdownStarted && timeleft > 0)
        {
            timeleft -= Time.deltaTime;
            UpdateTimerDisplay();

            if (timeleft <= 0)
            {
                timeleft = 0;
                Game_Over();
            }
        }
    }

    private System.Collections.IEnumerator CountdownStart(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Countdown starts now
        countdownStarted = true;
        Debug.Log("Countdown started!");

        // Initialize timer display
        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            // Format time as seconds with 1 decimal place
            timerText.text = "Time Left: " + timeleft.ToString("F0") + "s";
        }
    }

    public override bool CanProceed()
    {
        return enemy_Num <= 0;
    }

    // Optional: Method to add extra time
    public void AddTime(float extraTime)
    {
        timeleft += extraTime;
        UpdateTimerDisplay();
    }

    // Optional: Method to pause/resume countdown
    public void SetCountdownActive(bool active)
    {
        countdownStarted = active;
    }
}