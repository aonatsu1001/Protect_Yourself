using UnityEngine;
using UnityEngine.UI;      // ← 新增：Button
using TMPro;               // ← 新增：TextMeshPro

public class GameFlowController : MonoBehaviour
{
    [Header("設定アセット")]
    public SurvivalConfig config;

    [Header("内部状態（デバッグ表示用）")]
    public PlayerState state;

    [Header("UI")]
    public TMP_Text statusText;     // ← 绑定 Canvas 里的文字（可空，为空则不刷新）
    public Button   nextDayButton;  // ← 绑定 Canvas 里的按钮（可空，为空则不注册）

    [Tooltip("自動で1日ずつ進める（テスト用）")]
    public bool autoSimulate = false;
    [Tooltip("自動進行のインターバル秒")]
    public float autoTickSeconds = 1.0f;

    float timer;

    void Start()
    {
        if (config == null)
        {
            Debug.LogError("SurvivalConfig が未設定です。");
            enabled = false;
            return;
        }

        // 初期化
        state = new PlayerState(config.initialFoodMeals, config.initialWaterLiters);
        Debug.Log("[GameFlow] ゲーム開始\n" + state.Summary());

        // ← 这里把 UI 事件与首次显示接上
        if (nextDayButton != null)
            nextDayButton.onClick.AddListener(OnNextDay);

        UpdateUI();   // 首次刷新 UI
    }

    void Update()
    {
        if (!autoSimulate) return;

        timer += Time.deltaTime;
        if (timer >= autoTickSeconds)
        {
            timer = 0f;
            AdvanceOneDay(); // 仍然使用你原来的推进1天方法
        }
    }

    // ==== UI用入口：按钮点击推进一天 ====
    public void OnNextDay()
    {
        AdvanceOneDay();
        UpdateUI();
    }

    // ==== 原有推进一天逻辑，保持不变 ====
    public void AdvanceOneDay()
    {
        if (!state.alive)
        {
            ShowResult();
            return;
        }

        state.ConsumeOneDay(config);
        Debug.Log($"[GameFlow] 1日経過\n{state.Summary()}");

        if (!state.alive)
        {
            ShowResult();
        }
    }

    // ==== 刷新文本（可根据需要自定义显示格式）====
    void UpdateUI()
    {
        if (statusText == null) return;

        // 用你已有的状态构建文案
        statusText.text =
            $"Day {state.dayElapsed}\n" +
            $"食料: {state.foodMeals} 食\n" +
            $"水: {state.waterLiters:0.0} L\n" +
            $"状態: {(state.alive ? "生存" : "死亡")}";

        // 如果已经死亡，可以顺便禁用按钮
        if (nextDayButton != null)
            nextDayButton.interactable = state.alive;
    }

    // ==== 原有结果逻辑，保持不变 ====
    public void ShowResult()
    {
        var msg =
            $"【リザルト】\n" +
            $"経過日数: {state.dayElapsed}\n" +
            $"最終状態: {(state.alive ? "生存" : "死亡")} / " +
            $"残フード:{state.foodMeals}食 / 残水:{state.waterLiters:0.0}L";

        Debug.Log(msg);

        // TODO: UI遷移 or ポップアップ表示
        // SceneManager.LoadScene("ResultScene");
        // あるいは UIController.ShowResult(state);
    }

    // ==== 后续联动，保持不变 ====
    public void AddFoodMeals(int addMeals) => state.foodMeals += Mathf.Max(0, addMeals);
    public void AddWaterLiters(float addL) => state.waterLiters += Mathf.Max(0f, addL);
}
