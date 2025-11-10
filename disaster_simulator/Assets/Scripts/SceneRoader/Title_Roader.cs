using UnityEngine;
using UnityEngine.SceneManagement; // SceneManagementを使うために必要

public class Title_Roader : MonoBehaviour
{
    // ボタンから呼び出すためのpublicな関数を作成
    public void LoadTitleScene()
    {
        // "Title"という名前のシーンを読み込む
        SceneManager.LoadScene("Title");
    }
}