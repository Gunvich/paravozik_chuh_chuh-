using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapon List")]
    public List<BaseWeapon> weapons = new List<BaseWeapon>();
    public int currentWeaponIndex = 0;

    void Start()
    {
        InitializeWeapons();
    }

    void Update()
    {
        HandleInput();
    }

    void InitializeWay() // или просто инициализация
    {
        // Выключаем всё оружие при старте, кроме текущего
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null)
            {
                if (i == currentWeaponIndex)
                    weapons[i].Equip();
                else
                    weapons[i].Unequip();
            }
        }
    }

    private void InitializeWeapons()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null)
            {
                if (i == currentWeaponIndex)
                    weapons[i].Equip();
                else
                    weapons[i].Unequip();
            }
        }
    }

    void HandleInput()
    {
        // Клик левой кнопкой мыши — атака
        if (Input.GetMouseButtonDown(0))
        {
            if (weapons.Count > 0 && weapons[currentWeaponIndex] != null)
            {
                weapons[currentWeaponIndex].Attack();
            }
        }

        // Переключение оружия с помощью колесика мыши или цифр
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            SwitchToNextWeapon();
        }
        else if (scroll < 0f)
        {
            SwitchToPreviousWeapon();
        }
    }

    void SwitchToNextWeapon()
    {
        currentWeaponIndex = (currentWeaponIndex + 1) % weapons.Count;
        SelectWeapon(currentWeaponIndex);
    }

    void SwitchToPreviousWeapon()
    {
        currentWeaponIndex = (currentWeaponIndex - 1 + weapons.Count) % weapons.Count;
        SelectWeapon(currentWeaponIndex);
    }

    void SelectWeapon(int index)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null)
            {
                if (i == index)
                    weapons[i].Equip();
                else
                    weapons[i].Unequip();
            }
        }
    }
}