using System.IO;
using UnityEngine;

/// <summary>
/// 從專案根目錄（Editor）或 exe 同層目錄（Build）的 secrets.json 讀取 API 金鑰。
/// 用法：SecretLoader.GeminiApiKey / SecretLoader.GoogleCloudApiKey
/// </summary>
public static class SecretLoader
{
    [System.Serializable]
    private class Secrets
    {
        public string gemini_api_key;
        public string google_cloud_api_key;
    }

    private static Secrets _cache;

    // ── 公開屬性 ──────────────────────────────────────────────
    public static string GeminiApiKey       => GetSecrets()?.gemini_api_key      ?? string.Empty;
    public static string GoogleCloudApiKey  => GetSecrets()?.google_cloud_api_key ?? string.Empty;

    // ── 內部邏輯 ──────────────────────────────────────────────
    private static Secrets GetSecrets()
    {
        if (_cache != null) return _cache;

        string path = GetSecretsPath();

        if (!File.Exists(path))
        {
            Debug.LogError($"[SecretLoader] 找不到 secrets.json！\n預期路徑：{path}\n" +
                           "Editor：請把 secrets.json 放在專案根目錄（與 Assets/ 同層）。\n" +
                           "Build：請把 secrets.json 放在 .exe 旁邊的同一個資料夾。");
            return null;
        }

        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        _cache = JsonUtility.FromJson<Secrets>(json);

        if (_cache == null)
            Debug.LogError("[SecretLoader] secrets.json 解析失敗，請確認 JSON 格式正確。");
        else
            Debug.Log("[SecretLoader] 成功讀取 secrets.json");

        return _cache;
    }

    /// <summary>
    /// Editor：Application.dataPath = &lt;ProjectRoot&gt;/Assets → ../secrets.json = 專案根目錄
    /// Build ：Application.dataPath = &lt;BuildDir&gt;/GameName_Data → ../secrets.json = .exe 同層目錄
    /// </summary>
    private static string GetSecretsPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../secrets.json"));
    }
}
