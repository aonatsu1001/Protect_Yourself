using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ResultSceneRoader : MonoBehaviour
{
    [SerializeField] private ItemDataManager dataManager;

    // ボタンから呼び出すためのpublicな関数を作成
    public void LoadResultScene()
    {
        if (dataManager == null)
        {
            Debug.LogError("ItemDataManager が見つかりません。Inspectorで参照を割り当ててください。");
            return;
        }

        // いま選択されているアイテムでfinalDataを作る
        List<GameItem> finalData = dataManager.GetSelectedItemsList();

        // 受け渡し箱に保存
        ResultPayload.SetFromSelected(finalData);

        // "ItemSelectScene"という名前のシーンを読み込む
        SceneManager.LoadScene("ResultScene");
    }
}