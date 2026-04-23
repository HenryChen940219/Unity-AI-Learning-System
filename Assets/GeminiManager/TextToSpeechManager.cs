using UnityEngine;
using GoogleTextToSpeech.Scripts.Data;
using System;

namespace GoogleTextToSpeech.Scripts
{
    public class TextToSpeechManager : MonoBehaviour
    {
        [Header("設定")]
        [SerializeField] private TextToSpeech textToSpeech;
        [SerializeField] private AudioSource audioSource;

        private VoiceScriptableObject voice;

        private void Start()
        {
            if (voice == null)
            {
                voice = ScriptableObject.CreateInstance<VoiceScriptableObject>();
                voice.languageCode = "cmn-TW";
                voice.name = "cmn-TW-Wavenet-A";
                voice.pitch = -2.0f;
                voice.speed = 0.85f;
            }
        }

        // 🛑 新增這個功能：給其他按鈕呼叫用的「強制閉嘴」
        public void StopSpeaking()
        {
            if (audioSource != null)
            {
                audioSource.Stop(); // 1. 停止播放
                audioSource.clip = null; // 2. 把錄音帶拿出來 (關鍵！)
            }
        }

        public void SendTextToGoogle(string _text)
        {
            if (textToSpeech == null) textToSpeech = GetComponent<TextToSpeech>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            StopSpeaking(); // 講新話之前，先清空舊的

            textToSpeech.GetSpeechAudioFromGoogle(_text, voice, AudioClipReceived, ErrorReceived);
        }

        private void ErrorReceived(BadRequestData badRequestData)
        {
            Debug.LogError($"TTS Error: {badRequestData.error.message}");
        }

        private void AudioClipReceived(AudioClip clip)
        {
            StopSpeaking(); // 雙重保險
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}