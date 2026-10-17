using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach this component to any UI Button or scene GameObject to trigger ads.
/// This acts as a scene-local proxy to AdsManager so you can drag it into Button OnClick()
/// events in ANY scene (including MainMenu, VoxelWorld, etc.) without needing AdsManager
/// in that scene's hierarchy.
/// </summary>
public class AdButton : MonoBehaviour
{
    public enum AdActionType
    {
        Manual,             // Manual hookup via Button OnClick() in inspector
        Interstitial,       // Automatically triggers Interstitial ad when button is clicked
        Rewarded,           // Automatically triggers Rewarded ad when button is clicked
        BannerShow,         // Automatically shows banner ad when button is clicked
        BannerHide,         // Automatically hides banner ad when button is clicked
        BannerToggle,       // Automatically toggles banner ad when button is clicked
        LoadSceneWithAd     // Automatically triggers Interstitial ad, then loads targetScene
    }

    [Header("Automatic Button Hookup (Optional)")]
    [Tooltip("If set to anything other than Manual, automatically hooks up to the Button on this GameObject.")]
    [SerializeField] private AdActionType autoTrigger = AdActionType.Manual;

    [Header("Scene Transition Settings")]
    [Tooltip("Target scene name to load when using LoadSceneWithAd (e.g., 'MainMenu' or 'VoxelWorld').")]
    [SerializeField] private string targetScene = "";

    [Header("Scene Start Trigger (Optional)")]
    [Tooltip("If true, automatically displays an Interstitial ad as soon as this scene starts.")]
    [SerializeField] private bool showInterstitialOnSceneStart = false;

    private void Awake()
    {
        if (autoTrigger != AdActionType.Manual)
        {
            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(ExecuteAutoTrigger);
            }
        }
    }

    private void Start()
    {
        if (showInterstitialOnSceneStart)
        {
            InterShow();
        }
    }

    private void ExecuteAutoTrigger()
    {
        switch (autoTrigger)
        {
            case AdActionType.Interstitial:
                InterShow();
                break;
            case AdActionType.Rewarded:
                RewordShow();
                break;
            case AdActionType.BannerShow:
                BannerShow();
                break;
            case AdActionType.BannerHide:
                BannerHide();
                break;
            case AdActionType.BannerToggle:
                BannerToggle();
                break;
            case AdActionType.LoadSceneWithAd:
                LoadTargetSceneWithAd();
                break;
        }
    }

    #region Button OnClick() Callable Methods
    /// <summary>
    /// Displays an Interstitial Ad. Hook this up to Button OnClick() in the Inspector.
    /// </summary>
    public void InterShow()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.InterShow();
        }
    }

    /// <summary>
    /// Displays an Interstitial Ad, then loads the specified scene when the ad is closed or if unavailable.
    /// Hook this up to Button OnClick() in the Inspector and type the scene name in the box.
    /// </summary>
    public void LoadSceneWithAd(string sceneName)
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.LoadSceneWithAd(sceneName);
        }
        else if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Displays an Interstitial Ad, then loads the scene configured in 'targetScene' above.
    /// Hook this up to Button OnClick() in the Inspector.
    /// </summary>
    public void LoadTargetSceneWithAd()
    {
        if (!string.IsNullOrEmpty(targetScene))
        {
            LoadSceneWithAd(targetScene);
        }
        else
        {
            Debug.LogWarning("[AdButton] 'Target Scene' is empty! Please assign a scene name in the Inspector.");
        }
    }

    /// <summary>
    /// Displays a Rewarded Ad. Hook this up to Button OnClick() in the Inspector.
    /// </summary>
    public void RewordShow()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.RewordShow();
        }
    }

    /// <summary>
    /// Shows the banner ad. Hook this up to Button OnClick() in the Inspector.
    /// </summary>
    public void BannerShow()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.BannerShow();
        }
    }

    /// <summary>
    /// Hides the banner ad. Hook this up to Button OnClick() in the Inspector.
    /// </summary>
    public void BannerHide()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.BannerHide();
        }
    }

    /// <summary>
    /// Toggles the banner ad on and off. Hook this up to Button OnClick() in the Inspector.
    /// </summary>
    public void BannerToggle()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.BannerToggle();
        }
    }
    #endregion
}
