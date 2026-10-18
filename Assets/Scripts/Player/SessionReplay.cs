using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class ReplayFrame
{
    public float time;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 cameraPosition;
    public Quaternion cameraRotation;
    public bool walking;
}

[Serializable]
public class ReplayShotData
{
    public float time;
    public Vector3 origin;
    public Vector3 direction;
}

[Serializable]
public class ReplaySession
{
    public bool powerUpsDisabled = true;
    public string scene;
    public float duration;
    public List<ReplayFrame> frames = new List<ReplayFrame>();
    public List<ReplayShotData> shots = new List<ReplayShotData>();
}

public enum ReplayStartupMode { Normal, Record, Playback }

// Start runs before gameplay Start methods; LateUpdate captures the resulting pose.
[DefaultExecutionOrder(-1000)]
public class SessionReplay : MonoBehaviour
{
    [Tooltip("Choose before entering Play Mode. A new session requires restarting the scene.")]
    public ReplayStartupMode startupMode = ReplayStartupMode.Normal;
    [Tooltip("Recording duration in game seconds. Pausing also pauses recording/playback.")]
    public float recordingDuration = 60f;
    [Tooltip("File name inside Application.persistentDataPath/Replays. Recording replaces this file.")]
    public string fileName = "session.json";
    public KeyCode stopKey = KeyCode.F8;

