#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Meta.WitAi.Data.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// GUIビルド時: IPreprocessBuild/IPostprocessBuild で自動的にトークンを注入・クリア
/// CI/CD時: -executeMethod CIBuilder.BuildProject -witToken "xxx" -witServerToken "yyy" で呼び出す
/// </summary>
public class CIBuilder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -100;

    private static readonly string[] BuildScenes = new[]
    {
        "Assets/Scenes/SampleScene.unity"
    };

    private static readonly string WitConfigPath = "ProjectSettings/wit.config";

    // --- トークン解決 ---

    private static string ResolveClientToken()
    {
        string token = GetCommandLineArg("-witToken");
        if (!string.IsNullOrEmpty(token)) return token;

        token = Environment.GetEnvironmentVariable("WIT_CLIENT_TOKEN");
        if (!string.IsNullOrEmpty(token)) return token;

        return null;
    }

    private static string ResolveServerToken()
    {
        string token = GetCommandLineArg("-witServerToken");
        if (!string.IsNullOrEmpty(token)) return token;

        token = Environment.GetEnvironmentVariable("WIT_SERVER_TOKEN");
        if (!string.IsNullOrEmpty(token)) return token;

        return null;
    }

    // --- GUI ビルド用フック（File → Build で自動実行） ---

    public void OnPreprocessBuild(BuildReport report)
    {
        string clientToken = ResolveClientToken();
        if (!string.IsNullOrEmpty(clientToken))
            InjectClientToken(clientToken);
        else
            Debug.LogWarning("[CIBuilder] Client Token 未設定。-witToken 引数または WIT_CLIENT_TOKEN 環境変数を確認してください。");

        string serverToken = ResolveServerToken();
        if (!string.IsNullOrEmpty(serverToken))
            InjectServerToken(serverToken);
        else
            Debug.LogWarning("[CIBuilder] Server Token 未設定。-witServerToken 引数または WIT_SERVER_TOKEN 環境変数を確認してください。");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        InjectClientToken(string.Empty);
        InjectServerToken(string.Empty);
    }

    // --- CI/CD用 ---

    public static void BuildProject()
    {
        string clientToken = ResolveClientToken();
        string serverToken = ResolveServerToken();

        if (string.IsNullOrEmpty(clientToken))
        {
            Debug.LogError("[CIBuilder] -witToken が指定されていません!");
            EditorApplication.Exit(1);
            return;
        }

        InjectClientToken(clientToken);
        if (!string.IsNullOrEmpty(serverToken))
            InjectServerToken(serverToken);

        try
        {
            var options = new BuildPlayerOptions
            {
                scenes = BuildScenes,
                locationPathName = "Builds/app.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[CIBuilder] ビルド成功");
            }
            else
            {
                Debug.LogError($"[CIBuilder] ビルド失敗: {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }
        finally
        {
            InjectClientToken(string.Empty);
            InjectServerToken(string.Empty);
        }
    }

    // --- ローカルテスト用メニュー ---

    [MenuItem("Build/Test Token Injection (Dry Run)")]
    public static void TestTokenInjection()
    {
        string clientToken = ResolveClientToken();
        string serverToken = ResolveServerToken();

        if (string.IsNullOrEmpty(clientToken) && string.IsNullOrEmpty(serverToken))
        {
            Debug.LogWarning("[CIBuilder] トークンが見つかりません。引数または環境変数を設定してください。");
            return;
        }

        if (!string.IsNullOrEmpty(clientToken))
        {
            InjectClientToken(clientToken);
            Debug.Log("[CIBuilder] Client Token 注入テスト成功");
            InjectClientToken(string.Empty);
        }

        if (!string.IsNullOrEmpty(serverToken))
        {
            InjectServerToken(serverToken);
            Debug.Log("[CIBuilder] Server Token 注入テスト成功");
            InjectServerToken(string.Empty);
        }

        Debug.Log("[CIBuilder] テスト完了、クリア済み。");
    }

    // --- ユーティリティ ---

    private static string GetCommandLineArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static void InjectClientToken(string token)
    {
        string[] guids = AssetDatabase.FindAssets("t:WitConfiguration");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WitConfiguration config = AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
            if (config == null) continue;

            config.SetClientAccessToken(token);
            EditorUtility.SetDirty(config);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CIBuilder] Client Token: {(string.IsNullOrEmpty(token) ? "クリア済み" : "注入済み")}");
    }

    private static void InjectServerToken(string token)
    {
        if (!File.Exists(WitConfigPath))
        {
            Debug.LogWarning($"[CIBuilder] {WitConfigPath} が見つかりません。スキップします。");
            return;
        }

        string json = File.ReadAllText(WitConfigPath);
        JObject config = JObject.Parse(json);
        JArray settings = (JArray)config["configSettings"];

        if (settings != null)
        {
            foreach (JObject entry in settings)
            {
                entry["serverToken"] = token;
            }
        }

        File.WriteAllText(WitConfigPath, config.ToString(Formatting.None));
        Debug.Log($"[CIBuilder] Server Token: {(string.IsNullOrEmpty(token) ? "クリア済み" : "注入済み")}");
    }
}
#endif
