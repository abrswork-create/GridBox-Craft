using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this component directly to the Add Block / Build Button.
/// It automatically wires up the Button's onClick event to place a block in the world.
/// </summary>
[RequireComponent(typeof(Button))]
public class PlaceBlockButton : MonoBehaviour
{
    [SerializeField] private VoxelPlacer voxelPlacer;

    private void Awake()
    {
        if (voxelPlacer == null)
        {
            voxelPlacer = FindAnyObjectByType<VoxelPlacer>();
        }

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    public void OnButtonClicked()
    {
        if (voxelPlacer == null)
        {
            voxelPlacer = FindAnyObjectByType<VoxelPlacer>();
        }

        if (voxelPlacer != null)
        {
            voxelPlacer.PlaceBlock();
        }
    }
}
