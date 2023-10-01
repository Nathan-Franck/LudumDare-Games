using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Overlay : MonoBehaviour
{
    public float ScreenBlackout = 0.0f;
    public float ChromaticAberration = 0.0f;
    public float CRT_Distortion = 0.0f;

    public Shader shader;
    private Material material;

    private void Awake()
    {
        material = new Material(shader);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        material.SetFloat("_ScreenBlackout", ScreenBlackout);
        material.SetFloat("_ChromaticAberration", ChromaticAberration);
        material.SetFloat("_CRT_Distortion", CRT_Distortion);
        Graphics.Blit(source, destination, material);
    }
}
