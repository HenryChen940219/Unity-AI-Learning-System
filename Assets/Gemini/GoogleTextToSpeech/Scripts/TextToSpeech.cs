using System;
using GoogleTextToSpeech.Scripts.Data;
using UnityEngine;
using Input = GoogleTextToSpeech.Scripts.Data.Input;

namespace GoogleTextToSpeech.Scripts
{
    public class TextToSpeech : MonoBehaviour
    {
        // API Key 由 SecretLoader 從 secrets.json 讀取，不再使用 Inspector 欄位

        private RequestService _requestService;
        private static AudioConverter _audioConverter;

        // 🔧 修改：直接傳入 callback，完全移除 event += 造成的重複訂閱問題
        public void GetSpeechAudioFromGoogle(string textToConvert, VoiceScriptableObject voice, Action<AudioClip> audioClipReceived, Action<BadRequestData> errorReceived)
        {
            if (_requestService == null) _requestService = gameObject.AddComponent<RequestService>();
            if (_audioConverter == null) _audioConverter = gameObject.AddComponent<AudioConverter>();

            var dataToSend = new DataToSend
            {
                input = new Input() { text = textToConvert },
                voice = new Voice() { languageCode = voice.languageCode, name = voice.name },
                audioConfig = new AudioConfig()
                {
                    audioEncoding = "LINEAR16",
                    pitch = voice.pitch,
                    speakingRate = voice.speed
                }
            };

            // 建立一個一次性的回傳通道
            Action<string> onRequestSuccess = (requestData) =>
            {
                var audioData = JsonUtility.FromJson<AudioData>(requestData);
                AudioConverter.SaveTextToMp3(audioData);
                _audioConverter.LoadClipFromMp3(audioClipReceived);
            };

            RequestService.SendDataToGoogle("https://texttospeech.googleapis.com/v1/text:synthesize", dataToSend, SecretLoader.GoogleCloudApiKey, onRequestSuccess, errorReceived);
        }
    }
}