using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public TracingSettings traceSettings = new TracingSettings();
    public LabelSettings labelSettings = new LabelSettings();
    public Camera camera;
    public GameObject carLabelPrefab;
    public Vector3 carStatusLabelOffset = new Vector3(0.5f, 0.5f, 0);
    public string[] LabelList = new string[] {
        "mom.png",
        "childhood_kitten.png",
        "passwords.txt",
        "bank_details.txt",
        "social_security_number.txt",
        "tax_return.pdf",
        "nudes.png",
        "budget.txt",
        "diary.txt",
        "gps_data.txt",
    };

    public Level[] levels;

    public TMPro.TextMeshProUGUI userMessageText;
    public AnimationCurve userMessageAnimationCurve;
    public enum DebugBehaviour
    {
        PlayFromBeginning,
        SkipToLevel,
    };
    public DebugBehaviour debugBehaviour;
    public int debugLevel = 0;


    public int currentLevel;

    void Start()
    {
        if (debugBehaviour == DebugBehaviour.PlayFromBeginning)
        {
            StartCoroutine(StartGame());
        }
        else if (debugBehaviour == DebugBehaviour.SkipToLevel)
        {
            var level = levels[debugLevel];
            camera.transform.position = level.transform.position;
            StartCoroutine(level.StartLevel(this));
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
            transform.localScale = Vector3.one * animationCurve.Evaluate(Time.time - startTime);
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
            yield return StartCoroutine(ShowMessageToUser($"Level {currentLevel + 1} - {level.LevelName}"));
            yield return StartCoroutine(level.StartLevel(this));
            yield return StartCoroutine(ShowMessageToUser($"Done Deal!"));
            currentLevel = (currentLevel + 1) % levels.Length;
        }
    }
}
