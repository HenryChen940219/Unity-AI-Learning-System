//using UnityEngine;
//using Vuforia;

//public class SimpleARHandler : MonoBehaviour
//{
//    [Tooltip("請把 MainScene 拖曳到這裡")]
//    public MainScene mainScene;

//    // 🆕 新增這個變數：這張圖代表第幾關？(0, 1, 2...)
//    [Tooltip("這張圖對應第幾關？(0=第一關, 1=第二關)")]
//    public int targetID = 0;

//    // 當 Vuforia 掃描到圖片時會自動呼叫這個函式
//    public void OnTargetFound()
//    {
//        Debug.Log($"📷 掃描到圖片 ID: {targetID}");

//        if (mainScene != null)
//        {
//            // 🛑 修正錯誤的地方：把 ID 傳進去
//            mainScene.OnARImageFound(targetID);
//        }
//    }

//    // 為了相容 Vuforia 的事件系統，保留這個給 Unity Event 用 (如果有的話)
//    // 但如果你是用腳本直接呼叫，上面那個就夠了
//}