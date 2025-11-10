using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using Newtonsoft.Json;

public class Feedback : MonoBehaviour
{
    private string geminiApiKey;

    [Header("Gemini Settings")]
    [SerializeField] private string model = "gemini-1.5-flash";

    [Serializable]
    private class Secrets
    {
        public string applicationId;
        public string geminiApiKey;
    }

    void Awake()
    {
        string path = Path.Combine(Application.dataPath, "secrets.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Secrets secrets = JsonUtility.FromJson<Secrets>(json);
            geminiApiKey = secrets.geminiApiKey;
        }
        else
        {
            Debug.LogError("secrets.jsonが見つかりません！ Assetsフォルダに作成してください。");
        }
    }

    [Serializable]
    public class ItemEntry
    {
        public string category;
        public string name;
        public int quantity;
        public Dictionary<string, float> parameters;

        public ItemEntry(string category, string name, int quantity, Dictionary<string, float> parameters)
        {
            this.category = category;
            this.name = name;
            this.quantity = quantity;
            this.parameters = parameters;
        }
        public override string ToString() => $"[{category}] {name} x {quantity}";
    }

    [Serializable]
    public class UserPayload
    {
        public int personCount;
        public int budget;
        public List<ItemEntry> items;
    }

    [Serializable]
    public class Calc
    {
        public double totalWeight;
        public double totalCapacity;
        public double carryableWaterL;
        public double carryableMeals;
        public double waterPlusNoWaterDays;
        public double foodPlusNoFoodDays;
        public bool hasWarmthItem;
        public bool hasRainproofItem;
        public bool hasBackpack; // ← 修正点：この行を追加
    }

    [Serializable]
    public class AIFeedbackResponse
    {
        public Calc calc;
        public string reasonBrief;
        public List<string> advice;
        public List<string> missingCategories;
        public List<ItemEntry> suggestedNextItems;
        public string disclaimer;
    }

    public IEnumerator EvaluateWithGeminiCoroutine(List<ItemEntry> items, int personCount, int budgetYen)
    {
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            Debug.LogError("Gemini APIキーが未設定です。secrets.jsonを確認してください。");
            yield return null;
            yield break;
        }

        var payload = new UserPayload
        {
            personCount = personCount,
            budget = budgetYen,
            items = new List<ItemEntry>(items)
        };
        string userJson = JsonConvert.SerializeObject(payload, Formatting.Indented);
        Debug.Log("[Gemini Request userJson] " + userJson);

        string instruction =
@"あなたは災害備蓄アドバイザーです。
ユーザーが選んだアイテムリスト（JSON形式）を解析し、以下の厳格なルールに基づいて**中間データ**と**計算過程の要約**を算出し、**JSONのみ** を返してください。

【ルール】
1. **キャパシティ（容量）の計算:**
   - 最初に全アイテムの ""capacity"" パラメータを合計し ""totalCapacity"" を算出します。
   - **もし ""totalCapacity"" が0の場合、素手で運搬できると仮定し、""totalCapacity"" を10 (kg)として計算を進めます。**
   - 次に全アイテムの ""weight"" パラメータを合計し ""totalWeight"" を算出します。
   - もし **totalWeight > totalCapacity** の場合、""water"" と ""food"" を優先して運搬すると仮定し、運搬可能な水量と食料数を ""carryableWaterL"", ""carryableMeals"" として算出します。

2. **水・食料の基本日数計算:**
   - 運搬可能な水・食料を元に ""waterDays"", ""mealDays"" を計算し、最低生存期間（水+3日, 食料+7日）を加算した ""waterPlusNoWaterDays"", ""foodPlusNoFoodDays"" を算出してください。

3. **環境アイテムの有無:**
   - ""warmth"" > 0 のアイテムがあれば ""hasWarmthItem"" を true に。
   - ""rainproof"" > 0 のアイテムがあれば ""hasRainproofItem"" を true に。
   - ""capacity"" > 0 のアイテムが一つでもあれば ""hasBackpack"" を true に。

**最終的な生存日数(days)はUnity側で計算するため、daysフィールドは不要です。**

【要約（reasonBrief）の厳格なルール】
- 以下の2ステップの文章を**必ず生成**し、改行して繋げてください。
- **ステップ1:** 「まず、水からは合計XX日、食料からは合計YY日分の備えがあり、AA日生存可能です。」という文章を生成します。XXには'waterPlusNoWaterDays-3'、YYには'foodPlusNoFoodDays-7'をそれぞれ入れてください。またAAには`min(waterPlusNoWaterDays, foodPlusNoFoodDays)`の値を小数点を切り捨てて整数で入れてください。
- **ステップ2:** リュックを一つでも選択している場合はステップ2を飛ばしてください。リュックを一つも選択していない場合、「次にBBであることから生存日数はZZ日になります。」という文章を生成します。BBには、リュックを選択していないため10kgしか運べなかったという記述を入れてください。ZZには、AAから3日減算した生存日数を入れてください。これまでの生存日数はすべて0日以上の整数としてください。

【出力JSON（厳守）】
{
  ""calc"": {
    ""totalWeight"": number,
    ""totalCapacity"": number,
    ""carryableWaterL"": number,
    ""carryableMeals"": number,
    ""waterPlusNoWaterDays"": number,
    ""foodPlusNoFoodDays"": number,
    ""hasWarmthItem"": boolean,
    ""hasRainproofItem"": boolean,
    ""hasBackpack"": boolean
  },
  ""reasonBrief"": string,
  ""advice"": string[],
  ""missingCategories"": string[],
  ""suggestedNextItems"": [{ ""category"": string, ""name"": string, ""quantity"": number }],
  ""disclaimer"": string
}";

