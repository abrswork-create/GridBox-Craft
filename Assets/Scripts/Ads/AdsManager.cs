using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

public class AdsManager : MonoBehaviour
{
    private static AdsManager _instance;
    public static AdsManager Instance
    {
        get
        {
            if (_instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                _instance = FindAnyObjectByType<AdsManager>();
#else
                _instance = FindObjectOfType<AdsManager>();
#endif
                if (_instance == null)
                {
                    GameObject adObj = new GameObject("AdsManager");
                    _instance = adObj.AddComponent<AdsManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

#pragma warning disable 0414
    [Header("UMP Privacy & Consent (GDPR / CCPA)")]
    [Tooltip("If enabled, shows the Google UMP consent form (GDPR/EU regulations) before loading ads on Android and iOS.")]
    [SerializeField] private bool enableUmp = true;
    [Tooltip("Enable for testing UMP consent popup in Editor (simulates an EEA user). Disable before release.")]
    [SerializeField] private bool testModeEEA = false;

    [Header("Banner Ad Settings")]
    [SerializeField] private bool showBannerOnStart = true;
    [SerializeField] private AdPosition bannerPosition = AdPosition.Top;
    [SerializeField] private string androidBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111"; // Google Test ID
    [SerializeField] private string iosBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";     // Google Test ID

    [Header("Interstitial Ad Settings")]
    [SerializeField] private bool autoReloadInterstitial = true;
    [SerializeField] private string androidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Google Test ID
    [SerializeField] private string iosInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";     // Google Test ID

    [Header("Rewarded Ad Settings")]
    [SerializeField] private bool autoReloadRewarded = true;
    [SerializeField] private string androidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Google Test ID
    [SerializeField] private string iosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";     // Google Test ID

    [Header("Reward Events")]
    [Tooltip("Event triggered when the player finishes watching a rewarded ad")]
    public UnityEvent onRewardEarned;
#pragma warning restore 0414

    // Initialization and Consent States
    private bool _isMobileAdsInitialized = false;
    private bool _isUmpCompleted = false;

    // Internal Ad Objects
    private BannerView _bannerView;
    private InterstitialAd _interstitialAd;
    private RewardedAd _rewardedAd;

    // Loading & Display States
    private bool _isBannerLoading = false;
    private bool _isBannerVisible = false;
    private bool _isInterstitialLoading = false;
    private bool _isRewardedLoading = false;

    // Callbacks
    private Action _onInterstitialClosedCallback;
    private Action<Reward> _onRewardedSuccessCallback;
    private Action _onRewardedClosedCallback;

    // Public C# Events
    public event Action OnBannerLoaded;
    public event Action<LoadAdError> OnBannerLoadFailed;
    public event Action OnInterstitialLoaded;
    public event Action<LoadAdError> OnInterstitialLoadFailed;
    public event Action OnInterstitialClosed;
    public event Action OnRewardedLoaded;
    public event Action<LoadAdError> OnRewardedLoadFailed;
    public event Action OnRewardedClosed;

    // Thread safe dispatch queue
    private static readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

    public bool IsInterstitialAdReady => _interstitialAd != null && _interstitialAd.CanShowAd();
    public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.CanShowAd();
    public bool IsBannerVisible => _isBannerVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance == null)
        {
            GameObject adObj = new GameObject("AdsManager");
            adObj.AddComponent<AdsManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (showBannerOnStart && !_isBannerVisible && _bannerView != null)
        {
            BannerShow();
        }
    }

    private void Update()
    {
        while (_mainThreadQueue.Count > 0)
        {
            Action action = null;
            lock (_mainThreadQueue)
            {
                if (_mainThreadQueue.Count > 0)
                    action = _mainThreadQueue.Dequeue();
            }
            action?.Invoke();
        }
    }

    public static void RunOnMainThread(Action action)
    {
        if (action == null) return;
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    private void Start()
    {
        _isBannerVisible = showBannerOnStart;

        if (enableUmp)
        {
            RequestConsentAndInitialize();
        }
        else
        {
            InitializeGoogleMobileAds();
        }
    }

    /// <summary>
    /// Requests UMP consent information (GDPR/EU regulations) and shows the form if required.
    /// Once consent is resolved, initializes Google Mobile Ads SDK.
    /// </summary>
    public void RequestConsentAndInitialize()
    {
        Debug.Log("[AdsManager] Requesting UMP Consent information...");

        var requestParameters = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false
        };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (testModeEEA)
        {
            requestParameters.ConsentDebugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA
            };
        }
#endif

        ConsentInformation.Update(requestParameters, (FormError updateError) =>
        {
            RunOnMainThread(() =>
            {
                if (updateError != null)
                {
                    Debug.LogError($"[AdsManager] UMP Consent update failed: {updateError.Message}");
                    OnConsentComplete();
                    return;
                }

                // Determines if a consent form is required and shows it if necessary
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
                {
                    RunOnMainThread(() =>
                    {
                        if (formError != null)
                        {
                            Debug.LogWarning($"[AdsManager] UMP Consent form completed with notice/error: {formError.Message}");
                        }
                        else
                        {
                            Debug.Log("[AdsManager] UMP Consent form completed successfully.");
                        }

                        OnConsentComplete();
                    });
                });
            });
        });
    }

    private void OnConsentComplete()
    {
        _isUmpCompleted = true;

        if (ConsentInformation.CanRequestAds())
        {
            Debug.Log("[AdsManager] Consent verified. Initializing Google Mobile Ads SDK...");
            InitializeGoogleMobileAds();
        }
        else
        {
            Debug.LogWarning("[AdsManager] Consent not obtained; ads cannot be requested under current policy.");
        }
    }

    private void InitializeGoogleMobileAds()
    {
        if (_isMobileAdsInitialized) return;

        Debug.Log("[AdsManager] Initializing Google Mobile Ads SDK...");
        MobileAds.Initialize(initStatus =>
        {
            RunOnMainThread(() =>
            {
                _isMobileAdsInitialized = true;
                Debug.Log("[AdsManager] Google Mobile Ads SDK initialized.");
                LoadInterstitialAd();
                LoadRewardedAd();

                if (_isBannerVisible)
                {
                    BannerShow();
                }
            });
        });
    }

    #region Helper Methods for Ad Unit IDs
    private string GetBannerAdUnitId()
    {
#if UNITY_ANDROID
        return string.IsNullOrEmpty(androidBannerAdUnitId) ? "ca-app-pub-3940256099942544/6300978111" : androidBannerAdUnitId;
#elif UNITY_IPHONE || UNITY_IOS
        return string.IsNullOrEmpty(iosBannerAdUnitId) ? "ca-app-pub-3940256099942544/2934735716" : iosBannerAdUnitId;
#else
        return string.IsNullOrEmpty(androidBannerAdUnitId) ? "ca-app-pub-3940256099942544/6300978111" : androidBannerAdUnitId;
#endif
    }

    private string GetInterstitialAdUnitId()
    {
#if UNITY_ANDROID
        return string.IsNullOrEmpty(androidInterstitialAdUnitId) ? "ca-app-pub-3940256099942544/1033173712" : androidInterstitialAdUnitId;
#elif UNITY_IPHONE || UNITY_IOS
        return string.IsNullOrEmpty(iosInterstitialAdUnitId) ? "ca-app-pub-3940256099942544/4411468910" : iosInterstitialAdUnitId;
#else
        return string.IsNullOrEmpty(androidInterstitialAdUnitId) ? "ca-app-pub-3940256099942544/1033173712" : androidInterstitialAdUnitId;
#endif
    }

    private string GetRewardedAdUnitId()
    {
#if UNITY_ANDROID
        return string.IsNullOrEmpty(androidRewardedAdUnitId) ? "ca-app-pub-3940256099942544/5224354917" : androidRewardedAdUnitId;
#elif UNITY_IPHONE || UNITY_IOS
        return string.IsNullOrEmpty(iosRewardedAdUnitId) ? "ca-app-pub-3940256099942544/1712485313" : iosRewardedAdUnitId;
#else
        return string.IsNullOrEmpty(androidRewardedAdUnitId) ? "ca-app-pub-3940256099942544/5224354917" : androidRewardedAdUnitId;
#endif
    }
    #endregion

    #region Button Callable Functions

    /// <summary>
    /// Displays an Interstitial Ad. Call this from your UI Button OnClick().
    /// </summary>
    public void InterShow()
    {
        ShowInterstitialAd(null);
    }

    /// <summary>
    /// Displays an Interstitial Ad, then loads the target scene once the ad is closed or if unavailable.
    /// Can be linked directly to UI Buttons in the Inspector (takes scene name parameter).
    /// </summary>
    public void LoadSceneWithAd(string sceneName)
    {
        Debug.Log($"[AdsManager] Showing Interstitial Ad before loading scene '{sceneName}'...");
        ShowInterstitialAd(() =>
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
        });
    }

