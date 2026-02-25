using UnityEngine;
using UnityEngine.SceneManagement;

namespace NulabCup.Debugging
{
    /// <summary>
    /// 起動時間を計測するためのデバッグユーティリティ。
    /// RuntimeInitializeOnLoadMethod でシーンロード前後のタイミングを捕捉する。
    /// adb logcat -s Unity | grep "STARTUP" でフィルタ可能。
    /// </summary>
    public static class StartupProfiler
    {
        const string Tag = "[STARTUP]";
        static float s_AppStartTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSubsystemRegistration()
        {
            s_AppStartTime = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} [SubsystemRegistration] t={s_AppStartTime:F3}s — サブシステム登録開始");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void OnAfterAssembliesLoaded()
        {
            var t = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} [AfterAssembliesLoaded] t={t:F3}s (Δ{t - s_AppStartTime:F3}s) — アセンブリロード完了");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void OnBeforeSplashScreen()
        {
            var t = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} [BeforeSplashScreen] t={t:F3}s (Δ{t - s_AppStartTime:F3}s) — スプラッシュ画面前");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            var t = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} [BeforeSceneLoad] t={t:F3}s (Δ{t - s_AppStartTime:F3}s) — シーンロード前");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            var t = Time.realtimeSinceStartup;
            var sceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"{Tag} [AfterSceneLoad] t={t:F3}s (Δ{t - s_AppStartTime:F3}s) — シーン '{sceneName}' ロード完了");
        }

        /// <summary>
        /// 各 MonoBehaviour から呼ぶ汎用マーカー
        /// </summary>
        public static void LogMilestone(string component, string phase)
        {
            var t = Time.realtimeSinceStartup;
            Debug.Log($"{Tag} [{component}] {phase} t={t:F3}s (Δ{t - s_AppStartTime:F3}s)");
        }
    }
}
