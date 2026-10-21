using UnityEngine;
using TMPro;

public class TypewriterEffect : MonoBehaviour
{
    [Range(0, 1)] 
    public float progress = 0f;
    
    private TMP_Text textComponent;

    void Update()
    {
        if (textComponent == null) 
            textComponent = GetComponent<TMP_Text>();

        if (textComponent != null && textComponent.textInfo != null)
        {
            int totalCharacters = textComponent.textInfo.characterCount;
            textComponent.maxVisibleCharacters = Mathf.RoundToInt(progress * totalCharacters);
        }
    }
}