    /// <summary>
    /// Displays a Rewarded Ad. Call this from your UI Button OnClick().
    /// </summary>
    public void RewordShow()
    {
        ShowRewardedAd(() =>
        {
            Debug.Log("<color=green>[AdsManager] Rewarded ad completed! Triggering rewards...</color>");
            onRewardEarned?.Invoke();

            // Default in-game reward: fully restore player health & stamina if PlayerStats is present
            if (PlayerStats.Main != null)
            {
                PlayerStats.Main.Health = PlayerStats.Main.maxHealth;
                PlayerStats.Main.Stamina = PlayerStats.Main.maxStamina;
            }
        });
    }

    /// <summary>
    /// Shows the banner ad. Call this from a UI Button OnClick().
    /// </summary>
    public void BannerShow()
    {
        _isBannerVisible = true;
        if (!_isMobileAdsInitialized)
        {
            Debug.Log("[AdsManager] BannerShow requested; waiting for UMP / MobileAds initialization...");
            return;
        }

        if (_bannerView != null)
        {
            _bannerView.Show();
        }
        else
        {
            LoadBannerAd();
        }
    }

    /// <summary>
    /// Hides the banner ad. Call this from a UI Button OnClick().
    /// </summary>
    public void BannerHide()
    {
        _isBannerVisible = false;
        if (_bannerView != null)
        {
            _bannerView.Hide();
        }
    }

