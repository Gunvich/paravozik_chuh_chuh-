using System;
using UnityEngine;
using UnityEngine.Events;

public class Block : MonoBehaviour
{
    public enum SnapMode
    {
        Both,
        PointsOnly,
        SurfaceOnly
    }

    [Flags]
    public enum BlockCategory
    {
        None = 0,
        Wall = 1 << 0,
        Floor = 1 << 1,
        Roof = 1 << 2,
        Furniture = 1 << 3,
        Decor = 1 << 4,
        Everything = ~0
    }

    [Header("Налаштування з'єднання")]
    public SnapMode snapMode = SnapMode.Both;
    public BlockCategory blockCategory = BlockCategory.Wall;
    public BlockCategory canAttachTo = BlockCategory.Everything;

    [Header("Збір та розбір")]
    [Tooltip("Час будівництва в секундах (0 = миттєво)")]
    public float buildTime = 2f;
    [Tooltip("Час розбору в секундах")]
    public float dismantleTime = 2f;

    [HideInInspector] public float currentBuildProgress = 0f;

    public UnityEvent onBuilt;
    public UnityEvent onDismantled;

    [Space]
    public GameObject[] points;

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                UpdateMaterial();
            }
        }
    }

    public bool isActive = false;
    public bool isMain = false;
    public bool isBuilding = false;
    public Quaternion blockRotate;

    [Header("Візуальні налаштування")]
    public MeshRenderer meshRenderer;
    public Color defaultColor = Color.white;
    public Color selectedColor = new Color(1f, 0.92f, 0.016f, 1f);
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        propertyBlock = new MaterialPropertyBlock();
        blockRotate = transform.rotation;
    }

    void Start()
    {
        if (BlockManager.Instance != null)
            BlockManager.Instance.RegisterBlock(this);

        if (isMain) isActive = true;
        UpdateMaterial();
    }

    void OnDestroy()
    {
        if (BlockManager.Instance != null)
            BlockManager.Instance.UnregisterBlock(this);
    }

    void OnMouseDown()
    {
        if (BlockManager.Instance != null && BlockManager.Instance.isBuildMode && !isMain && !IsSelected && !isBuilding)
        {
            BlockManager.Instance.SelectBlock(gameObject);
        }
    }

    // Викликається при розміщенні блоку
    public void PlaceAsBlueprint()
    {
        if (buildTime <= 0f)
        {
            CompleteBuilding();
        }
        else
        {
            isBuilding = true;
            isActive = false;
            currentBuildProgress = 0f;
            UpdateMaterial();
        }
    }

    // Повертає прогрес від 0 до 1
    public float AddBuildProgress(float deltaTime)
    {
        if (!isBuilding) return 1f;

        currentBuildProgress += deltaTime;
        if (currentBuildProgress >= buildTime)
        {
            CompleteBuilding();
            return 1f;
        }

        return currentBuildProgress / buildTime;
    }

    private void CompleteBuilding()
    {
        isBuilding = false;
        isActive = true;
        currentBuildProgress = buildTime;
        UpdateMaterial();
        onBuilt?.Invoke();
    }

    public void Dismantle()
    {
        onDismantled?.Invoke();
        Destroy(gameObject);
    }

    public bool CanAttachTo(Block targetBlock)
    {
        if (targetBlock == null) return false;
        return (canAttachTo & targetBlock.blockCategory) != 0;
    }

    public void UpdateMaterial()
    {
        if (meshRenderer == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        meshRenderer.GetPropertyBlock(propertyBlock);

        if (IsSelected)
        {
            propertyBlock.SetColor(BaseColorProp, selectedColor);
            propertyBlock.SetColor(EmissionColorProp, selectedColor * 1.5f);
        }
        else if (!isActive)
        {
            propertyBlock.SetColor(BaseColorProp, inactiveColor);
            propertyBlock.SetColor(EmissionColorProp, Color.black);
        }
        else
        {
            propertyBlock.SetColor(BaseColorProp, defaultColor);
            propertyBlock.SetColor(EmissionColorProp, Color.black);
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}