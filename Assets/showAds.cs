using UnityEngine;

public class showAds : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowBanner();
    }

    // Update is called once per frame
    void Update()
    {

    }
    public void ShowInterstitial()
    {
        AdsManager.ShowInterstitial();
    }
    public void ShowRewarded()
    {
        AdsManager.ShowRewarded(() => { });
    }
    public void ShowBanner()
    {
        AdsManager.ShowBanner();
    }
    public void HideBanner()
    {
        AdsManager.HideBanner();
    }
    public void ToggleBanner()
    {
        AdsManager.ToggleBanner();
    }
    public void LoadScene(string sceneName)
    {
        AdsManager.LoadScene(sceneName);
    }
}
