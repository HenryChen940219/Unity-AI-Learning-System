using System;
using GoogleTextToSpeech.Scripts.Data;
using TMPro;
using UnityEngine;

namespace GoogleTextToSpeech.Scripts.Example
{
    public class TextToSpeechExample : MonoBehaviour
    {
        // ❌ 原本的寫法：[SerializeField] private VoiceScriptableObject voice;
        // ✅ 現在的寫法：拿掉 SerializeField，不讓它出現在 Inspector
        private VoiceScriptableObject voice;

        [SerializeField] private TextToSpeech textToSpeech;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private TextMeshProUGUI inputField;

        private Action<AudioClip> _audioClipReceived;
        private Action<BadRequestData> _errorReceived;

        private void Start()
        {
            if (voice == null)
            {
                voice = ScriptableObject.CreateInstance<VoiceScriptableObject>();
                voice.languageCode = "cmn-TW";

                // 改成 B (男生)
                voice.name = "cmn-TW-Wavenet-B";

                // 男聲通常不需要調音高，預設就很穩了
                voice.pitch = 0f;
                voice.speed = 0.9f; // 稍微慢一點點就好

                Debug.Log("✅ TTS Manager: 已設定為「男聲助教」");
            }
        }

        public void PressBtn()
        {
            // 防呆：如果按鈕按太快，先清空之前的事件
            _errorReceived = null;
            _audioClipReceived = null;

            _errorReceived += ErrorReceived;
            _audioClipReceived += AudioClipReceived;

            // 呼叫 Google API
            textToSpeech.GetSpeechAudioFromGoogle(inputField.text, voice, _audioClipReceived, _errorReceived);
        }

        private void ErrorReceived(BadRequestData badRequestData)
        {
            Debug.LogError($"TTS Error {badRequestData.error.code} : {badRequestData.error.message}");
        }

        private void AudioClipReceived(AudioClip clip)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}