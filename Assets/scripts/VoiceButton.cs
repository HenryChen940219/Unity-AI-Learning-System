using UnityEngine;
using UnityEngine.EventSystems;
using GoogleSpeechToText.Scripts; // 引用您麥克風管理器的命名空間

public class VoiceButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("綁定您的錄音管理器")]
    public SpeechToTextManager sttManager;

    [Header("短按判定的時間差")]
    public float shortClickThreshold = 0.3f;

    private float pressTime;
    private bool isHolding = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (sttManager == null) return;

        pressTime = Time.time;
        isHolding = true;

        // 視覺回饋：按鈕微微縮小
        transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

        // 👉 按下的瞬間，先讓 AI 閉嘴
        sttManager.InterruptAI();
        Debug.Log("📱 [按鈕] 按下：已發送中斷訊號");
    }

    void Update()
    {
        // 👉 持續按住超過門檻，才真正開始錄音
        if (isHolding && Time.time - pressTime > shortClickThreshold)
        {
            sttManager.StartRecording();
            isHolding = false; // 確保 StartRecording 只呼叫一次
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (sttManager == null) return;

        isHolding = false;

        // 恢復按鈕原本大小
        transform.localScale = new Vector3(1f, 1f, 1f);

        float holdDuration = Time.time - pressTime;

        if (holdDuration < shortClickThreshold)
        {
            Debug.Log("📱 [按鈕] 短按放開 (不做任何事，剛才已中斷說話)");
        }
        else
        {
            Debug.Log("📱 [按鈕] 長按放開：停止錄音並送出");
            // 👉 放開時停止錄音並送出
            sttManager.StopRecording();
        }
    }
}