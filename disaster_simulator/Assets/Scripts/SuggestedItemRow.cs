using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class SuggestedItemRow : MonoBehaviour
{
    [Header("Refs (assign in prefab)")]
    [SerializeField] private TMP_Text productNameText;    // ProductNameText
    [SerializeField] private Button     linkButton;

    private string linkUrl;


    private void AutoWire()
    {
        // 子オブジェクトの名前で自動取得（未割り当て時のみ）
        if (!productNameText)
            productNameText = transform.Find("ProductNameText")?.GetComponent<TMP_Text>();

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

    /// <summary>名前のみを設定</summary>
    public void Bind(string name)
    {
        if (productNameText) productNameText.text = name ?? "";
    }

    /// <summary>クリック時に開くリンクURLを設定</summary>
    public void SetLinkUrl(string url)
    {
        linkUrl = url;
    }

    /// <summary>ResultPayload の表示用DTOからまとめて反映</summary>
    public void Bind(ResultPayload.ResultViewItem it)
    {
        if (it == null)
        {
            Bind("");
            SetLinkUrl("");
            return;
        }
        Bind(it.name);
        SetLinkUrl(it.productUrl);
    }
}
