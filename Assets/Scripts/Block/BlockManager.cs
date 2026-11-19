using System.Collections.Generic;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    public static BlockManager Instance { get; private set; }

    [Header("Режим будування")]
    public bool isBuildMode = false;

    [Header("Налаштування конструктора")]
    public float distanceFromCamera = 5f;

    [Tooltip("Максимальна відстань на екрані в пікселях для прилипання")]
    public float screenSnapDistance = 40f;

    private readonly List<Block> allBlocks = new List<Block>();

    private GameObject currentBlock;
    private Block currentBlockComponent;
    private Block targetBlockToAttach;
    private Collider currentBlockCollider;
    private Camera mainCam;

    private int originalLayer;
    private bool skipReleaseThisFrame;

    [Header("Побудова та розбір (затиснення)")]
    public KeyCode buildKey = KeyCode.F;
    public KeyCode dismantleKey = KeyCode.X;

    [Tooltip("Опціонально: UI Image з типом Filled для відображення прогресу")]
    public UnityEngine.UI.Image progressFillImage;
    public GameObject progressUIContainer;

    private Block targetActionBlock;
    private float dismantleTimer;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        mainCam = Camera.main;
    }

    public void SetBuildMode(bool active)
    {
        isBuildMode = active;

        if (!isBuildMode && currentBlock != null)
        {
            ReleaseBlock();
        }
    }

    public void ToggleBuildMode()
    {
        SetBuildMode(!isBuildMode);
    }

    public void RegisterBlock(Block block)
    {
        if (!allBlocks.Contains(block))
            allBlocks.Add(block);
    }

    public void UnregisterBlock(Block block)
    {
        allBlocks.Remove(block);
    }

    void Update()
    {
        if (!isBuildMode)
        {
            UpdateProgressUI(false, 0f);
            return;
        }

        if (currentBlock != null)
        {
            UpdateProgressUI(false, 0f);

            if (skipReleaseThisFrame)
            {
                skipReleaseThisFrame = false;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                ReleaseBlock();
                return;
            }

            MoveBlockWithCursor();
            RotateBlock();
        }
        else
        {
            HandleBuildAndDismantle();
        }
    }

    public void SpawnBlock(GameObject blockPrefab)
    {
        if (!isBuildMode || blockPrefab == null) return;

        if (currentBlock != null)
        {
            ReleaseBlock();
        }

        GameObject newBlock = Instantiate(blockPrefab);
        Block blockComp = newBlock.GetComponent<Block>();
        if (blockComp != null)
        {
            blockComp.blockRotate = newBlock.transform.rotation;
        }

        SelectBlock(newBlock);
    }

    public void SelectBlock(GameObject block)
    {
        if (!isBuildMode) return;

        currentBlock = block;
        currentBlockComponent = block.GetComponent<Block>();
        currentBlockCollider = block.GetComponent<Collider>();

        originalLayer = block.layer;
        block.layer = 2; // Ignore Raycast

        currentBlock.transform.SetParent(null);
        currentBlockComponent.IsSelected = true;

        skipReleaseThisFrame = true;
    }

    private void MoveBlockWithCursor()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = distanceFromCamera;
        currentBlock.transform.position = mainCam.ScreenToWorldPoint(mousePos);

        switch (currentBlockComponent.snapMode)
        {
            case Block.SnapMode.PointsOnly:
                TrySnapToNearestPoint();
                break;

            case Block.SnapMode.SurfaceOnly:
                TrySnapToNearestSurface();
                break;

            case Block.SnapMode.Both:
                if (Input.GetKey(KeyCode.LeftShift))
                    TrySnapToNearestPoint();
                else
                    TrySnapToNearestSurface();
                break;
        }
    }

    private void RotateBlock()
    {
        if (currentBlock == null || currentBlockComponent == null) return;

        bool hasRotated = false;

        if (Input.GetKeyDown(KeyCode.W)) { currentBlockComponent.blockRotate = Quaternion.Euler(90f, 0f, 0f) * currentBlockComponent.blockRotate; hasRotated = true; }
        if (Input.GetKeyDown(KeyCode.S)) { currentBlockComponent.blockRotate = Quaternion.Euler(-90f, 0f, 0f) * currentBlockComponent.blockRotate; hasRotated = true; }
        if (Input.GetKeyDown(KeyCode.D)) { currentBlockComponent.blockRotate = Quaternion.Euler(0f, 0f, 90f) * currentBlockComponent.blockRotate; hasRotated = true; }
        if (Input.GetKeyDown(KeyCode.A)) { currentBlockComponent.blockRotate = Quaternion.Euler(0f, 0f, -90f) * currentBlockComponent.blockRotate; hasRotated = true; }
        if (Input.GetKeyDown(KeyCode.E)) { currentBlockComponent.blockRotate = Quaternion.Euler(0f, 90f, 0f) * currentBlockComponent.blockRotate; hasRotated = true; }
        if (Input.GetKeyDown(KeyCode.Q)) { currentBlockComponent.blockRotate = Quaternion.Euler(0f, -90f, 0f) * currentBlockComponent.blockRotate; hasRotated = true; }

        if (hasRotated)
        {
            currentBlock.transform.rotation = currentBlockComponent.blockRotate;
        }
    }

    private void TrySnapToNearestPoint()
    {
        if (currentBlockComponent == null || currentBlockComponent.points == null) return;

        currentBlock.transform.rotation = currentBlockComponent.blockRotate;

        float minDistance = screenSnapDistance;
        Transform bestMyPoint = null;
        Transform bestTargetPoint = null;
        Block bestTargetBlock = null;

        foreach (Block otherBlock in allBlocks)
        {
            if (otherBlock == null || otherBlock == currentBlockComponent || !otherBlock.isActive) continue;
            if (!currentBlockComponent.CanAttachTo(otherBlock)) continue;

            foreach (GameObject otherPoint in otherBlock.points)
            {
                if (otherPoint == null) continue;

                Vector3 targetScreen3D = mainCam.WorldToScreenPoint(otherPoint.transform.position);
                if (targetScreen3D.z < 0) continue;

                Vector2 targetScreen2D = targetScreen3D;

                foreach (GameObject myPoint in currentBlockComponent.points)
                {
                    if (myPoint == null) continue;

                    // Стикування лише між поінтами з однаковим тегом
                    if (!myPoint.CompareTag(otherPoint.tag)) continue;

                    if (Vector3.Angle(myPoint.transform.up, -otherPoint.transform.up) > 10f) continue;

                    Vector3 myScreen3D = mainCam.WorldToScreenPoint(myPoint.transform.position);
                    if (myScreen3D.z < 0) continue;

                    Vector2 myScreen2D = myScreen3D;
                    float screenDist = Vector2.Distance(myScreen2D, targetScreen2D);

                    if (screenDist < minDistance)
                    {
                        minDistance = screenDist;
                        bestMyPoint = myPoint.transform;
                        bestTargetPoint = otherPoint.transform;
                        bestTargetBlock = otherBlock;
                    }
                }
            }
        }

        if (bestMyPoint != null && bestTargetPoint != null)
        {
            Vector3 localOffset = bestMyPoint.position - currentBlock.transform.position;
            currentBlock.transform.position = bestTargetPoint.position - localOffset;
            targetBlockToAttach = bestTargetBlock;
        }
        else
        {
            targetBlockToAttach = null;
        }
    }

    private void TrySnapToNearestSurface()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f) && currentBlockCollider != null)
        {
            Block hitBlock = hit.collider.GetComponentInParent<Block>();

            if (hitBlock == null || !currentBlockComponent.CanAttachTo(hitBlock))
            {
                currentBlock.transform.rotation = currentBlockComponent.blockRotate;
                targetBlockToAttach = null;
                return;
            }

            Vector3 extents = currentBlockCollider.bounds.extents;
            float offset = Mathf.Abs(Vector3.Dot(extents, hit.normal));

            currentBlock.transform.rotation = Quaternion.LookRotation(hit.normal) * currentBlockComponent.blockRotate;
            currentBlock.transform.position = hit.point + hit.normal * offset;

            targetBlockToAttach = hitBlock;
        }
        else
        {
            currentBlock.transform.rotation = currentBlockComponent.blockRotate;
            targetBlockToAttach = null;
        }
    }

    public void ReleaseBlock()
    {
        if (currentBlock != null)
        {
            if (targetBlockToAttach != null && currentBlockComponent != null)
            {
                currentBlock.transform.SetParent(targetBlockToAttach.transform, true);
                currentBlockComponent.IsSelected = false;
                currentBlock.layer = originalLayer;

                currentBlockComponent.PlaceAsBlueprint();
            }
            else
            {
                Destroy(currentBlock);
            }
        }

        currentBlock = null;
        currentBlockComponent = null;
        currentBlockCollider = null;
        targetBlockToAttach = null;
    }

    private void HandleBuildAndDismantle()
    {
        bool isHoldingBuild = Input.GetKey(buildKey);
        bool isHoldingDismantle = Input.GetKey(dismantleKey);

        if (isHoldingBuild || isHoldingDismantle)
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Block hitBlock = hit.collider.GetComponentInParent<Block>();

                if (hitBlock != null && !hitBlock.isMain)
                {
                    if (targetActionBlock != hitBlock)
                    {
                        targetActionBlock = hitBlock;
                        dismantleTimer = 0f;
                    }

                    // 1. Логіка побудови (якщо об'єкт ще не добудований)
                    if (isHoldingBuild && hitBlock.isBuilding)
                    {
                        float progress = hitBlock.AddBuildProgress(Time.deltaTime);
                        UpdateProgressUI(hitBlock.isBuilding, progress);
                        return;
                    }

                    // 2. Логіка розбору (можна розбирати як готові, так і недобудовані об'єкти)
                    if (isHoldingDismantle)
                    {
                        dismantleTimer += Time.deltaTime;
                        float progress = Mathf.Clamp01(dismantleTimer / hitBlock.dismantleTime);
                        UpdateProgressUI(true, progress);

                        if (dismantleTimer >= hitBlock.dismantleTime)
                        {
                            hitBlock.Dismantle();
                            targetActionBlock = null;
                            dismantleTimer = 0f;
                            UpdateProgressUI(false, 0f);
                        }
                        return;
                    }
                }
            }
        }

        // Якщо відпустили клавішу або відвели курсор
        targetActionBlock = null;
        dismantleTimer = 0f;
        UpdateProgressUI(false, 0f);
    }

    private void UpdateProgressUI(bool show, float progress)
    {
        if (progressUIContainer != null && progressUIContainer.activeSelf != show)
            progressUIContainer.SetActive(show);

        if (progressFillImage != null && show)
            progressFillImage.fillAmount = progress;
    }
}