    /// <summary>
    /// Toggles the banner ad on and off.
    /// </summary>
    public void BannerToggle()
    {
        if (_isBannerVisible)
        {
            BannerHide();
        }
        else
        {
            BannerShow();
        }
    }

    #endregion

    #region Static Methods for Easy Calling from Other Scripts
    /// <summary>
    /// Shows an Interstitial Ad from code. Can run a callback when closed.
    /// Example: AdsManager.ShowInterstitial(() => SceneManager.LoadScene("MainMenu"));
    /// </summary>
    public static void ShowInterstitial(Action onClosed = null) => Instance.ShowInterstitialAd(onClosed);

    /// <summary>
    /// Shows a Rewarded Ad from code and grants a reward on completion.
    /// Example: AdsManager.ShowRewarded(() => player.AddCoins(100));
    /// </summary>
    public static void ShowRewarded(Action onRewardEarned, Action onClosed = null) => Instance.ShowRewardedAd(onRewardEarned, onClosed);

    /// <summary>
    /// Gets or sets the banner ad position dynamically.
    /// </summary>
    public AdPosition BannerPosition
    {
        get => bannerPosition;
        set => SetPosition(value);
    }

    /// <summary>
    /// Changes the banner ad position dynamically from any script.
    /// Example: AdsManager.SetBannerPosition(AdPosition.Top);
    /// </summary>
    public static void SetBannerPosition(AdPosition newPosition) => Instance.SetPosition(newPosition);

    /// <summary>
    /// Instance method to set position and reload banner if already visible.
    /// </summary>
    public void SetPosition(AdPosition newPosition)
    {
        if (bannerPosition != newPosition)
        {
            Debug.Log($"[AdsManager] Changing banner position from {bannerPosition} to {newPosition}");
            bannerPosition = newPosition;
            if (_bannerView != null)
            {
                DestroyBannerAd();
                if (_isBannerVisible)
                {
                    LoadBannerAd();
                }
            }
        }
    }

    /// <summary>
    /// Shows the banner ad from code (optional position parameter).
    /// </summary>
    public static void ShowBanner(AdPosition? position = null)
    {
        if (position.HasValue)
        {
            Instance.SetPosition(position.Value);
        }
        Instance.BannerShow();
    }

    /// <summary>
    /// Hides the banner ad from code.
    /// </summary>
    public static void HideBanner() => Instance.BannerHide();

    /// <summary>
    /// Toggles the banner ad visibility from code.
    /// </summary>
    public static void ToggleBanner() => Instance.BannerToggle();