    public bool IsRecording { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool ManagedByBenchmark { get; set; }
    public event Action Stopping;
    public float StartedAt { get { return startedAt; } }
    public float SessionDuration { get; private set; }
    public string Status { get; private set; }
    public float Elapsed { get { return Mathf.Min(Time.time - startedAt, SessionDuration); } }

    public float PrepareBenchmark()
    {
        if (selectedMode == ReplayStartupMode.Record) SessionDuration = recordingDuration;
        else if (selectedMode == ReplayStartupMode.Playback)
        {
            session = JsonUtility.FromJson<ReplaySession>(File.ReadAllText(FilePath));
            Validate(session);
            SessionDuration = session.duration;
        }
        else throw new InvalidOperationException("Select Record or Playback before starting a benchmark.");
        if (!Finite(SessionDuration) || SessionDuration <= 0f)
            throw new InvalidDataException("Session duration must be positive.");
        return SessionDuration;
    }

    public bool BeginBenchmark()
    {
        if (!ManagedByBenchmark || selectedMode == ReplayStartupMode.Normal) return false;
        if (selectedMode == ReplayStartupMode.Record)
        {
            StartRecording();
        }
        else StartPlayback();
        return IsRecording || IsPlaying;
    }
    string playbackPath;
    public string FilePath { get { return selectedMode == ReplayStartupMode.Playback && !string.IsNullOrEmpty(playbackPath)
        ? playbackPath : Path.Combine(Application.persistentDataPath, "Replays", Path.GetFileName(fileName)); } }

    InputHandler input;
    PlayerMovement movement;
    PlayerShooting shooting;
    Rigidbody body;
    Camera replayCamera;
    CameraFollow cameraFollow;
    ReplaySession session;
    float startedAt;
    float durationLimit;
    int frameIndex;
    int shotIndex;
    bool wasKinematic;
    bool cameraFollowEnabled;
    ReplayStartupMode selectedMode;

    void Awake()
    {
        if (BenchmarkLaunch.Applies)
        {
            startupMode = BenchmarkLaunch.NormalGame ? ReplayStartupMode.Normal : ReplayStartupMode.Playback;
            playbackPath = BenchmarkLaunch.NormalGame ? null : BenchmarkLaunch.ReplayPath;
        }
        selectedMode = startupMode;
        if (selectedMode == ReplayStartupMode.Normal) return;

        // Exclude both random spawning and pickups already placed in the scene.
        foreach (PowerUpManager manager in FindObjectsOfType<PowerUpManager>())
            manager.DisableForBenchmark();
        foreach (PowerUp powerUp in FindObjectsOfType<PowerUp>())
            powerUp.gameObject.SetActive(false);
    }

    void Start()
    {
        if (selectedMode == ReplayStartupMode.Normal) return;
        Status = "Preparing";
        gameObject.AddComponent<ReplayStatusUI>().replay = this;
        input = FindObjectOfType<InputHandler>();
        if (input != null)
        {
            movement = input.playerMovement;
            shooting = input.playerShooting;
        }
        replayCamera = Camera.main;
        if (movement == null || shooting == null || replayCamera == null ||
            (body = movement.GetComponent<Rigidbody>()) == null)
        {
            Debug.LogError("SessionReplay requires InputHandler, player Rigidbody and a MainCamera.", this);
            enabled = false;
            return;
        }
        cameraFollow = replayCamera.GetComponent<CameraFollow>();
        shooting.ShotFired += RecordShot;
        if (ManagedByBenchmark) return;
        Debug.Log("Benchmark session: " + selectedMode + ". Power-ups disabled. F8 stops. File: " + FilePath, this);
        if (selectedMode == ReplayStartupMode.Record) StartRecording();
        else StartPlayback();
    }

    void Update()
    {
        if (Input.GetKeyDown(stopKey)) Stop();
    }

    void LateUpdate()
    {
        if (!IsRecording && !IsPlaying) return;
        if (!movement.isActiveAndEnabled || !shooting.isActiveAndEnabled)
        {
            Debug.LogWarning("Replay stopped: player movement or shooting is disabled (for example, player death).", this);
            Stop();
            return;
        }
        if (Time.timeScale == 0f) return;
        float elapsed = Time.time - startedAt;
        if (IsRecording)
        {
            CaptureFrame(Mathf.Min(elapsed, durationLimit));
            if (elapsed >= durationLimit && !ManagedByBenchmark) Stop();
        }
        else
        {
            float time = Mathf.Min(elapsed, session.duration);
            ApplyFrame(time);
            while (shotIndex < session.shots.Count && session.shots[shotIndex].time <= time)
            {
                ReplayShotData shot = session.shots[shotIndex++];
                shooting.ReplayShot(shot.origin, shot.direction);
            }
            if (elapsed >= session.duration && !ManagedByBenchmark) Stop();
        }
    }

    bool CanStart()
    {
        return isActiveAndEnabled && input != null && movement != null && shooting != null &&
            replayCamera != null && movement.isActiveAndEnabled && shooting.isActiveAndEnabled &&
            Time.timeScale > 0f;
    }

    void StartRecording()
    {
        if (!Application.isPlaying || !CanStart() || IsRecording || IsPlaying) return;
        if (!Finite(recordingDuration) || recordingDuration <= 0f)
        {
            Debug.LogError("Recording duration must be a positive finite number.", this);
            return;
        }
        session = new ReplaySession { scene = SceneManager.GetActiveScene().name };
        durationLimit = recordingDuration;
        startedAt = Time.time;
        IsRecording = true;
        SessionDuration = durationLimit;
        Status = "RECORDING";
        CaptureFrame(0f);
        Debug.Log("Recording started for " + durationLimit + " game seconds.", this);
    }

    void CaptureFrame(float time)
    {
        ReplayFrame frame = new ReplayFrame {
            time = time, position = body.position, rotation = body.rotation,
            cameraPosition = replayCamera.transform.position,
            cameraRotation = replayCamera.transform.rotation, walking = movement.IsWalking
        };
        int last = session.frames.Count - 1;
        if (last >= 0 && session.frames[last].time == time) session.frames[last] = frame;
        else session.frames.Add(frame);
    }

    void RecordShot(Vector3 origin, Vector3 direction)
    {
        if (!IsRecording) return;
        float time = Time.time - startedAt;
        if (time > durationLimit) return;
        session.shots.Add(new ReplayShotData { time = time, origin = origin, direction = direction });
    }

    void StartPlayback()
    {
        if (!Application.isPlaying || !CanStart() || IsRecording || IsPlaying) return;
        try
        {
            ReplaySession loaded = ManagedByBenchmark ? session : JsonUtility.FromJson<ReplaySession>(File.ReadAllText(FilePath));
            Validate(loaded);
            session = loaded;
            SessionDuration = session.duration;
        }
        catch (Exception exception)
        {
            Status = "Load failed - see Console";
            Debug.LogError("Could not load replay: " + exception.Message, this);
            return;
        }
        input.ClearHistory();
        input.ReplayControlsInput = true;
        movement.ReplayControlsInput = true;
        wasKinematic = body.isKinematic;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        cameraFollowEnabled = cameraFollow != null && cameraFollow.enabled;
        if (cameraFollow != null) cameraFollow.enabled = false;
        frameIndex = shotIndex = 0;
        startedAt = Time.time;
        IsPlaying = true;
        Status = "PLAYBACK";
        ApplyFrame(0f);
        Debug.Log("Replay started with the fresh scene. Enemy simulation is not deterministic.", this);
    }

    void ApplyFrame(float time)
    {
        while (frameIndex + 1 < session.frames.Count && session.frames[frameIndex + 1].time <= time)
            frameIndex++;
        ReplayFrame a = session.frames[frameIndex];
        ReplayFrame b = session.frames[Mathf.Min(frameIndex + 1, session.frames.Count - 1)];
        float blend = b.time > a.time ? Mathf.Clamp01((time - a.time) / (b.time - a.time)) : 0f;
        body.position = Vector3.Lerp(a.position, b.position, blend);
        body.rotation = Quaternion.Slerp(a.rotation, b.rotation, blend);
        replayCamera.transform.position = Vector3.Lerp(a.cameraPosition, b.cameraPosition, blend);
        replayCamera.transform.rotation = Quaternion.Slerp(a.cameraRotation, b.cameraRotation, blend);
        movement.Animating(a.walking ? 1f : 0f, 0f);
    }

    [ContextMenu("Stop and Save Recording / Stop Playback")]
    public void Stop()
    {
        if ((IsRecording || IsPlaying) && Stopping != null) Stopping();
        if (IsRecording || IsPlaying)
            Status = Elapsed >= SessionDuration ? "Completed" : "Stopped";
        if (IsRecording)
        {
            CaptureFrame(Mathf.Min(Time.time - startedAt, durationLimit));
            session.duration = session.frames[session.frames.Count - 1].time;
            IsRecording = false;
            try
            {
                if (string.IsNullOrEmpty(Path.GetFileName(fileName))) throw new IOException("Specify a file name.");
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(session));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
                Debug.Log("Saved replay (" + session.duration + " s, " + session.shots.Count + " shots): " + FilePath, this);
            }
            catch (Exception exception) { Status = "Save failed - see Console"; Debug.LogError("Could not save replay: " + exception.Message, this); }
        }
        if (!IsPlaying) return;
        IsPlaying = false;
        if (input != null) input.ReplayControlsInput = false;
        if (movement != null)
        {
            movement.ReplayControlsInput = false;
            movement.Animating(0f, 0f);
        }
        if (body != null) body.isKinematic = wasKinematic;
        if (cameraFollow != null) cameraFollow.enabled = cameraFollowEnabled;
        if (shooting != null) shooting.DisableEffects();
        Debug.Log("Replay stopped; manual controls restored.", this);
    }

    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    static bool ValidRotation(Quaternion value)
    {
        float length = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
        return Finite(length) && Mathf.Abs(length - 1f) < 0.01f;
    }

