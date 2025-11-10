using UnityEngine;
using UnityEngine.SceneManagement; // SceneManagementを使うために必要

public class Signup_Roader : MonoBehaviour
{
    // ボタンから呼び出すためのpublicな関数を作成
    public void LoadSignupScene()
    {
        SceneManager.LoadScene("Signup");
    }
}