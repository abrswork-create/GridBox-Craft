using UnityEngine;

public class VoxelPlacer : MonoBehaviour
{
    public GameObject blockSelection;

    public bool highlightVoxel = false;

    public bool canPlace = true;

    public bool canGrab = true;

    [Tooltip("If true, mouse buttons will place/mine blocks. Leave false for mobile/button-only controls.")]
    [SerializeField] private bool allowMouseControls = false;

    [SerializeField] private Camera m_Camera;

    private void Awake()
    {
        canPlace = true;
        if (m_Camera == null)
        {
            m_Camera = Camera.main;
        }
    }

    private Camera GetCam()
    {
        if (m_Camera == null)
        {
            m_Camera = Camera.main;
        }
        return m_Camera;
    }

    public void OnMobileMineClick()
    {
        MineBlock();
    }

    public void OnMobilePlaceClick()
    {
        PlaceBlock();
    }

    public bool PlaceBlock()
    {
        if (GameController.IsPaused || !canPlace)
            return false;

        // Only place a block if the player has a placeable Block in their active hotbar slot
        if (ContainerManager.Main != null)
        {
            string selectedSlotID = ContainerManager.Main.GetSelectedSlotID();
            ItemStack selectedStack = ContainerManager.Main.PeekSlot(selectedSlotID);
            if (selectedStack != null && selectedStack.amount > 0 && selectedStack.item is Block)
            {
                ContainerManager.Main.UseSelectedHotbarItem();
                return true;
            }
        }

        // Empty hand or non-block item: Do not place anything!
        return false;
    }

    public bool MineBlock()
    {
        if (GameController.IsPaused || !canGrab)
            return false;

        Camera cam = GetCam();
        if (cam == null)
            return false;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, Consts.InteractionDistance, Consts.VoxelMask))
        {
            Vector3 targetVoxel = hit.point + ray.direction * 0.01f;
            if (TerrainManager.Main != null && TerrainManager.Main.GetVoxel(targetVoxel, out Voxel voxel))
            {
                if (voxel != Voxel.Air)
                {
                    TerrainManager.Main.SetVoxel(targetVoxel, Voxel.Air);
                    if (ItemDatabase.Main != null)
                    {
                        var copy = ItemDatabase.Main.GetCopy(voxel.ToString());
                        if (copy != null)
                        {
                            ItemStack itemStack = new ItemStack(copy);
                            ContainerManager.Main?.TryAlterContainer(itemStack);
                        }
                    }
                    return true;
                }
            }
        }

        return false;
    }

    void Update()
    {
        Camera cam = GetCam();
        if (cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, Consts.InteractionDistance, Consts.VoxelMask))
            {
                if (highlightVoxel && blockSelection)
                {
                    PlacementData posRot = ItemUtil.Snap(ray.direction, hit.point + ray.direction * 0.01f, Vector3Int.one, ItemPivot.MidCenter, SnapType.Center);
                    blockSelection.transform.position = posRot.position;
                    blockSelection.SetActive(true);
                }
            }
            else
            {
                blockSelection?.SetActive(false);
            }
        }
        else
        {
            blockSelection?.SetActive(false);
        }

        if (allowMouseControls && !GameController.IsPaused)
        {
            if (Input.GetMouseButtonDown(0))
            {
                PlaceBlock();
            }
            else if (Input.GetMouseButtonDown(1))
            {
                MineBlock();
            }
        }
    }
}