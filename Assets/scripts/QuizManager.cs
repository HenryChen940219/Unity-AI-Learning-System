using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class QuizManager : MonoBehaviour
{
    [Header("UI 綁定")]
    public GameObject panelQuiz;
    public TMP_Text textTitle;       // 顯示 "第 X 題 / 共 10 題"
    public TMP_Text textQuestion;    // 顯示題目
    public Button[] btnOptions;      // 4 個選項按鈕
    public TMP_Text[] textOptions;   // 4 個選項的文字

    [Header("控制按鈕")]
    public Button btnNext;           // 下一題 / 查看成績的按鈕
    public Button btnExit;           // 離開測驗的按鈕

    [Header("串接回主程式")]
    public MainScene mainScene;      // 用來取得當前主題並在測驗結束後關閉面板

    [Header("Firebase 紀錄")]
    public FirebaseManager firebaseManager;

    // 題目資料結構
    private class QuestionData
    {
        public string question;
        public string[] options;
        public int correctIndex;
        public string rationale;
    }

    private List<QuestionData> questionBank = new List<QuestionData>();
    private int currentQuestionIndex = 0;
    private int score = 0;

    // 測驗歷程追蹤變數
    private float quizStartTime = 0f;
    private List<string> wrongCategoriesList = new List<string>();

    void Start()
    {
        // 隱藏面板與綁定按鈕
        if (panelQuiz) panelQuiz.SetActive(false);
        if (btnNext) btnNext.onClick.AddListener(OnNextButtonClicked);
        if (btnExit) btnExit.onClick.AddListener(EndQuiz);

        // 綁定 4 個選項按鈕的點擊事件
        for (int i = 0; i < btnOptions.Length; i++)
        {
            int index = i; // 避免 Closure 問題
            btnOptions[i].onClick.AddListener(() => OnOptionSelected(index));
        }
    }

    // 🔥 提供給 MainScene 呼叫的入口點 🔥
    public void StartQuiz()
    {
        panelQuiz.SetActive(true);
        currentQuestionIndex = 0;
        score = 0;

        if (btnExit) btnExit.GetComponentInChildren<TMP_Text>().text = "X";

        // 紀錄：開始測驗計時，並清空錯誤清單
        quizStartTime = Time.time;
        wrongCategoriesList.Clear();

        // 🔥🔥🔥 動態判斷當前主題並載入對應題庫 🔥🔥🔥
        string currentTopic = mainScene != null ? mainScene.GetCurrentTopic() : "Webduino";
        LoadQuestions(currentTopic);

        ShowQuestion();
    }

    private void ShowQuestion()
    {
        if (currentQuestionIndex >= questionBank.Count) return;

        // 🔥 動態切換標題顯示的名稱
        string currentTopic = mainScene != null ? mainScene.GetCurrentTopic() : "Webduino";

        QuestionData q = questionBank[currentQuestionIndex];
        textTitle.text = $"{currentTopic} 課後練習 ( {currentQuestionIndex + 1} / {questionBank.Count} )";
        textQuestion.text = q.question;

        btnNext.gameObject.SetActive(false);
        btnExit.gameObject.SetActive(true);

        // 設定選項文字並打開按鈕
        for (int i = 0; i < btnOptions.Length; i++)
        {
            if (i < q.options.Length)
            {
                btnOptions[i].gameObject.SetActive(true);
                btnOptions[i].interactable = true; // 恢復可點擊
                textOptions[i].text = q.options[i];

                // 恢復為指定的深藍灰色 (#34495E) 與白色文字 (#FFFFFF)
                btnOptions[i].GetComponent<Image>().color = new Color32(52, 73, 94, 255);
                textOptions[i].color = Color.white;
            }
            else
            {
                btnOptions[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnOptionSelected(int selectedIndex)
    {
        QuestionData q = questionBank[currentQuestionIndex];

        // 鎖定所有按鈕不讓玩家反悔修改
        foreach (Button btn in btnOptions) btn.interactable = false;

        // 判斷對錯，並把回饋文字直接加到按鈕上
        if (selectedIndex == q.correctIndex)
        {
            score += 10;
            textOptions[selectedIndex].text += $"\n\n<color=#005500><b>答對了！</b>{q.rationale}</color>";
            btnOptions[selectedIndex].GetComponent<Image>().color = new Color(0.5f, 1f, 0.5f); // 淺綠色
        }
        else
        {
            // 答錯了：點錯的按鈕加上紅色提示
            textOptions[selectedIndex].text += "\n\n<color=#AA0000><b>❌ 答錯了。</b></color>";
            btnOptions[selectedIndex].GetComponent<Image>().color = new Color(1f, 0.5f, 0.5f); // 淺紅色

            // 同時，在正確的按鈕上顯示完整解析
            textOptions[q.correctIndex].text += $"\n\n<color=#005500><b>正確答案！</b>{q.rationale}</color>";
            btnOptions[q.correctIndex].GetComponent<Image>().color = new Color(0.5f, 1f, 0.5f); // 淺綠色

            // 紀錄：抓取題目的類別名稱 (例如：樣式辨識、演算法)，並加入錯誤清單
            string category = "未分類";
            int startIndex = q.question.IndexOf("【");
            int endIndex = q.question.IndexOf("】");
            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                category = q.question.Substring(startIndex + 1, endIndex - startIndex - 1);
            }
            wrongCategoriesList.Add(category);
        }

        // 顯示下一步按鈕
        btnNext.gameObject.SetActive(true);
        if (btnNext) btnNext.GetComponentInChildren<TMP_Text>().text = (currentQuestionIndex == questionBank.Count - 1) ? "查看成績" : "下一題";
    }

    private void OnNextButtonClicked()
    {
        currentQuestionIndex++;

        if (currentQuestionIndex < questionBank.Count)
        {
            ShowQuestion();
        }
        else
        {
            // ===== 測驗結束：顯示成績畫面 =====
            textTitle.text = "測驗完成！";
            textQuestion.text = $"你的總分是：<size=60><color=#FF8800>{score}</color></size> / 100\n\n恭喜你完成所有的概念構圖與挑戰！";

            // 結算並上傳 Firebase
            float duration = Time.time - quizStartTime;
            if (firebaseManager != null)
            {
                firebaseManager.LogQuizStats(score, duration, wrongCategoriesList);
                Debug.Log($"📊 [紀錄] 測驗結束上傳！分數: {score}, 總時間: {duration:F1}秒, 錯誤題型數量: {wrongCategoriesList.Count}");
            }

            // 隱藏選項
            foreach (Button btn in btnOptions) btn.gameObject.SetActive(false);

            // 隱藏「下一題」按鈕
            btnNext.gameObject.SetActive(false);

            // 把「離開」按鈕改成「X」或結束學習
            if (btnExit) btnExit.GetComponentInChildren<TMP_Text>().text = "X";
        }
    }

    private void EndQuiz()
    {
        panelQuiz.SetActive(false);
        // 呼叫 MainScene 原本的關閉邏輯
        if (mainScene != null)
        {
            mainScene.CloseWebduinoSlide();
        }
    }

    // ==========================================
    // 動態載入雙題庫系統
    // ==========================================
    private void LoadQuestions(string topic)
    {
        questionBank.Clear(); // 每次載入前先清空題庫

        if (topic == "Arduino")
        {
            // 💡 Arduino 專屬題庫 (10 題)
            questionBank.Add(new QuestionData
            {
                question = "【演算法】在 Arduino 程式中，`setup()` 區塊內的程式碼只會在開機或重置時執行一次，而 `loop()` 區塊內的程式碼會不斷重複執行。",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 0,
                rationale = "setup() 用於一次性初始設定，loop() 負責無窮盡地循環運作。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【樣式辨識】下列哪一個感測器測量到的數值屬於連續變化的「類比訊號 (Analog)」？",
                options = new string[] { "光敏電阻", "按鈕開關", "碰撞開關", "紅外線避障感測器" },
                correctIndex = 0,
                rationale = "光敏電阻偵測光線強弱作漸進變化，屬於類比訊號。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【抽象化】若想讓 LED 燈產生「漸暗漸亮」的效果（例如呼吸燈），應該將它接在 Arduino 的哪一種腳位上？",
                options = new string[] { "5V 電源腳位", "一般數位腳位 (Digital)", "帶有波浪號 (~) 的 PWM 腳位", "GND 接地腳位" },
                correctIndex = 2,
                rationale = "帶波浪號的 PWM 腳位能模擬不同電壓，控制 LED 亮度漸變。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【樣式辨識】蜂鳴器如果只需要發出單一頻率的警告音（也就是單純的開與關），接在一般的數位腳位 (Digital) 上就可以了。",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 0,
                rationale = "只要控制元件的「開 (HIGH)」與「關 (LOW)」，使用一般數位腳位即可。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【樣式辨識】關於「超音波感測器」的應用，下列敘述何者最正確？",
                options = new string[] { "用來偵測有沒有人經過", "發射與接收聲波，藉此計算與前方障礙物的距離", "測量環境的溫度與濕度", "偵測地上的黑線或白線" },
                correctIndex = 1,
                rationale = "超音波利用聲波反射時間差來計算距離，常用於倒車雷達。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【抽象化】在接線時，電子元件的 GND（接地/負極）腳位可接可不接，只要有接 VCC（正極）就能正常運作。",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 1,
                rationale = "電流必須從正極流向負極形成完整迴路，沒有 GND 無法通電。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【演算法】在程式設計中，使用「如果 (條件) ... 就 (動作) ... 否則 (其他動作)」的語法，主要是為了達成什麼目的？",
                options = new string[] { "設定腳位是輸入還是輸出", "讓程式具備邏輯判斷的能力", "將程式碼上傳到開發板", "讀取感測器的數值" },
                correctIndex = 1,
                rationale = "條件判斷 (if/else) 負責讓程式根據條件來決定下一步該做什麼。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【演算法】當我們需要啟動「序列埠」來觀察感測器回傳的數值時，通常會在哪個區塊寫下 `Serial.begin()` 這行指令？",
                options = new string[] { "loop()", "setup()", "if 判斷式內", "程式最結尾" },
                correctIndex = 1,
                rationale = "序列埠啟動屬於「初始化」工作，必須寫在只執行一次的 setup()。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【拆解問題】在設計「智慧檯燈」時，當光敏電阻偵測環境變暗（數值變低），我們應設定「如果數值小於某個門檻，就點亮 LED 燈」。",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 0,
                rationale = "比較感測器數值與設定的門檻值來觸發作動器，是標準的條件邏輯。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【抽象化】在實作 Arduino 專題時，接線如果有錯，頂多就是程式不會動而已，絕對不會燒毀元件。這句話對嗎？",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 1,
                rationale = "接線錯誤 (如正負極短路) 非常危險，極有可能燒毀感測器或開發板。"
            });
        }
        else
        {
            // 💡 Webduino 專屬題庫 (10 題)
            questionBank.Add(new QuestionData
            {
                question = "【樣式辨識】小華想要製作一個「隨著環境越暗，燈光就自動越亮」的智慧小夜燈。請問在選擇「光敏電阻」的訊號類型時，他應該選擇哪一種才能達到「漸進式變亮」的效果？",
                options = new string[] { "數位訊號 (Digital)", "類比訊號 (Analog)", "Wi-Fi 訊號", "藍牙訊號" },
                correctIndex = 1,
                rationale = "類比能讀取0~1023連續數值，反映光線強弱作漸層變化。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【拆解問題】在設計「防盜警報器」時，我們需要釐清輸入與輸出的關係。下列哪一組元件的角色分配是正確的？",
                options = new string[] { "輸入: 蜂鳴器 / 輸出: 紅外線感測", "輸入: 三色LED / 輸出: 超音波", "輸入: 人體紅外線 / 輸出: LED燈", "輸入: 馬達 / 輸出: 按鈕" },
                correctIndex = 2,
                rationale = "紅外線負責偵測環境(輸入)，LED燈負責產生反應(輸出)。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【演算法】程式寫：「如果 (溫度 > 30度) 就 (打開風扇)；如果 (溫度 < 30度) 就 (關閉風扇)」。請問當溫度剛好等於 30 度時，會發生什麼事？",
                options = new string[] { "風扇自動打開", "風扇自動關閉", "程式漏洞，可能什麼都不做", "風扇會燒壞" },
                correctIndex = 2,
                rationale = "沒有定義「等於30」時該做什麼，導致邊界條件的程式漏洞。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【樣式辨識】若要製作一個「倒車雷達」，需要偵測車子與後方牆壁的「距離」，哪一個感測器最適合？",
                options = new string[] { "溫濕度感測器", "人體紅外線感測器", "光敏電阻", "超音波感測器" },
                correctIndex = 3,
                rationale = "超音波感測器利用聲波反射時間差來計算距離，適合測距。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【抽象化】如果 LED 燈接在開發板的「10號腳位」，但在程式中卻設定成「5號腳位」，會發生什麼事？",
                options = new string[] { "電腦會自動偵測並亮起", "LED 不會亮，控制訊號送錯地方", "LED 會閃爍", "開發板會當機" },
                correctIndex = 1,
                rationale = "程式對5號送電，但燈接在10號，所以完全收不到訊號。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【運算思維】「拆解問題」的主要目的是將大任務切割成許多小任務，以便更容易解決。這句話對嗎？",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 0,
                rationale = "這正是拆解問題的核心定義，將困難的大問題化整為零。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【演算法】關於「如果... 否則...」邏輯，下列敘述何者正確？",
                options = new string[] { "「否則」在條件成立時執行", "「否則」處理條件不成立的所有情況", "一定要搭配「否則」才能運作", "「否則」裡必須再放一個如果" },
                correctIndex = 1,
                rationale = "當If條件不滿足時，程式就會自動執行Else(否則)的動作。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【基礎知識】Webduino 開發板必須透過 Wi-Fi 連上網路，才能夠接收網頁上的積木程式指令。這句話對嗎？",
                options = new string[] { "正確", "錯誤" },
                correctIndex = 0,
                rationale = "Webduino 的特色是雲端控制，必須連網才能與網頁溝通。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【除錯】聲控燈程式：「如果 (聲音 > 0) 就 (開燈)」。結果燈一直亮著關不掉，最可能的原因是？",
                options = new string[] { "聲音感測器壞了", "環境有背景音，應提高門檻值", "電壓太強", "不支援聲音感測" },
                correctIndex = 1,
                rationale = "環境極少完全靜音，大於0極易被底噪觸發，應提高門檻值。"
            });

            questionBank.Add(new QuestionData
            {
                question = "【樣式辨識】「作動器 (Actuator)」通常是指下列哪一類元件？",
                options = new string[] { "蒐集環境資料的元件", "提供電力的元件", "運算邏輯的晶片", "接收指令並產生動作的元件" },
                correctIndex = 3,
                rationale = "作動器負責「做」出反應，例如馬達或LED，是系統的輸出端。"
            });
        }
    }
}