    static void Validate(ReplaySession data)
    {
        ValidateForScene(data, SceneManager.GetActiveScene().name);
    }

    public static void ValidateForScene(ReplaySession data, string sceneName)
    {
        if (data == null || !data.powerUpsDisabled || !CompatibleReplayScene(data.scene, sceneName) ||
            !Finite(data.duration) || data.duration < 0f || data.frames == null || data.frames.Count == 0 || data.shots == null)
            throw new InvalidDataException("Invalid replay data: check scene, duration, frames and disabled power-ups.");
        float previous = -1f;
        foreach (ReplayFrame frame in data.frames)
        {
            if (frame == null || !Finite(frame.time) || frame.time < 0f || frame.time <= previous || frame.time > data.duration ||
                !Finite(frame.position) || !Finite(frame.cameraPosition) || !ValidRotation(frame.rotation) || !ValidRotation(frame.cameraRotation))
                throw new InvalidDataException("Invalid replay frame.");
            previous = frame.time;
        }
        if (data.frames[0].time != 0f || previous != data.duration) throw new InvalidDataException("Incomplete replay timeline.");
        previous = -1f;
        foreach (ReplayShotData shot in data.shots)
        {
            if (shot == null || !Finite(shot.time) || shot.time < 0f || shot.time < previous || shot.time > data.duration ||
                !Finite(shot.origin) || !Finite(shot.direction) || Mathf.Abs(shot.direction.sqrMagnitude - 1f) > 0.01f)
                throw new InvalidDataException("Invalid replay shot.");
            previous = shot.time;
        }
    }

    // These scenes share gameplay geometry. Keep this allowlist narrow: unrelated
    // scenes must not accept recordings with different spawn points or navigation.
    static bool CompatibleReplayScene(string recorded, string current)
    {
        if (recorded == current) return true;
        return (recorded == "Level_01" && current == "Level_01_VisualExperiment") ||
            (recorded == "Level_01_VisualExperiment" && current == "Level_01");
    }

    void OnDisable() { Stop(); }
    void OnDestroy()
    {
        if (shooting != null) shooting.ShotFired -= RecordShot;
    }
}
