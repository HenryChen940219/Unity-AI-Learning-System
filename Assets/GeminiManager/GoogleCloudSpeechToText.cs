using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using GoogleSpeechToText.Scripts.Data; // 確保引用 Data

namespace GoogleSpeechToText.Scripts
{
    public class GoogleCloudSpeechToText : MonoBehaviour
    {
        [Header("Google Cloud API Key")]
        public string apiKey;

        [Header("Language Settings")]
        public string languageCode = "zh-TW";

        private string apiURL = "https://speech.googleapis.com/v1/speech:recognize";

        // 單例模式
        private static GoogleCloudSpeechToText _instance;
        public static GoogleCloudSpeechToText Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GoogleCloudSpeechToText>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            _instance = this;
        }

        public static void SendSpeechToTextRequest(byte[] audioData, string key, Action<string> onSuccess, Action<Error> onFail)
        {
            if (Instance != null)
            {
                Instance.apiKey = key;
                Instance.TranscribeAudio(audioData, onSuccess, onFail);
            }
            else
            {
                Debug.LogError("❌ 找不到 GoogleCloudSpeechToText 物件！");
            }
        }

        public void TranscribeAudio(byte[] audioData, Action<string> onSuccess, Action<Error> onFail)
        {
            StartCoroutine(SendRequest(audioData, onSuccess, onFail));
        }

        private IEnumerator SendRequest(byte[] audioData, Action<string> onSuccess, Action<Error> onFail)
        {
            string audioContent = Convert.ToBase64String(audioData);

            string json = "{";
            json += "\"config\": {";
            json += "\"encoding\": \"LINEAR16\",";
            json += "\"sampleRateHertz\": 44100,";
            json += "\"languageCode\": \"" + languageCode + "\"";
            json += "},";
            json += "\"audio\": {";
            json += "\"content\": \"" + audioContent + "\"";
            json += "}";
            json += "}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(apiURL + "?key=" + apiKey, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("STT Error: " + request.error);
                    // 這裡可以建立一個簡單的錯誤回傳，但暫時先略過
                }
                else
                {
                    string response = request.downloadHandler.text;
                    Debug.Log("Google 回傳原始資料: " + response);

                    // ⚠️ 關鍵修正：不再手動切字串，直接把整包 JSON 丟給 Manager 處理
                    if (onSuccess != null)
                    {
                        onSuccess(response);
                    }
                }
            }
        }
    }
}