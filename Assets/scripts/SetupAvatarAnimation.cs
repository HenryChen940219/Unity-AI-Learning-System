using UnityEngine;

public class SetupAvatarAnimation : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("請將角色的 Animator 元件拖到這裡")]
    public Animator avatarAnimator;

    private void Start()
    {
        // 防呆機制：如果沒拉，試著自己抓抓看
        if (avatarAnimator == null)
        {
            avatarAnimator = GetComponent<Animator>();
        }

        if (avatarAnimator != null)
        {
            // 1. 我們不強制換動畫，保留原本的設定
            // avatarAnimator.runtimeAnimatorController = ...; //這行刪掉，就不會變形了

            // 2. 只確保動畫機是「開啟」的狀態
            if (!avatarAnimator.enabled)
            {
                avatarAnimator.enabled = true;
            }

            // 3. 確保位置不被鎖死 (通常 RPM 人物需要這個)
            avatarAnimator.applyRootMotion = false;

            Debug.Log("✅ 角色動畫機已啟動，使用預設狀態。");
        }
        else
        {
            Debug.LogError("⚠️ 找不到 Animator！請確認人物身上有 Animator 元件。");
        }
    }
}