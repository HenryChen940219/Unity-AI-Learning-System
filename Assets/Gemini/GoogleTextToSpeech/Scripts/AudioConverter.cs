using System;
using System.Collections;
using System.IO;
using GoogleTextToSpeech.Scripts.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleTextToSpeech.Scripts
{
    public class AudioConverter : MonoBehaviour
    {
        // ✅ 修改 1: 副檔名改成 .wav (配合 TextToSpeech 的 LINEAR16 設定)
        private const string AudioFileName = "audio.wav";

        // 雖然函式名稱還叫 Mp3 (為了相容您的 TextToSpeech 腳本)，但現在存的是 Wav
        public static void SaveTextToMp3(AudioData audioData)
        {
            var bytes = Convert.FromBase64String(audioData.audioContent);
            File.WriteAllBytes(Application.temporaryCachePath + "/" + AudioFileName, bytes);
        }

        public void LoadClipFromMp3(Action<AudioClip> onClipLoaded)
        {
            StartCoroutine(LoadClipFromMp3Cor(onClipLoaded));
        }

        private static IEnumerator LoadClipFromMp3Cor(Action<AudioClip> onClipLoaded)
        {
            string filePath = "file://" + Application.temporaryCachePath + "/" + AudioFileName;

            // ✅ 修改 2: 這是解決 FMOD Error 的關鍵！
            // 必須用 AudioType.WAV 來開啟 LINEAR16 的檔案
            using (UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.WAV))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("讀取音檔失敗: " + webRequest.error);
                }
                else
                {
                    // 成功讀取 WAV
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                    if (clip != null)
                    {
                        onClipLoaded.Invoke(clip);
                    }
                    else
                    {
                        Debug.LogError("FMOD Error 依然存在：檔案下載成功但無法轉換為 AudioClip");
                    }
                }
            }
        }
    }
}