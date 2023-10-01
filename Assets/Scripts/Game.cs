using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using Unity.VisualScripting;
using UnityEngine;

public class Game : MonoBehaviour
{
    public TracingSettings traceSettings = new TracingSettings();
    public LabelSettings labelSettings = new LabelSettings();
    public BoundsSettings boundsSettings = new BoundsSettings();
    public float fitTolerance = 0.25f;
    [System.Serializable]
    public class CarLabelSettings
    {
        public GameObject revealPrefab;
        public GameObject collisionPrefab;
        public GameObject dockablePrefab;
        [DoNotSerialize]
        [HideInInspector]
        public string[] car_names = new string[] {
            "1994_family_memories.png",
            "childhood_kitten.png",
            "passwords.txt",
            "bank_details.txt",
            "social_security_number.txt",
            "tax_return.pdf",
            "homework.7z",
            "budget.txt",
            "DIARY_JAN_04.txt",
            "gps_data.txt",
            "browser_history.txt",
            "emails.txt",
            "photos.zip",
            "videos.zip",
            "music.zip",
            "quarantine_diary.txt",
            "lord_of_the_rings.md",
        };
        public AnimationCurve LineThicknessCurve;
        public AnimationCurve LabelVerticalCurve;
        public float TracesTime => LineThicknessCurve.keys[LineThicknessCurve.length - 1].time;
        public float TotalTime => Mathf.Max(LineThicknessCurve.keys[LineThicknessCurve.length - 1].time, LabelVerticalCurve.keys[LabelVerticalCurve.length - 1].time);
        public Vector3 statusOffset = new Vector3(0.5f, 0.5f, 0);
        public float quantization = 0.5f;
    }
    public float animationCurveQuantum = 0.05f;
    public CarLabelSettings carLabelSettings = new CarLabelSettings();
    public Camera camera;

    public Level[] levels;

    public TMPro.TextMeshProUGUI userMessageText;
    public TMPro.TextMeshProUGUI timerText;
    public AnimationCurve userMessageAnimationCurve;
    public AnimationCurve finalUserMessageAnimationCurve;
    public AnimationCurve dockCurve;
    public Vector3 dockVector = new Vector3(0, 1, 0);
    public Font labelFont;
    public enum DebugBehaviour
    {
        PlayFromBeginning,
        SkipToLevel,
    };
    public DebugBehaviour debugBehaviour;
    public int debugLevel = 0;
    public HashSet<string> usedLabels = new HashSet<string>();

    public int currentLevel;
    public Vector3 carParkOffset = new Vector3(0, 0.23f, 0);
    public Vector3 carParkEulers = new Vector3(0, 0, 90);
    public AnimationCurve failPulse;
    public Vector3 cameraFocusPoint = new Vector3(0, 0, -10);
    public int filesDeleted = 0;
    public Vector3 initialCameraPosition;

    void Start()
    {
        initialCameraPosition = camera.transform.position;
        userMessageText.enabled = false;
        timerText.enabled = false;
        if (debugBehaviour == DebugBehaviour.PlayFromBeginning)
        {
            StartCoroutine(StartGame());
        }
        else if (debugBehaviour == DebugBehaviour.SkipToLevel)
        {
            var overlay = camera.GetComponent<Overlay>();
            overlay.ScreenBlackout = 0;
            var level = levels[debugLevel];
            camera.transform.position = level.transform.position + cameraFocusPoint;
            StartCoroutine(level.StartLevel(this, currentLevel));
        }
    }

    public IEnumerator ShowMessageToUser(string message, float time = 1.0f)
    {
        userMessageText.enabled = true;
        userMessageText.text = message;
        yield return StartCoroutine(FadeTextAnimation(userMessageText, userMessageAnimationCurve));
        userMessageText.enabled = false;
    }

    public IEnumerator ShowFinalMessageToUser(string message, float time = 1.0f)
    {
        userMessageText.enabled = true;
        userMessageText.text = message;
        yield return StartCoroutine(FadeTextAnimation(userMessageText, finalUserMessageAnimationCurve));
    }

    IEnumerator MovePositionOverTime(Transform transform, Vector3 position, float time = 1.0f)
    {
        var startPosition = transform.position;
        var startTime = Time.time;
        while (Time.time - startTime < time)
        {
            transform.position = Vector3.Lerp(startPosition, position, (Time.time - startTime) / time);
            yield return null;
        }
    }

    IEnumerator FadeTextAnimation(TMPro.TextMeshProUGUI text, AnimationCurve animationCurve)
    {
        var startTime = Time.time;
        while (Time.time - startTime < animationCurve.keys[animationCurve.length - 1].time)
        {
            text.alpha = animationCurve.Evaluate(Time.time - startTime);
            yield return null;
        }
    }

    public IEnumerator FadeFromBlack(float time = 1.0f)
    {
        var overlay = camera.GetComponent<Overlay>();
        var startTime = Time.time;
        while (Time.time - startTime < time)
        {
            var t = (Time.time - startTime) / time;
            overlay.ScreenBlackout = 1 - t;
            yield return null;
        }
    }

    public IEnumerator FadeToBlack(float time = 1.0f)
    {
        var overlay = camera.GetComponent<Overlay>();
        var startTime = Time.time;
        while (Time.time - startTime < time)
        {
            var t = (Time.time - startTime) / time;
            overlay.ScreenBlackout = t;
            yield return null;
        }
    }

    IEnumerator StartGame()
    {
        var userWantsAgain = true;
        yield return StartCoroutine(ShowMessageToUser("Lets play a game"));
        while (userWantsAgain)
        {
            StartCoroutine(FadeFromBlack());
            while (currentLevel < levels.Length)
            {
                yield return StartCoroutine(MovePositionOverTime(camera.transform, levels[currentLevel].transform.position + cameraFocusPoint));
                var level = levels[currentLevel];
                yield return StartCoroutine(level.StartLevel(this, currentLevel));
                currentLevel++;
            }
            timerText.enabled = false;
            yield return StartCoroutine(FadeToBlack());
            yield return StartCoroutine(ShowFinalMessageToUser("your Lesson is complete.\nfiles_lost = " + filesDeleted + " " + (filesDeleted == 0 ? "\n\nuntil next time" : "\nretry? (Y/n)")));
            if (filesDeleted == 0)
            {
                // softlock the winner
                break;
            }
            yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.touchCount > 0 || Input.GetMouseButtonDown(0));
            if (Input.GetKeyDown(KeyCode.N))
            {
                yield return StartCoroutine(ShowFinalMessageToUser("c:/easy yt downloader/bin/Debug>"));
                userWantsAgain = false;
            }
            else if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.touchCount > 0 || Input.GetMouseButtonDown(0))
            {
                filesDeleted = 0;
                currentLevel = 0;
                camera.transform.position = initialCameraPosition;
                yield return StartCoroutine(ShowMessageToUser("your loss"));
            }
        }
    }
}
