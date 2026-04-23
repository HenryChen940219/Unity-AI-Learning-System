using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class SlideVideoController : MonoBehaviour
{
    [Header("UI設定")]
    public RawImage videoScreen;   // VideoOverlay
    public VideoPlayer videoPlayer; // Video Player

    [System.Serializable]
    public struct PageVideoMapping
    {
        [Tooltip("請輸入主題名稱，例如：Webduino 或 Arduino")]
        public string topicName;   // 🔥 新增：判斷是哪個主題
        public int pageIndex;      // 第幾頁
        public VideoClip clip;     // 要播什麼
    }

    [Header("影片對應表 (請在這邊設定)")]
    public List<PageVideoMapping> videoList;

    void Start()
    {
        // 一開始先隱藏影片層
        videoScreen.gameObject.SetActive(false);

        // 當影片準備好 (Prepare 完成) 時執行
        videoPlayer.prepareCompleted += (source) =>
        {
            videoScreen.texture = source.texture; // 把畫面貼上去
            videoPlayer.Play(); // 開始播放
        };
    }

    // 🔧 給 MainScene 呼叫的函式 (🔥 修改：加入 currentTopic 參數)
    public void OnPageChanged(int currentPage, string currentTopic)
    {
        // 1. 先找找看這一頁有沒有影片
        VideoClip targetClip = null;

        foreach (var item in videoList)
        {
            // 🔥 修改：必須「主題名稱相同」且「頁數相同」才會播放
            if (item.pageIndex == currentPage && item.topicName == currentTopic)
            {
                targetClip = item.clip;
                break;
            }
        }

        // 2. 判斷邏輯 (修正順序，避免報錯)
        if (targetClip != null)
        {
            Debug.Log($"🎬 {currentTopic} 的第 {currentPage} 頁是動態教學，準備播放...");

            // 🔥 關鍵修正：必須先讓物件「醒過來」，VideoPlayer 才能工作！
            videoScreen.gameObject.SetActive(true);

            // 如果怕閃一下上一支影片的殘影，可以先設成全透明 (選用)
            // videoScreen.color = Color.clear; 

            videoPlayer.clip = targetClip;
            videoPlayer.Prepare(); // 這裡現在不會報錯了，因為上面已經 SetActive(true)
        }
        else
        {
            // 這頁沒影片 -> 關閉螢幕、停止播放
            videoPlayer.Stop();
            videoScreen.gameObject.SetActive(false);
        }
    }
}