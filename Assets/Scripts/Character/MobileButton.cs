using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MobileButtonType
{
    Custom,
    Jump,
    Sprint,
    Mine,
    Place
}

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private MobileButtonType buttonType = MobileButtonType.Custom;
    [SerializeField] private bool canToggle = false;
    [SerializeField] private Graphic visualTarget;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    public MobileButtonType ButtonType => buttonType;
    public bool IsPressed { get; private set; }
    public bool WasPressedThisFrame { get; private set; }
    public bool WasReleasedThisFrame { get; private set; }
    public bool IsToggled { get; private set; }

    private void Awake()
    {
        if (visualTarget == null)
        {
            visualTarget = GetComponent<Graphic>();
        }
    }

    private void LateUpdate()
    {
        WasPressedThisFrame = false;
        WasReleasedThisFrame = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        WasPressedThisFrame = true;

        if (canToggle)
        {
            IsToggled = !IsToggled;
        }

        DispatchButtonAction();
        UpdateVisuals();
    }

    private void DispatchButtonAction()
    {
        switch (buttonType)
        {
            case MobileButtonType.Place:
                var placer = FindAnyObjectByType<VoxelPlacer>();
                if (placer != null) placer.PlaceBlock();
                break;
            case MobileButtonType.Mine:
                var miner = FindAnyObjectByType<VoxelPlacer>();
                if (miner != null) miner.MineBlock();
                break;
            case MobileButtonType.Jump:
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) player.TriggerMobileJump();
                break;
            case MobileButtonType.Sprint:
                var runner = FindAnyObjectByType<PlayerController>();
                if (runner != null) runner.ToggleMobileRun();
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
        WasReleasedThisFrame = true;
        UpdateVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (IsPressed)
        {
            IsPressed = false;
            WasReleasedThisFrame = true;
            UpdateVisuals();
        }
    }

    public void SetToggled(bool value)
    {
        IsToggled = value;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (visualTarget != null)
        {
            if (canToggle)
            {
                visualTarget.color = IsToggled ? pressedColor : normalColor;
            }
            else
            {
                visualTarget.color = IsPressed ? pressedColor : normalColor;
            }
        }
    }
}
