// Assets/Scripts/ResultPayload.cs
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class ResultPayload
{
    // 結果画面で並べるための軽量DTO
    public class ResultViewItem
    {
        public string name;
        public int price;
        public string imageUrl; // 無ければ null でOK
        public string productUrl;
        public string category; // 必要なら
    }

    public static List<ResultViewItem> DisplayItems { get; private set; } = new();
    public static List<Feedback.ItemEntry> FeedbackItems { get; private set; } = new();

    /// <summary>
    /// 選択された GameItem から結果画面用の表示データとAI送信用データを作成
    /// GameItem に imageUrl が無くてもOK（持っていれば反映）
    /// </summary>
    public static void SetFromSelected(IEnumerable<GameItem> selectedItems)
    {
        if (selectedItems == null)
        {
            DisplayItems = new();
            FeedbackItems = new();
            return;
        }

        // 画像URLを持っていれば拾う（field/property どちらでも）
        string GetImageUrl(GameItem it)
        {
            var t = it.GetType();
            var f = t.GetField("imageUrl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f.GetValue(it) as string;
            var p = t.GetProperty("imageUrl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p.GetValue(it) as string;
            return null;
        }

        DisplayItems = selectedItems
            .Select(it => new ResultViewItem
            {
                name = it.name,
                price = it.price,
                category = it.category,
                imageUrl = GetImageUrl(it), // 無ければ null
                productUrl = it.productUrl
            })
            .ToList();

        // ▼▼▼ 修正点 ▼▼▼
        FeedbackItems = selectedItems
            .Select(it => new Feedback.ItemEntry(it.category, it.name, 1, it.parameters)) // 4番目の引数にit.parametersを追加
            .ToList();
    }

    public static List<ResultViewItem> ConsumeDisplay()
    {
        var copy = DisplayItems;
        DisplayItems = new();
        return copy;
    }

    public static List<Feedback.ItemEntry> ConsumeFeedback()
    {
        var copy = FeedbackItems;
        FeedbackItems = new();
        return copy;
    }

    public static void Clear()
    {
        DisplayItems.Clear();
        FeedbackItems.Clear();
    }
}