using UnityEngine;

public class BaseWeapon : MonoBehaviour
{
    [Header("Settings")]
    public string weaponName = "Weapon";
    public float fireRate = 0.25f;
    public float range = 100f;

    [Header("UI Crosshair")]
    public GameObject crosshairUI; // Ссылка на конкретный прицел для этого оружия (точка или крестик)

    [Header("References")]
    public Camera playerCamera;

    private float nextFireTime = 0f;

    protected virtual void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    public virtual void Attack()
    {
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireRate;

        ShootRaycast();
    }

    protected void ShootRaycast()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range))
        {
            Debug.Log($"Попали в объект: {hit.collider.name} на расстоянии {hit.distance} м");
        }
        else
        {
            Debug.Log("Выстрел в молоко");
        }
    }

    public virtual void Equip()
    {
        gameObject.SetActive(true);

        // Включаем прицел этого оружия, если он назначен
        if (crosshairUI != null)
        {
            crosshairUI.SetActive(true);
        }

        Debug.Log($"Достали оружие: {weaponName}");
    }

    public virtual void Unequip()
    {
        gameObject.SetActive(false);

        // Выключаем прицел при смене оружия
        if (crosshairUI != null)
        {
            crosshairUI.SetActive(false);
        }

        Debug.Log($"Убрали оружие: {weaponName}");
    }
}