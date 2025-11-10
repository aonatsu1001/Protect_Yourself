using UnityEngine;
using UnityEngine.SceneManagement; // SceneManagementを使うために必要

public class Howtoplay_Roader : MonoBehaviour
{
    // ボタンから呼び出すためのpublicな関数を作成
    public void LoadHowtoplayScene()
    {
        // "Howtoplay"という名前のシーンを読み込む
        SceneManager.LoadScene("Howtoplay");
    }
}