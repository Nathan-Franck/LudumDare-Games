using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public Camera camera;

    public Level[] levels;

    public int currentLevel;

    void Start()
    {
        StartCoroutine(GoGame());
    }

    IEnumerator MoveCameraTo(Vector3 position, float time = 1.0f)
    {
        var startPosition = camera.transform.position;
        var startTime = Time.time;
        while (Time.time - startTime < time)
        {
            camera.transform.position = Vector3.Lerp(startPosition, position, (Time.time - startTime) / time);
            yield return null;
        }
    }

    IEnumerator GoGame()
    {
        while (true)
        {
            yield return StartCoroutine(MoveCameraTo(levels[currentLevel].transform.position));
            currentLevel = (currentLevel + 1) % levels.Length;
            yield return new WaitForSeconds(1.0f);
        }
    }
}
