using UnityEngine;
using System.IO;

public class SettingsManager : MonoBehaviour
{
    // Путь, где будет храниться файл настроек на компьютере
    private string savePath;

    // Класс (структура), в котором хранятся все наши настройки
    [System.Serializable]
    public class GameSettings
    {
        public float masterVolume = 1.0f;
        public float mouseSensitivity = 15.0f;
        public int qualityLevel = 2;
    }

    public GameSettings settings = new GameSettings();

    void Awake()
    {
        // Задаем путь к файлу settings.json в папке сохранения игры
        // Создаст папку Saves прямо в корне проекта (рядом с папкой Assets)
        string folderPath = Application.dataPath + "/../Saves";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        savePath = folderPath + "/settings.json";

        // При запуске сразу пробуем загрузить сохраненные настройки
        LoadSettings();
    }

    // Метод сохранения настроек в JSON
    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(settings, true);
        File.WriteAllText(savePath, json);
        Debug.Log("Настройки сохранены в: " + savePath);
    }

    // Метод загрузки настроек из JSON
    public void LoadSettings()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            JsonUtility.FromJsonOverwrite(json, settings);
            Debug.Log("Настройки успешно загружены!");
        }
        else
        {
            Debug.Log("Файл настроек не найден, используются стандартные значения.");
            SaveSettings(); // Создаем файл с дефолтными настройками, если его еще нет
        }
    }
}