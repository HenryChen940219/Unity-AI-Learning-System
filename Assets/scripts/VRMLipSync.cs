using UnityEngine;
using VRM;

public class VRMLipSync : MonoBehaviour
{
    [Header("聲音來源 (拖曳角色的 AudioSource 到這裡)")]
    public AudioSource audioSource;

    [Header("嘴巴張開的靈敏度 (越大張越開，建議 100~500)")]
    public float sensitivity = 300f; // 預設幫您調高到 300 了！

    [Header("嘴巴開合的平滑度 (越低越柔和，建議 15~25)")]
    public float smoothness = 20f;

    private VRMBlendShapeProxy blendShapeProxy;
    private float[] samples = new float[256];
    private float currentMouthOpen = 0f; // 用來記錄當前的嘴型大小，做平滑過渡

    void Start()
    {
        blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (blendShapeProxy == null || audioSource == null) return;

        float targetMouthOpen = 0f; // 目標嘴型大小

        if (audioSource.isPlaying)
        {
            // 獲取聲音頻譜數據
            audioSource.GetSpectrumData(samples, 0, FFTWindow.Rectangular);

            float volume = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                volume += samples[i];
            }
            volume /= samples.Length; // 算出平均音量

            // 計算目標張開幅度，乘以靈敏度
            targetMouthOpen = Mathf.Clamp01(volume * sensitivity);
        }

        // 🔥 關鍵魔法：使用 Lerp 讓嘴巴從「現在的大小」滑順地過渡到「目標大小」
        currentMouthOpen = Mathf.Lerp(currentMouthOpen, targetMouthOpen, Time.deltaTime * smoothness);

        // 套用 'A' (啊) 嘴型
        blendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.A), currentMouthOpen);
    }
}