    /// <summary>
    /// Shows an Interstitial Ad, then loads the target scene when closed.
    /// Example: AdsManager.LoadScene("MainMenu");
    /// </summary>
    public static void LoadScene(string sceneName) => Instance.LoadSceneWithAd(sceneName);

    /// <summary>
    /// Shows the privacy options form (required by Google for EEA users to change consent at any time).
    /// </summary>
    public static void ShowPrivacyOptions(Action onClosed = null) => Instance.ShowPrivacyOptionsForm(onClosed);

    /// <summary>
    /// Shows the privacy options form (call from UI button in Settings).
    /// </summary>
    public void ShowPrivacyOptionsForm(Action onClosed = null)
    {
        Debug.Log("[AdsManager] Showing Privacy Options Form...");
        ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
        {
            RunOnMainThread(() =>
            {
                if (error != null)
                {
                    Debug.LogError($"[AdsManager] Error showing privacy options form: {error.Message}");
                }
                else
                {
                    Debug.Log("[AdsManager] Privacy options form closed.");
                }

                if (ConsentInformation.CanRequestAds() && !_isMobileAdsInitialized)
                {
                    InitializeGoogleMobileAds();
                }

                onClosed?.Invoke();
            });
        });
    }

    /// <summary>
    /// Checks if privacy options button should be visible (EEA / required regions).
    /// </summary>
    public static bool IsPrivacyOptionsRequired()
    {
        return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    }
    #endregion

    #region Interstitial Ad Core Logic
    internal void LoadInterstitialAd()
    {
        if (!_isMobileAdsInitialized || _isInterstitialLoading) return;

        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        string adUnitId = GetInterstitialAdUnitId();
        _isInterstitialLoading = true;
        Debug.Log($"[AdsManager] Loading Interstitial Ad: {adUnitId}");

        AdRequest request = new AdRequest();
        InterstitialAd.Load(adUnitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            RunOnMainThread(() =>
            {
                _isInterstitialLoading = false;
                if (error != null || ad == null)
                {
                    Debug.LogError($"[AdsManager] Interstitial Ad failed to load: {error}");
                    OnInterstitialLoadFailed?.Invoke(error);
                    return;
                }

                Debug.Log("[AdsManager] Interstitial Ad loaded successfully.");
                _interstitialAd = ad;
                RegisterInterstitialEvents(_interstitialAd);
                OnInterstitialLoaded?.Invoke();
            });
        });
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("[AdsManager] Interstitial Ad closed.");
                _onInterstitialClosedCallback?.Invoke();
                _onInterstitialClosedCallback = null;
                OnInterstitialClosed?.Invoke();

                if (autoReloadInterstitial)
                {
                    LoadInterstitialAd();
                }
            });
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnMainThread(() =>
            {
                Debug.LogError($"[AdsManager] Interstitial Ad full-screen content failed: {error}");
                _onInterstitialClosedCallback?.Invoke();
                _onInterstitialClosedCallback = null;

                if (autoReloadInterstitial)
                {
                    LoadInterstitialAd();
                }
            });
        };
    }

    public void ShowInterstitialAd(Action onClosed = null)
    {
        _onInterstitialClosedCallback = onClosed;
        Debug.Log("[AdsManager] Showing Interstitial Ad...");

        if (IsInterstitialAdReady)
        {
            try
            {
                _interstitialAd.Show();
                Debug.Log("[AdsManager] Interstitial Ad shown.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AdsManager] Error showing interstitial ad: {ex.Message}");
                _onInterstitialClosedCallback?.Invoke();
                _onInterstitialClosedCallback = null;
            }
        }
        else
        {
            Debug.LogWarning("[AdsManager] Interstitial ad not ready; loading now.");
            if (!_isInterstitialLoading)
            {
                LoadInterstitialAd();
            }
            _onInterstitialClosedCallback?.Invoke();
            _onInterstitialClosedCallback = null;
        }
    }
    #endregion

    #region Rewarded Ad Core Logic
    internal void LoadRewardedAd()
    {
        if (!_isMobileAdsInitialized || _isRewardedLoading) return;

        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        string adUnitId = GetRewardedAdUnitId();
        _isRewardedLoading = true;
        Debug.Log($"[AdsManager] Loading Rewarded Ad: {adUnitId}");

        AdRequest request = new AdRequest();
        RewardedAd.Load(adUnitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            RunOnMainThread(() =>
            {
                _isRewardedLoading = false;
                if (error != null || ad == null)
                {
                    Debug.LogError($"[AdsManager] Rewarded Ad failed to load: {error}");
                    OnRewardedLoadFailed?.Invoke(error);
                    return;
                }

                Debug.Log("[AdsManager] Rewarded Ad loaded successfully.");
                _rewardedAd = ad;
                RegisterRewardedEvents(_rewardedAd);
                OnRewardedLoaded?.Invoke();
            });
        });
    }

    private void RegisterRewardedEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            RunOnMainThread(() =>
            {
                Debug.Log("[AdsManager] Rewarded Ad closed.");
                _onRewardedClosedCallback?.Invoke();
                _onRewardedClosedCallback = null;
                _onRewardedSuccessCallback = null;
                OnRewardedClosed?.Invoke();

                if (autoReloadRewarded)
                {
                    LoadRewardedAd();
                }
            });
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            RunOnMainThread(() =>
            {
                Debug.LogError($"[AdsManager] Rewarded Ad full-screen content failed: {error}");
                _onRewardedClosedCallback?.Invoke();
                _onRewardedClosedCallback = null;
                _onRewardedSuccessCallback = null;

                if (autoReloadRewarded)
                {
                    LoadRewardedAd();
                }
            });
        };
    }

    public void ShowRewardedAd(Action onUserEarnedReward, Action onClosed = null)
    {
        ShowRewardedAd(reward => onUserEarnedReward?.Invoke(), onClosed);
    }

    public void ShowRewardedAd(Action<Reward> onUserEarnedReward, Action onClosed = null)
    {
        _onRewardedSuccessCallback = onUserEarnedReward;
        _onRewardedClosedCallback = onClosed;
        Debug.Log("[AdsManager] Showing Rewarded Ad...");

        if (IsRewardedAdReady)
        {
            try
            {
                _rewardedAd.Show((Reward reward) =>
                {
                    RunOnMainThread(() =>
                    {
                        Debug.Log($"[AdsManager] User earned reward: {reward.Type} ({reward.Amount})");
                        _onRewardedSuccessCallback?.Invoke(reward);
                    });
                });
                Debug.Log("[AdsManager] Rewarded Ad shown.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AdsManager] Error showing rewarded ad: {ex.Message}");
                _onRewardedClosedCallback?.Invoke();
                _onRewardedClosedCallback = null;
                _onRewardedSuccessCallback = null;
            }
        }
        else
        {
            Debug.LogWarning("[AdsManager] Rewarded Ad not ready; loading now.");
            if (!_isRewardedLoading)
            {
                LoadRewardedAd();
            }
            _onRewardedClosedCallback?.Invoke();
            _onRewardedClosedCallback = null;
            _onRewardedSuccessCallback = null;
        }
    }
    #endregion

    #region Banner Ad Core Logic
    internal void LoadBannerAd()
    {
        if (!_isMobileAdsInitialized || _isBannerLoading) return;

        if (_bannerView != null)
        {
            DestroyBannerAd();
        }

        string adUnitId = GetBannerAdUnitId();
        _isBannerLoading = true;
        Debug.Log($"[AdsManager] Loading Banner Ad: {adUnitId}");

        _bannerView = new BannerView(adUnitId, AdSize.Banner, bannerPosition);

        _bannerView.OnBannerAdLoaded += () =>
        {
            RunOnMainThread(() =>
            {
                _isBannerLoading = false;
                Debug.Log("[AdsManager] Banner Ad loaded successfully.");
                if (_isBannerVisible)
                {
                    _bannerView.Show();
                }
                OnBannerLoaded?.Invoke();
            });
        };

        _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            RunOnMainThread(() =>
            {
                _isBannerLoading = false;
                Debug.LogError($"[AdsManager] Banner Ad failed to load: {error}");
                OnBannerLoadFailed?.Invoke(error);
            });
        };

        AdRequest request = new AdRequest();
        _bannerView.LoadAd(request);
    }

    internal void DestroyBannerAd()
    {
        if (_bannerView != null)
        {
            Debug.Log("[AdsManager] Destroying Banner Ad.");
            _bannerView.Destroy();
            _bannerView = null;
            _isBannerLoading = false;
        }
    }
    #endregion


    private void OnDestroy()
    {
        DestroyBannerAd();

        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
    }
}
