using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pause : MonoBehaviour
{
    // Start is called before the first frame update
    bool paused = false;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Finish and save any active recording before leaving the game.
        SessionReplay replay = FindObjectOfType<SessionReplay>();
        if (replay != null) replay.Stop();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void pausegame()
    {
        if (paused)
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            paused = false;
        }
        else
        {
            Time.timeScale = 0;
            AudioListener.pause = true;
            paused = true;
        }
    }
}
