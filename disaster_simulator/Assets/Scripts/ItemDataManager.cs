using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class GameItem
{
    public int itemId;
    public string name;
    public int price;
    public string category; // ← 追加: カテゴリ名を保存する

    public string imageUrl;
    public string productUrl;
    public Dictionary<string, float> parameters = new Dictionary<string, float>();

    // コンストラクタを修正
    public GameItem(int id, string name, int price, string category, string imageUrl, string productUrl, Dictionary<string, float> parameters)
    {
        this.itemId = id;
        this.name = name ?? "";
        this.price = price;
        this.category = category; // ← 追加
        this.imageUrl = imageUrl;
        this.productUrl = productUrl;
        this.parameters = parameters;
    }

    public override string ToString()
    {
        var paramStrings = parameters.Select(kvp => $"{kvp.Key}: {kvp.Value}");
        return $"ID:{itemId} | Category:{category} | {name} | パラメータ: {{{string.Join(", ", paramStrings)}}}";
    }
}

public class ItemDataManager : MonoBehaviour
{
    [Header("Budget Settings")]
    public int startingBudget = 30000;

    public event Action<int> OnBudgetChanged;

    private List<GameItem> allGameItems = new List<GameItem>();
    private List<int> selectedItemIds = new List<int>();
    private int nextItemId = 0;
    private int currentBudget;

    void Awake()
    {
        currentBudget = startingBudget;
    }

    // ▼▼▼ 変更点 ▼▼▼
    // 他のスクリプトから現在の残金を取得できるようにプロパティを追加
    public int CurrentBudget
    {
        get { return currentBudget; }
    }

    public GameItem CreateGameItemFromApi(RakutenAPI.Item apiItem, string category, Dictionary<string, float> fixedParams)
    {
        int itemId = nextItemId++;
        var imgUrl = apiItem.mediumImageUrls != null && apiItem.mediumImageUrls.Length > 0
               ? apiItem.mediumImageUrls[0].imageUrl
               : null;
        // コンストラクタの引数を修正
        var gameItem = new GameItem(itemId, apiItem.itemName, apiItem.itemPrice, category, imgUrl, apiItem.itemUrl, fixedParams);
        allGameItems.Add(gameItem);
        return gameItem;
    }

    // ▼▼▼ 変更点 ▼▼▼
    // void SelectItem から bool TrySelectItem に変更
    public bool TrySelectItem(int itemId)
    {
        GameItem itemToSelect = allGameItems.FirstOrDefault(i => i.itemId == itemId);
        if (itemToSelect == null) return false;

        // 予算が足りるかチェック
        if (itemToSelect.price > currentBudget)
        {
            Debug.LogWarning($"予算不足: 残金 {currentBudget}円, 商品価格 {itemToSelect.price}円");
            return false; // 予算不足ならfalseを返す
        }

        if (!selectedItemIds.Contains(itemId))
        {
            selectedItemIds.Add(itemId);
            RecalculateBudget();
        }
        return true; // 成功したらtrueを返す
    }

    public void DeselectItem(int itemId)
    {
        if (selectedItemIds.Contains(itemId))
        {
            selectedItemIds.Remove(itemId);
            RecalculateBudget();
        }
    }

    private void RecalculateBudget()
    {
        int totalCost = 0;
        foreach (int id in selectedItemIds)
        {
            GameItem item = allGameItems.FirstOrDefault(i => i.itemId == id);
            if (item != null)
            {
                totalCost += item.price;
            }
        }
        currentBudget = startingBudget - totalCost;
        OnBudgetChanged?.Invoke(currentBudget);
    }

    public List<GameItem> GetSelectedItemsList()
    {
        return allGameItems.Where(item => selectedItemIds.Contains(item.itemId)).ToList();
    }
}