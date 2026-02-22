using UnityEngine;
using UnityEngine.SceneManagement;

public class GeneralNav : MonoBehaviour
{
    [CoolHeader("GeneralNav")]
    private string something;
    
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
