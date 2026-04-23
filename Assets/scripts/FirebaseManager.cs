using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Text; // 用來組裝字串

public class FirebaseManager : MonoBehaviour
{
    public FirebaseAuth auth;
    public FirebaseUser user;

    DatabaseReference dbRoot;
    bool ready;

    public event Action<string> OnInfoMessage;
    public event Action<string> OnErrorMessage;

    private DateTime infoPanelEnterTime;
    private double totalInfoPanelSeconds = 0;
    private string sessionLoginTime = null;

    // 當前正在進行的課程主題 (Webduino 或 Arduino)
    public string currentTopic = "";

    // 🔥 新增：專門用來群組化「同一趟挑戰」的 Session Key
    private string currentTopicSessionKey = "";

    void Start() { _ = InitFirebase(); }

    async System.Threading.Tasks.Task InitFirebase()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            NotifyError("[Firebase] 依賴未就緒：" + status);
            return;
        }
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        user = auth.CurrentUser;
        dbRoot = FirebaseDatabase.DefaultInstance.RootReference;
        ready = true;
        NotifyInfo("Firebase 已就緒");
    }

    void OnDestroy()
    {
        if (auth != null) auth.StateChanged -= AuthStateChanged;
        UpdateLastLoginOnExit();
        EndRecords();
    }

    void AuthStateChanged(object sender, EventArgs e)
    {
        if (auth.CurrentUser != user)
        {
            user = auth.CurrentUser;
            if (user != null)
            {
                NotifyInfo("登入成功：" + (user.Email ?? ""));
                sessionLoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                EnsureUserNode();
                LoadPreviousStaySecondsAndStart();
                InitLearningStats();
            }
            else
            {
                NotifyInfo("已登出");
                currentTopic = "";
                currentTopicSessionKey = ""; // 登出清除 Key
                EndRecords();
            }
        }
    }

    DatabaseReference GetUserReference()
    {
        if (dbRoot != null && user != null)
            return dbRoot.Child("Users").Child(user.UserId);
        return null;
    }

    public void LogLearningProgress(string stepName)
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic)) return;
        userRef.Child($"records/{currentTopic}_stats/progress/{stepName}").SetValueAsync(true);
    }

    public void LogThemePreference(string themeName)
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic)) return;

        DatabaseReference refTheme = userRef.Child($"records/{currentTopic}_stats/preference/{themeName}");
        refTheme.RunTransaction(mutableData =>
        {
            int current = 0;
            if (mutableData.Value != null) int.TryParse(mutableData.Value.ToString(), out current);
            mutableData.Value = current + 1;
            return TransactionResult.Success(mutableData);
        });
    }

    public void LogPanelDuration(string panelName, double seconds)
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic) || seconds <= 0) return;

        DatabaseReference refTime = userRef.Child($"records/{currentTopic}_stats/stay_duration/{panelName}");
        refTime.RunTransaction(mutableData =>
        {
            double current = 0;
            if (mutableData.Value != null) double.TryParse(mutableData.Value.ToString(), out current);
            mutableData.Value = current + seconds;
            return TransactionResult.Success(mutableData);
        });
    }

    // 🔥🔥🔥 修復：將閱讀時間改為「累加」，並且完讀狀態只要為真就永久為真 🔥🔥🔥
    public void LogReadingStats(double durationSeconds, bool isCompleted)
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic)) return;
        var readingRef = userRef.Child($"records/{currentTopic}_stats/reading");

        // 使用 Transaction 進行累加
        readingRef.Child("total_duration_seconds").RunTransaction(mutableData =>
        {
            double current = 0;
            if (mutableData.Value != null) double.TryParse(mutableData.Value.ToString(), out current);
            mutableData.Value = current + Math.Round(durationSeconds, 2);
            return TransactionResult.Success(mutableData);
        });

        // 完讀狀態只要達成一次 true，就一直保持 true
        readingRef.Child("is_completed").RunTransaction(mutableData =>
        {
            bool current = false;
            if (mutableData.Value != null) bool.TryParse(mutableData.Value.ToString(), out current);
            mutableData.Value = current || isCompleted;
            return TransactionResult.Success(mutableData);
        });
    }

    public void LogWorksheetStats(int retryCount, string aiErrorFeedback, string photoUrl = "")
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic)) return;

        string safeSessionKey = string.IsNullOrEmpty(currentTopicSessionKey) ? "Session_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") : currentTopicSessionKey;

        var worksheetRef = userRef.Child($"records/{currentTopic}_stats/worksheet_history/{safeSessionKey}").Push();

        worksheetRef.Child("timestamp").SetValueAsync(DateTime.Now.ToString("HH:mm:ss"));
        worksheetRef.Child("retry_count").SetValueAsync(retryCount);
        worksheetRef.Child("feedback").SetValueAsync(aiErrorFeedback);

        if (!string.IsNullOrEmpty(photoUrl))
        {
            worksheetRef.Child("snapshot_url").SetValueAsync(photoUrl);
        }
    }

    public void LogQuizStats(int finalScore, double durationSeconds, List<string> wrongCategories)
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic)) return;
        var quizRef = userRef.Child($"records/{currentTopic}_stats/quiz");

        quizRef.Child("score").SetValueAsync(finalScore);
        quizRef.Child("duration_seconds").SetValueAsync(Math.Round(durationSeconds, 2));

        quizRef.Child("wrong_categories").RemoveValueAsync().ContinueWith(task =>
        {
            Dictionary<string, int> currentAttemptErrors = new Dictionary<string, int>();
            foreach (var category in wrongCategories)
            {
                if (currentAttemptErrors.ContainsKey(category))
                    currentAttemptErrors[category]++;
                else
                    currentAttemptErrors[category] = 1;
            }

            foreach (var kvp in currentAttemptErrors)
            {
                quizRef.Child("wrong_categories").Child(kvp.Key).SetValueAsync(kvp.Value);
            }
        });
    }

    public void LogChatMessage(string role, string message)
    {
        var userRef = GetUserReference();
        if (userRef == null || string.IsNullOrEmpty(currentTopic)) return;
        var chatRef = userRef.Child($"records/{currentTopic}_stats/chat_history");
        var newMsgRef = chatRef.Push();
        newMsgRef.Child("timestamp").SetValueAsync(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        newMsgRef.Child("role").SetValueAsync(role);
        newMsgRef.Child("message").SetValueAsync(message);
    }

    void InitLearningStats()
    {
        var userRef = GetUserReference();
        if (userRef == null) return;
        string[] topics = { "Webduino", "Arduino" };
        foreach (var t in topics)
        {
            var statsRef = userRef.Child($"records/{t}_stats");
            statsRef.GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompletedSuccessfully && !task.Result.Exists)
                {
                    statsRef.Child("progress").Child("step1_decompose").SetValueAsync(false);
                }
            });
        }
    }

    public void SetTopic(string topic)
    {
        currentTopic = topic;
        currentTopicSessionKey = "Session_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        SaveTopicVisit(topic);
    }

    public void SaveTopicVisit(string topicName)
    {
        var userRef = GetUserReference();
        if (userRef == null) return;
        string key = "topic_" + topicName;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        userRef.Child($"records/{key}").SetValueAsync(timestamp);
    }

    public void FetchLearningStats(Action<LearningStats> onDone)
    {
        string targetTopic = string.IsNullOrEmpty(currentTopic) ? "Webduino" : currentTopic;
        FetchLearningStats(targetTopic, onDone);
    }

    public void FetchLearningStats(string topic, Action<LearningStats> onDone)
    {
        var userRef = GetUserReference();
        if (userRef == null) { onDone?.Invoke(null); return; }

        userRef.Child($"records/{topic}_stats").GetValueAsync().ContinueWithOnMainThread(t =>
        {
            if (!t.IsCompletedSuccessfully || !t.Result.Exists)
            {
                Debug.LogWarning($"無 {topic} 學習統計資料");
                onDone?.Invoke(null);
                return;
            }

            var stats = new LearningStats();
            var snap = t.Result;

            if (snap.Child("progress").Exists)
            {
                foreach (var child in snap.Child("progress").Children)
                {
                    bool val = false;
                    bool.TryParse(child.Value.ToString(), out val);
                    stats.progress[child.Key] = val;
                }
            }

            if (snap.Child("preference").Exists)
            {
                foreach (var child in snap.Child("preference").Children)
                {
                    int val = 0;
                    int.TryParse(child.Value.ToString(), out val);
                    stats.preference[child.Key] = val;
                }
            }

            if (snap.Child("stay_duration").Exists)
            {
                foreach (var child in snap.Child("stay_duration").Children)
                {
                    double val = 0;
                    double.TryParse(child.Value.ToString(), out val);
                    stats.stay_duration[child.Key] = val;
                }
            }

            if (snap.Child("reading").Exists)
            {
                if (snap.Child("reading/total_duration_seconds").Value != null) double.TryParse(snap.Child("reading/total_duration_seconds").Value.ToString(), out stats.reading_duration);
                if (snap.Child("reading/is_completed").Value != null) bool.TryParse(snap.Child("reading/is_completed").Value.ToString(), out stats.is_reading_completed);
            }

            if (snap.Child("worksheet").Exists)
            {
                if (snap.Child("worksheet/retry_count").Value != null) int.TryParse(snap.Child("worksheet/retry_count").Value.ToString(), out stats.worksheet_retry_count);
                if (snap.Child("worksheet/last_ai_feedback").Value != null) stats.worksheet_ai_feedback = snap.Child("worksheet/last_ai_feedback").Value.ToString();
            }

            if (snap.Child("worksheet_history").Exists)
            {
                StringBuilder historyBuilder = new StringBuilder();
                historyBuilder.AppendLine("\n  <color=#8E44AD><b>[ 歷史批改軌跡 ]</b></color>");

                int totalTries = 0;

                foreach (var sessionNode in snap.Child("worksheet_history").Children)
                {
                    string sessionStr = sessionNode.Key;
                    if (sessionStr.StartsWith("Session_"))
                    {
                        string raw = sessionStr.Substring(8);
                        string[] parts = raw.Split('_');
                        if (parts.Length >= 2)
                        {
                            sessionStr = parts[0] + " " + parts[1].Replace("-", ":");
                        }
                    }

                    historyBuilder.AppendLine($"  <color=#E67E22>挑戰階段：{sessionStr}</color>");

                    int sessionTryCount = 1;

                    foreach (var logNode in sessionNode.Children)
                    {
                        string time = logNode.Child("timestamp").Value?.ToString() ?? "";
                        string fb = logNode.Child("feedback").Value?.ToString() ?? "";

                        historyBuilder.AppendLine($"    <color=#7F8C8D>[{time}] 第 {sessionTryCount} 次：</color> {fb}");

                        sessionTryCount++;
                        totalTries++;
                    }
                    historyBuilder.AppendLine("");
                }

                stats.worksheet_ai_feedback = historyBuilder.ToString().TrimEnd();
                stats.worksheet_retry_count = totalTries;
            }

            if (snap.Child("quiz").Exists)
            {
                if (snap.Child("quiz/score").Value != null) int.TryParse(snap.Child("quiz/score").Value.ToString(), out stats.quiz_score);
                if (snap.Child("quiz/duration_seconds").Value != null) double.TryParse(snap.Child("quiz/duration_seconds").Value.ToString(), out stats.quiz_duration);

                if (snap.Child("quiz/wrong_categories").Exists)
                {
                    foreach (var child in snap.Child("quiz/wrong_categories").Children)
                    {
                        int val = 0;
                        int.TryParse(child.Value.ToString(), out val);
                        stats.quiz_wrong_categories[child.Key] = val;
                    }
                }
            }

            if (snap.Child("chat_history").Exists)
            {
                foreach (var child in snap.Child("chat_history").Children)
                {
                    string time = child.Child("timestamp").Value?.ToString() ?? "";
                    string role = child.Child("role").Value?.ToString() ?? "user";
                    string msg = child.Child("message").Value?.ToString() ?? "";

                    string prefix = (role == "student") ? "<color=#5DADE2><b>[學生]</b></color>" : "<color=#48C9B0><b>[Kelly助教]</b></color>";
                    stats.chat_history.Add($"<size=20><color=#2C3E50>{time}</color></size>\n{prefix} {msg}\n");
                }
            }

            onDone?.Invoke(stats);
        });
    }

    public void Register(string email, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6) { NotifyError("密碼需 >= 6位"); return; }
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted) NotifyError("註冊失敗");
            else { user = task.Result.User; NotifyInfo("註冊成功"); EnsureUserNode(); }
        });
    }

    public void Login(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted) NotifyError("登入失敗");
            else { user = task.Result.User; NotifyInfo("登入成功"); }
        });
    }

    public void Logout()
    {
        if (!ready || auth == null) return;
        UpdateLastLoginOnExit();
        EndRecords();
        auth.SignOut();
        NotifyInfo("已登出");
    }

    void EnsureUserNode()
    {
        var userRef = GetUserReference();
        if (userRef != null) userRef.Child("email").SetValueAsync(user.Email);
    }

    void UpdateLastLoginOnExit()
    {
        if (!string.IsNullOrEmpty(sessionLoginTime)) GetUserReference()?.Child("lastLogin").SetValueAsync(sessionLoginTime);
        sessionLoginTime = null;
    }

    void LoadPreviousStaySecondsAndStart()
    {
        GetUserReference()?.Child("records").GetValueAsync().ContinueWithOnMainThread(t => {
            double prev = 0;
            if (t.IsCompletedSuccessfully && t.Result != null)
            {
                if (t.Result.Child("infoPanelSeconds_raw").Exists) double.TryParse(t.Result.Child("infoPanelSeconds_raw").Value.ToString(), out prev);
            }
            totalInfoPanelSeconds = prev;
            StartRecords();
        });
    }

    void StartRecords() { infoPanelEnterTime = DateTime.Now; }
    void EndRecords()
    {
        if (infoPanelEnterTime == DateTime.MinValue) return;
        TimeSpan stay = DateTime.Now - infoPanelEnterTime;
        totalInfoPanelSeconds += stay.TotalSeconds;
        GetUserReference()?.Child("records").Child("infoPanelSeconds_raw").SetValueAsync(totalInfoPanelSeconds);
        infoPanelEnterTime = DateTime.MinValue;
    }

    public void IncrementCount(string fieldName)
    {
        GetUserReference()?.Child("records/counts/" + fieldName).RunTransaction(data => {
            int val = 0;
            if (data.Value != null) int.TryParse(data.Value.ToString(), out val);
            data.Value = val + 1;
            return TransactionResult.Success(data);
        });
    }

    public void LogEvent(string type, string target) { }

    public void FetchRecords(Action<UserRecords> onDone)
    {
        GetUserReference()?.Child("records").GetValueAsync().ContinueWithOnMainThread(t => {
            if (!t.IsCompletedSuccessfully) { onDone?.Invoke(null); return; }

            var data = new UserRecords();
            data.counts = new Dictionary<string, int>();
            data.events = new List<UserEvent>();

            var snap = t.Result;

            if (snap.Child("infoPanelSeconds_raw").Exists)
                data.infoPanelSeconds = FormatTime(double.Parse(snap.Child("infoPanelSeconds_raw").Value.ToString()));
            else
                data.infoPanelSeconds = "-";

            var countsSnap = snap.Child("counts");
            if (countsSnap.Exists)
            {
                foreach (var child in countsSnap.Children)
                {
                    int val = 0;
                    int.TryParse(child.Value?.ToString(), out val);
                    data.counts[child.Key] = val;
                }
            }

            onDone?.Invoke(data);
        });
    }

    public void FetchLastLogin(Action<string> onDone)
    {
        GetUserReference()?.Child("lastLogin").GetValueAsync().ContinueWithOnMainThread(t => {
            string val = (t.IsCompletedSuccessfully && t.Result.Exists) ? t.Result.Value.ToString() : "-";
            onDone?.Invoke(val);
        });
    }

    string FormatTime(double totalSeconds)
    {
        int minutes = (int)(totalSeconds / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"{minutes}分{seconds}秒";
    }

    void NotifyInfo(string msg) => OnInfoMessage?.Invoke(msg);
    void NotifyError(string msg) => OnErrorMessage?.Invoke(msg);
}

public class UserRecords
{
    public string infoPanelSeconds;
    public Dictionary<string, int> counts;
    public List<UserEvent> events;
}

public class UserEvent
{
    public string type;
    public string target;
    public string time;
}

public class LearningStats
{
    public Dictionary<string, bool> progress = new Dictionary<string, bool>();
    public Dictionary<string, int> preference = new Dictionary<string, int>();
    public Dictionary<string, double> stay_duration = new Dictionary<string, double>();

    public double reading_duration = 0;
    public bool is_reading_completed = false;

    public int worksheet_retry_count = 0;
    public string worksheet_ai_feedback = "";

    public int quiz_score = 0;
    public double quiz_duration = 0;
    public Dictionary<string, int> quiz_wrong_categories = new Dictionary<string, int>();

    public List<string> chat_history = new List<string>();
}