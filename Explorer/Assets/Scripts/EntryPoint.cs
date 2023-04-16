using System;
using System.Threading;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

public class EntryPoint : MonoBehaviour
{
    private SceneRuntime sceneRuntime;
    private void Awake()
    {
        sceneRuntime = new SceneRuntime(Helpers.LoadSceneSourceCode("Cube"));

        sceneRuntime.StartScene();
    }

    private void Update()
    {
        sceneRuntime.Update();
    }
}