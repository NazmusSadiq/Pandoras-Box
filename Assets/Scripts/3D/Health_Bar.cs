using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Healthbar : MonoBehaviour
{
    [SerializeField] private Image _healthbarSprite;
    [SerializeField] private float _reduceSpeed = 2;
    private float _target = 1;
    private Camera _cam;

    // Unity Message
    void Start()
    {
        _cam = Camera.main;
    }

    // Reference: Update health bar with current health values
    public void UpdateHealthBar(float maxHealth, float currentHealth)
    {
        _target = currentHealth / maxHealth;
    }

    // Unity Message
    void Update()
    {
        // Make healthbar face the camera (billboard effect)
        transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

        // Smoothly animate health bar towards target value
        _healthbarSprite.fillAmount = Mathf.MoveTowards(_healthbarSprite.fillAmount, _target, _reduceSpeed * Time.deltaTime);
    }
}