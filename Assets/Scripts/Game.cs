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
    public TMPro.TextMeshProUGUI attemptsText;
    public AnimationCurve userMessageAnimationCurve;
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

    void Start()
    {
        userMessageText.enabled = false;
        timerText.enabled = false;
        attemptsText.enabled = false;
        if (debugBehaviour == DebugBehaviour.PlayFromBeginning)
        {
            StartCoroutine(StartGame());
        }
        else if (debugBehaviour == DebugBehaviour.SkipToLevel)
        {
            var level = levels[debugLevel];
            camera.transform.position = level.transform.position;
            StartCoroutine(level.StartLevel(this, currentLevel));
        }
    }

    public IEnumerator ShowMessageToUser(string message, float time = 1.0f)
    {
        userMessageText.enabled = true;
        userMessageText.text = message;
        yield return StartCoroutine(ScaleAnimation(userMessageText.transform, userMessageAnimationCurve));
        userMessageText.enabled = false;
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

    IEnumerator ScaleAnimation(Transform transform, AnimationCurve animationCurve)
    {
        var startTime = Time.time;
        while (Time.time - startTime < animationCurve.keys[animationCurve.length - 1].time)
        {
            transform.localScale = Vector3.one * animationCurve.Evaluate((Time.time - startTime).Quantize(animationCurveQuantum));
            yield return null;
        }
    }

    IEnumerator StartGame()
    {
        yield return StartCoroutine(ShowMessageToUser("Beat my game, gamer."));
        while (true)
        {
            yield return StartCoroutine(MovePositionOverTime(camera.transform, levels[currentLevel].transform.position));
            var level = levels[currentLevel];
            yield return StartCoroutine(level.StartLevel(this, currentLevel));
            currentLevel = (currentLevel + 1) % levels.Length;
        }
    }
}
