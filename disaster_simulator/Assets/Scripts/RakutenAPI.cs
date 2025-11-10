using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class RakutenAPI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject itemTemplatePrefab;
    public Transform contentParent;
    public Button feedbackButton;
    public TextMeshProUGUI budgetText;
    public TextMeshProUGUI warningText;
    public GameObject dimOverlay;

    [Header("Manager References")]
    public ItemDataManager itemDataManager;
    public Feedback feedbackGenerator;

    private string applicationId;
    private Coroutine warningCoroutine;
    private Color defaultBudgetTextColor;

    [Header("Rakuten API Parameters")]
    [Tooltip("取得件数（1カテゴリあたり）")]
    [Range(1, 10)] public int hitsPerCategory = 8;

    private Dictionary<string, (string keyword, Dictionary<string, float> parameters)> searchCategories;

    const string BASE = "https://app.rakuten.co.jp/services/api/IchibaItem/Search/20220601";

    [System.Serializable]
    private class Secrets { public string applicationId; public string geminiApiKey; }

    void Awake()
    {
        searchCategories = new Dictionary<string, (string, Dictionary<string, float>)>
        {
            { "水分", ("保存水 2L 12本", new Dictionary<string, float> {
                { "water", 24f },   // 2L x 12本
                { "weight", 24f },  // 重さも24kg
                { "food", 0f }, { "light", 0f }, { "capacity", 0f }, { "warmth", 0f }, { "rainproof", 0f }
            })},
            { "食料", ("非常食 5日分", new Dictionary<string, float> {
                { "food", 15f },    // 3食 x 5日
                { "weight", 5f },   // 重さ5kgと仮定
                { "water", 0f }, { "light", 0f }, { "capacity", 0f }, { "warmth", 0f }, { "rainproof", 0f }
            })},
            { "寝具", ("寝袋 シュラフ", new Dictionary<string, float> {
                { "warmth", 10f },  // 防寒値
                { "weight", 2f },
                { "water", 0f }, { "food", 0f }, { "light", 0f }, { "capacity", 0f }, { "rainproof", 0f }
            })},
            { "雨具", ("レインコート", new Dictionary<string, float> {
                { "rainproof", 10f},// 防水値
                { "weight", 1f },
                { "water", 0f }, { "food", 0f }, { "light", 0f }, { "capacity", 0f }, { "warmth", 0f }
            })},
            { "衛生", ("簡易トイレ", new Dictionary<string, float> {
                { "weight", 3f },
                { "water", 0f }, { "food", 0f }, { "light", 0f }, { "capacity", 0f }, { "warmth", 0f }, { "rainproof", 0f }
            })},
            { "情報", ("ラジオ 手回し", new Dictionary<string, float> {
                { "light", 10f },
                { "weight", 1f },
                { "water", 0f }, { "food", 0f }, { "capacity", 0f }, { "warmth", 0f }, { "rainproof", 0f }
            })},
            { "リュック", ("防災 リュック", new Dictionary<string, float> {
                { "capacity", 40f }, // 容量(L)
                { "weight", 2f },   // 重さ(kg)
                { "water", 0f }, { "food", 0f }, { "light", 0f }, { "warmth", 0f }, { "rainproof", 1f }
            })}
        };

        string path = Path.Combine(Application.dataPath, "secrets.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Secrets secrets = JsonUtility.FromJson<Secrets>(json);
            applicationId = secrets.applicationId;
        }
        else { Debug.LogError("secrets.jsonが見つかりません！"); }
    }

    void Start()
    {
        if (itemDataManager == null || feedbackGenerator == null)
        {
            Debug.LogError("ItemDataManagerまたはFeedbackスクリプトが設定されていません！");
            return;
        }

        if (warningText != null) warningText.gameObject.SetActive(false);
        if (dimOverlay != null) dimOverlay.SetActive(false);

        if (budgetText != null)
        {
            defaultBudgetTextColor = budgetText.color;
        }

        itemDataManager.OnBudgetChanged += UpdateBudgetText;
        UpdateBudgetText(itemDataManager.startingBudget);

        if (feedbackButton != null) feedbackButton.onClick.AddListener(OnFeedbackButtonClick);
        if (!string.IsNullOrEmpty(applicationId)) StartCoroutine(FetchAllCategories());
    }

    void OnDestroy()
    {
        if (itemDataManager != null) itemDataManager.OnBudgetChanged -= UpdateBudgetText;
    }

    private void UpdateBudgetText(int newBudget)
    {
        if (budgetText != null)
        {
            budgetText.text = $"💰 {newBudget}";
            budgetText.color = (newBudget < 0) ? Color.red : defaultBudgetTextColor;
        }
    }

    IEnumerator FetchAllCategories()
    {
        foreach (var category in searchCategories)
        {
            Debug.Log($"<color=cyan>--- カテゴリ「{category.Key}」の取得を開始 ---</color>");
            yield return StartCoroutine(FetchItemsForCategory(category.Key, category.Value.keyword, category.Value.parameters));
            yield return new WaitForSeconds(1.0f);
        }
        yield break;
    }

    IEnumerator FetchItemsForCategory(string categoryName, string keyword, Dictionary<string, float> fixedParams)
    {
        var url = $"{BASE}?format=json&applicationId={applicationId}&keyword={UnityWebRequest.EscapeURL(keyword)}&hits={hitsPerCategory}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[RakutenAPI] Category '{categoryName}' Error: {req.error}");
                yield break;
            }
            var json = req.downloadHandler.text;
            var data = JsonUtility.FromJson<Root>(json);
            int cnt = (data?.Items != null) ? data.Items.Length : 0;
            Debug.Log($"カテゴリ「{categoryName}」の結果: {cnt} 件の商品が見つかりました。");
            if (cnt > 0)
            {
                for (int i = 0; i < cnt; i++)
                {
                    var apiItem = data.Items[i].Item;
                    GameItem gameItem = itemDataManager.CreateGameItemFromApi(apiItem, categoryName, fixedParams);
                    GameObject newItemUI = Instantiate(itemTemplatePrefab, contentParent);
                    newItemUI.transform.Find("ProductNameText").GetComponent<TextMeshProUGUI>().text = gameItem.name;
                    newItemUI.transform.Find("PriceText").GetComponent<TextMeshProUGUI>().text = $"価格: {gameItem.price}円";
                    RawImage productImage = newItemUI.GetComponentInChildren<RawImage>();
                    if (apiItem.mediumImageUrls != null && apiItem.mediumImageUrls.Length > 0)
                    {
                        StartCoroutine(SetImageFromUrl(apiItem.mediumImageUrls[0].imageUrl, productImage));
                    }
                    Toggle toggle = newItemUI.GetComponentInChildren<Toggle>();
                    if (toggle != null)
                    {
                        toggle.onValueChanged.AddListener((isOn) =>
                        {
                            if (isOn)
                            {
                                bool success = itemDataManager.TrySelectItem(gameItem.itemId);
                                if (!success)
                                {
                                    toggle.isOn = false;
                                    ShowWarning("残金が足りません");
                                }
                            }
                            else
                            {
                                itemDataManager.DeselectItem(gameItem.itemId);
                            }
                        });
                    }
                    newItemUI.SetActive(true);
                }
            }
        }
    }

    private void ShowWarning(string message)
    {
        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
        }
        warningCoroutine = StartCoroutine(ShowWarningCoroutine(message));
    }
    private IEnumerator ShowWarningCoroutine(string message)
    {
        if (dimOverlay != null) dimOverlay.SetActive(true);
        if (warningText != null)
        {
            warningText.text = message;
            warningText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.0f);
            warningText.gameObject.SetActive(false);
        }
        if (dimOverlay != null) dimOverlay.SetActive(false);
    }

    private void OnFeedbackButtonClick()
    {
        // DataTransfer.cs を経由してResultSceneへデータを渡す場合
        // DataTransfer.SelectedItems = itemDataManager.GetSelectedItemsList();
        // UnityEngine.SceneManagement.SceneManager.LoadScene("ResultScene");

        // 現在のシーンで直接AIを呼び出す場合
        List<GameItem> selectedItems = itemDataManager.GetSelectedItemsList();
        List<Feedback.ItemEntry> feedbackItems = new List<Feedback.ItemEntry>();
        foreach (var item in selectedItems)
        {
            feedbackItems.Add(new Feedback.ItemEntry(item.category, item.name, 1, item.parameters));
        }
        Debug.Log("AIへのフィードバックを開始します。");
        StartCoroutine(feedbackGenerator.EvaluateWithGeminiCoroutine(feedbackItems, 1, 0));
    }

    IEnumerator SetImageFromUrl(string url, RawImage targetImage)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                targetImage.texture = DownloadHandlerTexture.GetContent(req);
            }
        }
        yield break;
    }

    [System.Serializable] public class Root { public ItemEntry[] Items; }
    [System.Serializable] public class ItemEntry { public Item Item; }
    [System.Serializable]
    public class Item
    {
        public string itemName;
        public int itemPrice;
        public string itemUrl;
        public string itemCaption;
        public MediumImageUrl[] mediumImageUrls;
    }
    [System.Serializable] public class MediumImageUrl { public string imageUrl; }
}