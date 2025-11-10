// Assets/Scripts/SuggestedNextItemsUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; // EscapeURL 用

public class SuggestedNextItemsUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform content;                 // ScrollView/Viewport/Content
    [SerializeField] private SuggestedItemRow rowPrefab;         // 既存の SelectedItemRow プレハブ

    [Header("Search Options")]
    [SerializeField] private bool addDisasterTag = true;        // 検索語に「 防災 」を付けるか
    [SerializeField] private string extraQuery = "";            // 追加で付けたい語（任意）

    /// <summary>AIの候補を表示（s.nameを楽天検索リンクに）</summary>
    public void Render(IList<Feedback.ItemEntry> items)
    {
        if (!content || !rowPrefab) { Debug.LogWarning("[SuggestedNextItemsUI] 未割り当て"); return; }

        // 既存クリア
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        if (items == null || items.Count == 0) return;

        foreach (var s in items)
        {
            // ラベル表示例: "[水] ミネラルウォーター 2L x 6"
            string label = $"[{s.category}] {s.name} x {s.quantity}";

            // 検索クエリは s.name をベースに（必要なら「防災」や extraQuery を追加）
            string query = s.name;
            if (addDisasterTag) query += " 防災";
            if (!string.IsNullOrWhiteSpace(extraQuery)) query += " " + extraQuery.Trim();

            string url = BuildRakutenSearchUrl(query);

            // 行生成
            var row = Instantiate(rowPrefab, content, false);
            row.gameObject.SetActive(true);

            row.Bind(label);       // 価格は任意なので 0 に
            row.SetLinkUrl(url);      // ← クリック/ボタンで楽天検索へ
        }

        // レイアウト確定（見た目のズレ防止）
        Canvas.ForceUpdateCanvases();
        var rt = content as RectTransform;
        if (rt) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private static string BuildRakutenSearchUrl(string query)
    {
        // 例: https://search.rakuten.co.jp/search/mall/ミネラルウォーター%202L%206本%20防災/?f=0
        var encoded = UnityWebRequest.EscapeURL(query);
        return $"https://search.rakuten.co.jp/search/mall/{encoded}/?f=0";
    }
}
