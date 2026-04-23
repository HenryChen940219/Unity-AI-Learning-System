using UnityEngine;

public class ARCharacterLoop : MonoBehaviour
{
    public Transform lampTransform; // 拖入壁燈物件
    public Light lampLight;         // 拖入壁燈裡的 Light 元件
    public float moveSpeed = 0.5f;  // 移動速度
    public float maxDistance = 3.0f; // 您想要設定的走動路程
    public float detectRange = 1.0f; // 感應距離

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position; // 紀錄出發點
    }

    void Update()
    {
        // 1. 往前方移動
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);

        // 2. 檢查距離重置
        float distFromStart = Vector3.Distance(transform.position, startPosition);
        if (distFromStart >= maxDistance)
        {
            transform.position = startPosition; // 瞬移回原點
        }

        // 3. 處理燈光感應
        if (lampTransform != null && lampLight != null)
        {
            float distToLamp = Vector3.Distance(transform.position, lampTransform.position);
            lampLight.enabled = (distToLamp <= detectRange); // 夠近就亮，太遠就熄
        }
    }
}