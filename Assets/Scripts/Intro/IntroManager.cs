using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class IntroManager : MonoBehaviour
{
    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private string nextSceneName = "MainGame";

    private bool isTransitioning = false;

    private void Start()
    {
        if (playableDirector != null)
        {
            playableDirector.stopped += OnTimelineEnded;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
        {
            LoadNextScene();
        }
    }

    private void OnTimelineEnded(PlayableDirector director)
    {
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void OnDestroy()
    {
        if (playableDirector != null)
        {
            playableDirector.stopped -= OnTimelineEnded;
        }
    }
}
