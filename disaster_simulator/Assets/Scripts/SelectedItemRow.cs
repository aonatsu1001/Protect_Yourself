// Assets/Scripts/SelectedItemRow.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class SelectedItemRow : MonoBehaviour
{
    [Header("Refs (assign in prefab)")]
    [SerializeField] private RawImage productImage;       // ProductImage
    [SerializeField] private TMP_Text productNameText;    // ProductNameText
    [SerializeField] private TMP_Text priceText;          // PriceText
    [SerializeField] private Button     linkButton;

    [Header("Options")]
    [SerializeField] private bool hideImageIfEmpty = true;

    private string linkUrl;


    private void AutoWire()
    {
        // 子オブジェクトの名前で自動取得（未割り当て時のみ）
        if (!productNameText)
            productNameText = transform.Find("ProductNameText")?.GetComponent<TMP_Text>();
        if (!priceText)
            priceText = transform.Find("PriceText")?.GetComponent<TMP_Text>();

        if (!productImage)
        {
            productImage = transform.Find("ProductImage")?.GetComponent<RawImage>();
        }
        if (!linkButton)
        {
            linkButton = transform.Find("LinkButton")?.GetComponent<Button>();
        }
    }

    private void Awake()
    {
        AutoWire();
        if (linkButton)
        {
            linkButton.onClick.RemoveAllListeners();
            linkButton.onClick.AddListener(OnClickOpenLink);
        }
    }

    private void OnValidate()
    {
        // エディタ上でプレハブを開いたときにも自動で紐付け
        AutoWire();
    }

    private void OnClickOpenLink()
    {
        Debug.Log($"[SelectedItemRow] Click! url={(string.IsNullOrEmpty(linkUrl) ? "(empty)" : linkUrl)}", this);
        if (string.IsNullOrEmpty(linkUrl)) return;
        Application.OpenURL(linkUrl);
    }

    // ====== Public API ======

    /// <summary>名前と価格のみを設定</summary>
    public void Bind(string name, int price)
    {
        if (productNameText) productNameText.text = name ?? "";
        if (priceText) priceText.text = $"¥{Mathf.Max(0, price):N0}";
    }

    /// <summary>画像テクスチャを直接セット</summary>
    public void SetImage(Texture tex)
    {
        if (!productImage) return;
        productImage.texture = tex;
        if (hideImageIfEmpty) productImage.gameObject.SetActive(tex != null);
    }

    /// <summary>クリック時に開くリンクURLを設定</summary>
    public void SetLinkUrl(string url)
    {
        linkUrl = url;
    }

    /// <summary>URLから画像をロードして表示（簡易版）</summary>
    public void SetImageFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || !productImage)
        {
            if (productImage && hideImageIfEmpty) productImage.gameObject.SetActive(false);
            return;
        }
        StartCoroutine(LoadImageCoroutine(url));
    }

    /// <summary>ResultPayload の表示用DTOからまとめて反映</summary>
    public void Bind(ResultPayload.ResultViewItem it)
    {
        if (it == null)
        {
            Bind("", 0);
            SetImage(null);
            SetLinkUrl("");
            return;
        }
        Bind(it.name, it.price);
        if (!string.IsNullOrEmpty(it.imageUrl)) SetImageFromUrl(it.imageUrl);
        else SetImage(null);
        SetLinkUrl(it.productUrl);
    }

    // ====== Private ======
    private System.Collections.IEnumerator LoadImageCoroutine(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SelectedItemRow] Image load failed: {url} - {req.error}");
                SetImage(null);
                yield break;
            }
            var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            SetImage(tex);
        }
    }
}
