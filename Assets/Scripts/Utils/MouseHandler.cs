using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MouseHandler : MonoBehaviour
{
    public Transform cursorImageTransform;
    public Image cursorImage;

    public bool disableOnStart;
    [SerializeField] private bool keepCursorUnlocked = true;
    [SerializeField] private bool showCustomCursor = false;
    [SerializeField] private bool hideSystemCursor = true;

    bool lastPauseState;

    private void Awake()
    {
        ApplyCursorVisibility();
    }

    private void Start()
    {
        ApplyCursorVisibility();

        if (keepCursorUnlocked)
        {
            UnlockMouse();
            return;
        }

        if (disableOnStart)
        {
            LockMouse();
        }
    }

    private void ApplyCursorVisibility()
    {
        if (!showCustomCursor)
        {
            if (cursorImage != null)
            {
                cursorImage.enabled = false;
                if (cursorImage.gameObject != null && cursorImage.gameObject != this.gameObject)
                {
                    cursorImage.gameObject.SetActive(false);
                }
            }
        }

        if (hideSystemCursor || Application.isMobilePlatform)
        {
            Cursor.visible = false;
        }
    }

    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !hideSystemCursor && !Application.isMobilePlatform;
        if (cursorImage != null)
            cursorImage.enabled = showCustomCursor;
        lastPauseState = GameController.IsPaused;
    }

    public void LockMouse()
    {
        if (keepCursorUnlocked)
        {
            UnlockMouse();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (cursorImage != null)
            cursorImage.enabled = false;
        lastPauseState = GameController.IsPaused;
    }

    private void Update()
    {
        if (!showCustomCursor && cursorImage != null && cursorImage.enabled)
        {
            cursorImage.enabled = false;
        }

        if (keepCursorUnlocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = !hideSystemCursor && !Application.isMobilePlatform;
            if (showCustomCursor && cursorImage != null && cursorImageTransform != null)
            {
                cursorImage.enabled = true;
                cursorImageTransform.position = Input.mousePosition;
            }
            return;
        }

        if (GameController.IsPaused != lastPauseState)
        {
            if (GameController.IsPaused)
                UnlockMouse();
            else
                LockMouse();
        }
        if (GameController.isPaused)
        {
            if (showCustomCursor && cursorImageTransform != null)
                cursorImageTransform.position = Input.mousePosition;
        }
    }
}
