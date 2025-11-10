using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace DisasterGame.UI
{
    public class ResultSceneController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text survivedDaysText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button backToTitleButton;

        [Header("Selected Items (finalData)")]
        [SerializeField] private ScrollRect selectedItemsScroll;
        [SerializeField] private SelectedItemRow rowPrefab;
        [SerializeField] private TMP_Text totalPriceText;
        [SerializeField] private GameObject emptyPlaceholder;

        [Header("Suggested Items")]
        [SerializeField] private SuggestedNextItemsUI suggestedItemsUI;

        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "ItemSelectScene";
        [SerializeField] private string titleSceneName = "Title";

        [Header("Loading UI")]
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private float loadingMinSeconds = 4f; // ローディングの最低表示時間（実時間）

        [Header("Reward")]
        [SerializeField] private GameObject rewardPopupRoot;     // 中央に出すパネル（Canvas 内）
        [SerializeField] private TMP_Text rewardPopupText;       // 「楽天ポイント GET!」テキスト
        [SerializeField] private ParticleSystem confettiLeft;    // 紙吹雪（任意）
        [SerializeField] private ParticleSystem confettiRight;   // 紙吹雪（任意）
        [SerializeField] private AudioSource rewardSfx;          // 効果音（任意）
        [SerializeField] private float rewardShowTime = 0.7f;    // フェードイン
        [SerializeField] private float rewardHoldTime = 1.0f;    // 表示キープ
        [SerializeField] private float rewardHideTime = 0.5f;    // フェードアウト
        [SerializeField] private TMP_Text displayPointText;      // 獲得楽天ポイント表示
        private bool rewardShown = false;

        [Header("Count Up Effect")]
        [SerializeField] private bool countUpDays = true;
        [SerializeField] private float countUpDuration = 0.8f;

        // ===== 内部状態 =====
        private Coroutine masterRoutine;     // 全体の1本化したコルーチン
        private Coroutine countUpRoutine;    // 日数のカウントアップだけ個別停止
        private bool evalInFlight = false;   // AI評価の二重起動ガード

        private void Awake()
        {
            if (retryButton) retryButton.onClick.AddListener(OnClickRetry);
            if (backToTitleButton) backToTitleButton.onClick.AddListener(OnClickBackToTitle);
        }

        private void OnEnable()
        {
            // 全体の進行は常に1本のみ
            if (masterRoutine != null) StopCoroutine(masterRoutine);
            masterRoutine = StartCoroutine(MasterSequence());
        }

        private void OnDisable()
        {
            if (masterRoutine != null) { StopCoroutine(masterRoutine); masterRoutine = null; }
            if (countUpRoutine != null) { StopCoroutine(countUpRoutine); countUpRoutine = null; }
        }

        /// <summary>
        /// ★親コルーチン：ローディング → データ取得・描画 → ローディング閉じ
        ///   - 入れ子の StartCoroutine をやめ、すべて `yield return` で直に待つ
        ///   - 最低表示時間もここで担保
        /// </summary>
        private IEnumerator MasterSequence()
        {
            ShowLoading("災害発生中…");

            float startRt = Time.realtimeSinceStartup;

            // 最低表示時間（実時間）を満たす
            float elapsed = Time.realtimeSinceStartup - startRt;
            float remain = loadingMinSeconds - elapsed;
            if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

            // データ生成・描画（直に待つ）
            yield return RunSequence();

            HideLoading();
        }

        // データ生成→結果反映→選択アイテム描画
        private IEnumerator RunSequence()
        {
            yield return GetAndShowFeedbackFromPayload(); // ← 直に待つ（StartCoroutineしない）

            if (GameSessionResult.HasData)
            {
                ShowResult(GameSessionResult.DaysSurvived, GameSessionResult.Feedback);
            }
            else
            {
                ShowResult(0, "結果データが見つかりませんでした。");
            }

            RenderSelectedItems();
        }

        /// <summary>
        /// 外部から直接呼んで表示更新したい場合に使えます。
        /// StopAllCoroutines は使わず、必要なコルーチン（countUp）だけ停止。
        /// </summary>
        public void ShowResult(int daysSurvived, string feedback)
        {
            if (survivedDaysText)
            {
                if (countUpRoutine != null)
                {
                    StopCoroutine(countUpRoutine);
                    countUpRoutine = null;
                }

                if (countUpDays && daysSurvived > 0 && gameObject.activeInHierarchy)
                {
                    countUpRoutine = StartCoroutine(CountUpDaysCoroutine(daysSurvived));
                }
                else
                {
                    survivedDaysText.text = $"生存日数 {daysSurvived}日";
                    if (displayPointText) displayPointText.text = $"{daysSurvived} 楽天ポイントゲット！";
                }
            }

            if (feedbackText) feedbackText.text = feedback ?? "";
        }

        private IEnumerator CountUpDaysCoroutine(int target)
        {
            float t = 0f;
            int last = -1;
            while (t < countUpDuration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.Clamp01(t / countUpDuration);
                int current = Mathf.RoundToInt(Mathf.Lerp(0, target, ratio));
                if (current != last)
                {
                    if (survivedDaysText) survivedDaysText.text = $"生存日数 {current}日";
                    if (displayPointText) displayPointText.text = $"{current} 楽天ポイントゲット！";
                    last = current;
                }
                yield return null;
            }

            if (survivedDaysText) survivedDaysText.text = $"生存日数 {target}日";
            if (displayPointText) displayPointText.text = $"{target} 楽天ポイントゲット！";
            TryShowRakutenReward(target);
        }

        private void TryShowRakutenReward(int days)
        {
            if (rewardShown) return;
            if (days > 0 && isActiveAndEnabled)
            {
                rewardShown = true;
                StartCoroutine(PlayRakutenReward());
            }
        }

        private IEnumerator PlayRakutenReward()
        {
            if (!rewardPopupRoot) yield break;

            var cg = rewardPopupRoot.GetComponent<CanvasGroup>();
            if (!cg) cg = rewardPopupRoot.AddComponent<CanvasGroup>();

            if (rewardPopupText) rewardPopupText.text = "楽天ポイント GET!";

            rewardPopupRoot.SetActive(true);
            cg.alpha = 0f;
            var rt = rewardPopupRoot.transform as RectTransform;
            if (rt) rt.localScale = Vector3.one * 0.6f;

            if (rewardSfx) rewardSfx.Play();
            if (confettiLeft) confettiLeft.Play();
            if (confettiRight) confettiRight.Play();

            // フェードイン＋ポップ
            float t = 0f;
            while (t < rewardShowTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / rewardShowTime);
                float ease = 1f - Mathf.Pow(1f - u, 3f);
                cg.alpha = ease;
                if (rt) rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.10f, ease);
                yield return null;
            }
            if (rt) rt.localScale = Vector3.one * 1.10f;

            // 少し戻す
            t = 0f;
            const float settleTime = 0.12f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / settleTime);
                if (rt) rt.localScale = Vector3.one * Mathf.Lerp(1.10f, 1.0f, u);
                yield return null;
            }

            yield return new WaitForSeconds(rewardHoldTime);

            // フェードアウト
            t = 0f;
            while (t < rewardHideTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / rewardHideTime);
                cg.alpha = 1f - u;
                if (rt) rt.localScale = Vector3.one * Mathf.Lerp(1.0f, 0.95f, u);
                yield return null;
            }

            rewardPopupRoot.SetActive(false);
        }

        private void OnClickRetry()
        {
            if (!string.IsNullOrEmpty(gameplaySceneName))
                SceneManager.LoadScene(gameplaySceneName);
        }

        private void OnClickBackToTitle()
        {
            if (!string.IsNullOrEmpty(titleSceneName))
                SceneManager.LoadScene(titleSceneName);
        }

        private void RenderSelectedItems()
        {
            if (selectedItemsScroll == null || rowPrefab == null) return;
            var content = selectedItemsScroll.content;
            if (content == null) return;

            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            var items = ResultPayload.DisplayItems;
            if (items == null || items.Count == 0)
            {
                if (emptyPlaceholder) emptyPlaceholder.SetActive(true);
                if (totalPriceText) totalPriceText.text = "合計: 0円";
                return;
            }
            if (emptyPlaceholder) emptyPlaceholder.SetActive(false);

            long total = 0;
            foreach (var it in items)
            {
                total += it.price;
                var rowGO = Instantiate(rowPrefab.gameObject, content, false);
                rowGO.SetActive(true);

                var row = rowGO.GetComponent<SelectedItemRow>();
                if (row != null)
                {
                    row.Bind(it);
                }
                else
                {
                    var nameTMP = rowGO.transform.Find("ProductNameText")?.GetComponent<TMP_Text>();
                    var priceTMP = rowGO.transform.Find("PriceText")?.GetComponent<TMP_Text>();
                    if (nameTMP) nameTMP.text = it.name ?? "";
                    if (priceTMP) priceTMP.text = $"¥{Mathf.Max(0, it.price):N0}";
                    var rawImg = rowGO.transform.Find("ProductImage")?.GetComponent<RawImage>();
                    if (!string.IsNullOrEmpty(it.imageUrl))
                    {
                        StartCoroutine(LoadAndSetRawImage(rawImg, it.imageUrl));
                    }
                    else
                    {
                        if (rawImg) { rawImg.texture = null; rawImg.gameObject.SetActive(false); }
                    }
                }
            }

            if (totalPriceText)
                totalPriceText.text = $"合計: {total:#,0}円";

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private IEnumerator LoadAndSetRawImage(RawImage rawImg, string url)
        {
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Result] Image load failed: {url} - {req.error}");
                    if (rawImg) rawImg.gameObject.SetActive(false);
                    yield break;
                }

                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                if (rawImg)
                {
                    rawImg.texture = tex;
                    rawImg.enabled = true;
                    rawImg.gameObject.SetActive(true);

                    var fitter = rawImg.GetComponent<AspectRatioFitter>();
                    if (!fitter)
                    {
                        fitter = rawImg.gameObject.AddComponent<AspectRatioFitter>();
                        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    }
                    fitter.aspectRatio = (tex && tex.height > 0) ? (float)tex.width / tex.height : 1f;
                }
            }
        }

        private void ShowLoading(string message = "災害発生中…")
        {
            if (loadingOverlay) loadingOverlay.SetActive(true);
            if (loadingText) loadingText.text = message;
        }

        private void HideLoading()
        {
            if (loadingOverlay) loadingOverlay.SetActive(false);
        }

        /// <summary>
        /// ★ AI評価を1回だけ実行して GameSessionResult に反映
        ///   - StartCoroutine(eval) は使わず、eval をそのまま yield return
        ///   - 二重起動ガード（evalInFlight）で多重呼び出しを抑止
        /// </summary>
        private IEnumerator GetAndShowFeedbackFromPayload()
        {
            // 送信アイテム作成
            List<Feedback.ItemEntry> items = ResultPayload.FeedbackItems;
            if (items == null || items.Count == 0)
            {
                if (ResultPayload.DisplayItems != null)
                {
                    items = new List<Feedback.ItemEntry>();
                    foreach (var it in ResultPayload.DisplayItems)
                        items.Add(new Feedback.ItemEntry(it.category ?? "", it.name, 1, new System.Collections.Generic.Dictionary<string, float>()));
                }
            }
            if (items == null || items.Count == 0)
            {
                Debug.LogWarning("[Result] 送信できるアイテムがありません。");
                yield break;
            }

            // フライト中は弾く（必要に応じて待たせても良い）
            if (evalInFlight)
            {
                Debug.LogWarning("[Result] Evaluate skipped (already in flight)");
                yield break;
            }
            evalInFlight = true;

            try
            {
                // Feedback インスタンス確保
                Feedback fb = FindObjectOfType<Feedback>();
                if (fb == null)
                {
                    var go = new GameObject("FeedbackRuntime");
                    fb = go.AddComponent<Feedback>();
                    DontDestroyOnLoad(go);
                }

                // ★重要：入れ子 StartCoroutine を使わず、そのまま yield return
                var eval = fb.EvaluateWithGeminiCoroutine(items, 1, 0);
                yield return eval;

                var ai = eval.Current as Feedback.AIFeedbackResponse;
                if (ai == null || ai.calc == null)
                {
                    Debug.LogError("[Result] AIフィードバックの取得または中間データの解析に失敗しました。");
                    if (feedbackText) feedbackText.text = "フィードバックの取得に失敗しました。";
                    GameSessionResult.Clear();
                    yield break;
                }

                // スコア計算
                var calc = ai.calc;
                double baseSurvivalDays = System.Math.Min(calc.waterPlusNoWaterDays, calc.foodPlusNoFoodDays);
                double finalSurvivalDays = baseSurvivalDays;
                string penaltyReason = "";

                if (!calc.hasBackpack) finalSurvivalDays -= 3.0;
                if (!calc.hasWarmthItem) { finalSurvivalDays -= 1.0; penaltyReason += "防寒具なし(-1日)。"; }
                if (!calc.hasRainproofItem) { finalSurvivalDays -= 1.0; penaltyReason += "雨具なし(-1日)。"; }

                finalSurvivalDays = System.Math.Max(0.0, finalSurvivalDays);
                int daysRounded = (int)System.Math.Round(finalSurvivalDays);

                // テキスト構築
                if (feedbackText)
                {
                    var sb = new StringBuilder();
                    if (!string.IsNullOrEmpty(ai.reasonBrief)) sb.AppendLine(ai.reasonBrief);
                    if (!string.IsNullOrEmpty(penaltyReason)) sb.AppendLine(penaltyReason.Trim());
                    sb.AppendLine($"\n最終的な生存日数は **{daysRounded}日** となります。");

                    if (ai.advice != null && ai.advice.Count > 0)
                    {
                        sb.AppendLine("\nアドバイス:");
                        foreach (var a in ai.advice) sb.AppendLine("・" + a);
                    }

                    if (ai.suggestedNextItems != null && ai.suggestedNextItems.Count > 0)
                    {
                        sb.AppendLine("\n次に買うべき候補:");
                        suggestedItemsUI.Render(ai.suggestedNextItems);
                        foreach (var s in ai.suggestedNextItems)
                            sb.AppendLine($" - [{s.category}] {s.name} x{s.quantity}");
                    }

                    if (!string.IsNullOrEmpty(ai.disclaimer))
                        sb.AppendLine("\n" + ai.disclaimer);

                    feedbackText.text = sb.ToString().Trim();
                }

                GameSessionResult.Set(daysRounded, feedbackText ? feedbackText.text : "");
            }
            finally
            {
                evalInFlight = false;
            }
        }
    }

    public static class GameSessionResult
    {
        public static int DaysSurvived { get; private set; }
        public static string Feedback { get; private set; }
        public static bool HasData { get; private set; }

        public static void Set(int daysSurvived, string feedback)
        {
            DaysSurvived = Mathf.Max(0, daysSurvived);
            Feedback = feedback ?? "";
            HasData = true;
        }

        public static void Clear()
        {
            DaysSurvived = 0;
            Feedback = "";
            HasData = false;
        }
    }
}
