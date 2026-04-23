using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.InputSystem;
using GoogleSpeechToText.Scripts.Data;

namespace GoogleSpeechToText.Scripts
{
    public class SpeechToTextManager : MonoBehaviour
    {
        // API Key 由 SecretLoader 從 secrets.json 讀取，不再使用 Inspector 欄位

        [Header("Gemini Manager Prefab")]
        public UnityAndGeminiV3 geminiManager;

        private AudioClip clip;
        private byte[] bytes;
        private bool _isRecording = false;

        private float keyDownTime = 0f;
        private float holdThreshold = 0.3f;

        void Update()
        {
            if (Keyboard.current == null) return;

            // 1. 【按下瞬間】
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                keyDownTime = Time.time;
                InterruptAI(); // 改用統一的函式
            }

            // 2. 【持續按住】
            if (Keyboard.current.spaceKey.isPressed)
            {
                if (Time.time - keyDownTime > holdThreshold && !_isRecording)
                {
                    Debug.Log("🎤 [鍵盤] 長按偵測：開始錄音...");
                    StartRecording();
                }
            }

            // 3. 【放開瞬間】
            if (Keyboard.current.spaceKey.wasReleasedThisFrame)
            {
                if (_isRecording)
                {
                    Debug.Log("🛑 [鍵盤] 放開按鍵：停止錄音並送出");
                    StopRecording();
                }
                else
                {
                    Debug.Log("👆 [鍵盤] 短按偵測：僅停止說話，不錄音");
                }
            }
        }

        // 🔥🔥🔥 新增：給按鈕呼叫的中斷函式 🔥🔥🔥
        public void InterruptAI()
        {
            if (geminiManager != null && geminiManager.textToSpeechManager != null)
            {
                // 直接呼叫 textToSpeechManager 裡面的 StopSpeaking()
                geminiManager.textToSpeechManager.StopSpeaking();
                Debug.Log("🤫 噓！Kelly 請安靜");
            }
        }

        // 🔥🔥🔥 變更為 public，讓按鈕可以呼叫 🔥🔥🔥
        public void StartRecording()
        {
            if (_isRecording) return; // 防呆

            // 開始錄音 (使用設備預設麥克風)
            clip = Microphone.Start(null, false, 30, 44100);
            _isRecording = true;
        }

        // 🔥🔥🔥 變更為 public，讓按鈕可以呼叫 🔥🔥🔥
        public void StopRecording()
        {
            if (!_isRecording) return;

            var position = Microphone.GetPosition(null);
            Microphone.End(null);

            // 如果錄音時間太短(沒聲音)，就不處理
            if (position <= 0)
            {
                _isRecording = false;
                return;
            }

            var samples = new float[position * clip.channels];
            clip.GetData(samples, 0);

            bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
            _isRecording = false;

            // 發送請求給 Google STT
            GoogleCloudSpeechToText.SendSpeechToTextRequest(bytes, SecretLoader.GoogleCloudApiKey,
                (response) => {
                    Debug.Log("收到 Google STT 回應: " + response);

                    var speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);

                    if (speechResponse != null && speechResponse.results != null && speechResponse.results.Length > 0)
                    {
                        var transcript = speechResponse.results[0].alternatives[0].transcript;
                        Debug.Log("📜 辨識出的文字: " + transcript);

                        if (geminiManager != null)
                        {
                            Debug.Log("🚀 發送給 Gemini...");
                            geminiManager.SendChat(transcript);
                        }
                        else
                        {
                            Debug.LogError("❌ 斷線了！Inspector 裡的 'Gemini Manager' 欄位是空的！請把它拉進去！");
                        }
                    }
                },
                (error) => {
                    if (error != null)
                        Debug.LogError("STT 錯誤: " + error.message);
                    else
                        Debug.LogError("發生未知 STT 錯誤");
                });
        }

        private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
        {
            using (var memoryStream = new MemoryStream(44 + samples.Length * 2))
            {
                using (var writer = new BinaryWriter(memoryStream))
                {
                    writer.Write("RIFF".ToCharArray());
                    writer.Write(36 + samples.Length * 2);
                    writer.Write("WAVE".ToCharArray());
                    writer.Write("fmt ".ToCharArray());
                    writer.Write(16);
                    writer.Write((ushort)1);
                    writer.Write((ushort)channels);
                    writer.Write(frequency);
                    writer.Write(frequency * channels * 2);
                    writer.Write((ushort)(channels * 2));
                    writer.Write((ushort)16);
                    writer.Write("data".ToCharArray());
                    writer.Write(samples.Length * 2);

                    foreach (var sample in samples)
                    {
                        writer.Write((short)(sample * short.MaxValue));
                    }
                }
                return memoryStream.ToArray();
            }
        }
    }
}