        var req = new GeminiRequest
        {
            contents = new List<Content> {
                new Content {
                    role = "user",
                    parts = new List<Part> {
                        new Part { text = instruction },
                        new Part { text = userJson }
                    }
                }
            },
            generationConfig = new GenerationConfig { temperature = 0.2, maxOutputTokens = 2048 }
        };

        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={geminiApiKey}";
        string body = JsonConvert.SerializeObject(req);

        using (var uwr = new UnityWebRequest(endpoint, "POST"))
        {
            uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");

            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Gemini] HTTP Error {uwr.responseCode}: {uwr.error}\nResponse:\n{uwr.downloadHandler.text}");
                yield return null;
                yield break;
            }

            var outer = JsonConvert.DeserializeObject<GeminiResponse>(uwr.downloadHandler.text);
            var rawBuilder = new StringBuilder();
            if (outer?.candidates != null)
            {
                foreach (var c in outer.candidates)
                {
                    if (c?.content?.parts == null) continue;
                    foreach (var p in c.content.parts)
                    {
                        if (!string.IsNullOrEmpty(p?.text))
                            rawBuilder.AppendLine(p.text.Trim());
                    }
                }
            }
            var innerRaw = rawBuilder.ToString().Trim();

            string jsonCandidate = ExtractFirstJson(innerRaw);
            if (string.IsNullOrEmpty(jsonCandidate))
            {
                Debug.LogError("[Gemini] 応答からJSONを抽出できませんでした。Rawを確認してください:\n" + innerRaw);
                yield return null;
                yield break;
            }

            AIFeedbackResponse aiResponse = null;
            try
            {
                aiResponse = JsonConvert.DeserializeObject<AIFeedbackResponse>(jsonCandidate);
                Debug.Log(FormatResultLog(items, aiResponse));
            }
            catch (Exception ex)
            {
                Debug.LogError("[Gemini] JSONの解析に失敗しました。詳細: " + ex.Message);
                Debug.LogError("[Gemini] 問題のJSON:\n" + jsonCandidate);
            }
            yield return aiResponse;
        }
    }

    private static string ExtractFirstJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string cleanedText = text.Trim();
        if (cleanedText.StartsWith("```"))
        {
            int firstNewline = cleanedText.IndexOf('\n');
            if (firstNewline > -1)
            {
                cleanedText = cleanedText.Substring(firstNewline + 1).Trim();
            }
            if (cleanedText.EndsWith("```"))
            {
                cleanedText = cleanedText.Substring(0, cleanedText.Length - 3).Trim();
            }
        }
        int startIndex = cleanedText.IndexOf('{');
        if (startIndex == -1) return null;
        int endIndex = cleanedText.LastIndexOf('}');
        if (endIndex == -1) return null;
        return cleanedText.Substring(startIndex, endIndex - startIndex + 1);
    }

    // AIからのJsonをパースしたものをリザルトに渡す用にフォーマットする関数
    private static string FormatResultLog(List<ItemEntry> items, AIFeedbackResponse r)
    {
        if (r == null) return "AIからの応答が不正なため、ログを生成できませんでした。";
        var sb = new StringBuilder();
        sb.AppendLine("==== 送信した防災グッズ ====");
        foreach (var it in items) sb.AppendLine($"- {it}");
        sb.AppendLine("\n==== Gemini応答（中間データ） ====");
        if (r.calc != null)
        {
            sb.AppendLine($"[calc] totalWeight={r.calc.totalWeight}, totalCapacity={r.calc.totalCapacity}");
            sb.AppendLine($"[calc] carryableWaterL={r.calc.carryableWaterL}, carryableMeals={r.calc.carryableMeals}");
            sb.AppendLine($"[calc] water+3={r.calc.waterPlusNoWaterDays}, food+7={r.calc.foodPlusNoFoodDays}");
            sb.AppendLine($"[calc] hasWarmthItem={r.calc.hasWarmthItem}, hasRainproofItem={r.calc.hasRainproofItem}, hasBackpack={r.calc.hasBackpack}");
        }
        sb.AppendLine($"要約: {r.reasonBrief}");
        if (r.advice != null && r.advice.Count > 0)
        {
            sb.AppendLine("アドバイス:");
            foreach (var a in r.advice) sb.AppendLine($" - {a}");
        }
        return sb.ToString();
    }

    [Serializable] class GeminiRequest { public List<Content> contents; public GenerationConfig generationConfig; }
    [Serializable] class Content { public string role; public List<Part> parts; }
    [Serializable] class Part { public string text; }
    [Serializable] class GenerationConfig { public double temperature = 0.2; public int maxOutputTokens = 2048; }
    [Serializable] class GeminiResponse { public List<Candidate> candidates; }
    [Serializable] class Candidate { public Content content; }
}