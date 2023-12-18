﻿// https://stackoverflow.com/questions/41330771/use-unity-api-from-another-thread-or-call-a-function-in-the-main-thread

#define ENABLE_UPDATE_FUNCTION_CALLBACK
#define ENABLE_LATEUPDATE_FUNCTION_CALLBACK
#define ENABLE_FIXEDUPDATE_FUNCTION_CALLBACK

using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class UnityThread : MonoBehaviour
{
    //our (singleton) instance
    private static UnityThread instance;

    ////////////////////////////////////////////////UPDATE IMPL////////////////////////////////////////////////////////
    //Holds actions received from another Thread. Will be coped to actionCopiedQueueUpdateFunc then executed from there
    private static readonly List<Action> actionQueuesUpdateFunc = new ();

    //holds Actions copied from actionQueuesUpdateFunc to be executed
    private readonly List<Action> actionCopiedQueueUpdateFunc = new ();

    // Used to know if whe have new Action function to execute. This prevents the use of the lock keyword every frame
    private static volatile bool noActionQueueToExecuteUpdateFunc = true;

    ////////////////////////////////////////////////LATEUPDATE IMPL////////////////////////////////////////////////////////
    //Holds actions received from another Thread. Will be coped to actionCopiedQueueLateUpdateFunc then executed from there
    private static readonly List<Action> actionQueuesLateUpdateFunc = new ();

    //holds Actions copied from actionQueuesLateUpdateFunc to be executed
    private readonly List<Action> actionCopiedQueueLateUpdateFunc = new ();

    // Used to know if whe have new Action function to execute. This prevents the use of the lock keyword every frame
    private static volatile bool noActionQueueToExecuteLateUpdateFunc = true;

    ////////////////////////////////////////////////FIXEDUPDATE IMPL////////////////////////////////////////////////////////
    //Holds actions received from another Thread. Will be coped to actionCopiedQueueFixedUpdateFunc then executed from there
    private static readonly List<Action> actionQueuesFixedUpdateFunc = new ();

    //holds Actions copied from actionQueuesFixedUpdateFunc to be executed
    private readonly List<Action> actionCopiedQueueFixedUpdateFunc = new ();

    // Used to know if whe have new Action function to execute. This prevents the use of the lock keyword every frame
    private static volatile bool noActionQueueToExecuteFixedUpdateFunc = true;

    //Used to initialize UnityThread. Call once before any function here
    public static void initUnityThread(bool visible = false)
    {
        if (instance != null) { return; }

        if (Application.isPlaying)
        {
            // add an invisible game object to the scene
            var obj = new GameObject("MainThreadExecuter");

            if (!visible) { obj.hideFlags = HideFlags.HideAndDontSave; }

            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<UnityThread>();
        }
    }

    public void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    //////////////////////////////////////////////COROUTINE IMPL//////////////////////////////////////////////////////
#if (ENABLE_UPDATE_FUNCTION_CALLBACK)
    public static void executeCoroutine(IEnumerator action)
    {
        if (instance != null) { executeInUpdate(() => instance.StartCoroutine(action)); }
    }

    ////////////////////////////////////////////UPDATE IMPL////////////////////////////////////////////////////
    public static void executeInUpdate(Action action)
    {
        if (action == null) { throw new ArgumentNullException("action"); }

        lock (actionQueuesUpdateFunc)
        {
            actionQueuesUpdateFunc.Add(action);
            noActionQueueToExecuteUpdateFunc = false;
        }
    }

    public void Update()
    {
        if (noActionQueueToExecuteUpdateFunc) { return; }

        //Clear the old actions from the actionCopiedQueueUpdateFunc queue
        actionCopiedQueueUpdateFunc.Clear();

        lock (actionQueuesUpdateFunc)
        {
            //Copy actionQueuesUpdateFunc to the actionCopiedQueueUpdateFunc variable
            actionCopiedQueueUpdateFunc.AddRange(actionQueuesUpdateFunc);

            //Now clear the actionQueuesUpdateFunc since we've done copying it
            actionQueuesUpdateFunc.Clear();
            noActionQueueToExecuteUpdateFunc = true;
        }

        // Loop and execute the functions from the actionCopiedQueueUpdateFunc
        for (var i = 0; i < actionCopiedQueueUpdateFunc.Count; i++) { actionCopiedQueueUpdateFunc[i].Invoke(); }
    }
#endif

    ////////////////////////////////////////////LATEUPDATE IMPL////////////////////////////////////////////////////
#if (ENABLE_LATEUPDATE_FUNCTION_CALLBACK)
    public static void executeInLateUpdate(Action action)
    {
        if (action == null) { throw new ArgumentNullException("action"); }

        lock (actionQueuesLateUpdateFunc)
        {
            actionQueuesLateUpdateFunc.Add(action);
            noActionQueueToExecuteLateUpdateFunc = false;
        }
    }

    public void LateUpdate()
    {
        if (noActionQueueToExecuteLateUpdateFunc) { return; }

        //Clear the old actions from the actionCopiedQueueLateUpdateFunc queue
        actionCopiedQueueLateUpdateFunc.Clear();

        lock (actionQueuesLateUpdateFunc)
        {
            //Copy actionQueuesLateUpdateFunc to the actionCopiedQueueLateUpdateFunc variable
            actionCopiedQueueLateUpdateFunc.AddRange(actionQueuesLateUpdateFunc);

            //Now clear the actionQueuesLateUpdateFunc since we've done copying it
            actionQueuesLateUpdateFunc.Clear();
            noActionQueueToExecuteLateUpdateFunc = true;
        }

        // Loop and execute the functions from the actionCopiedQueueLateUpdateFunc
        for (var i = 0; i < actionCopiedQueueLateUpdateFunc.Count; i++) { actionCopiedQueueLateUpdateFunc[i].Invoke(); }
    }
#endif

    ////////////////////////////////////////////FIXEDUPDATE IMPL//////////////////////////////////////////////////
#if (ENABLE_FIXEDUPDATE_FUNCTION_CALLBACK)
    public static void executeInFixedUpdate(Action action)
    {
        if (action == null) { throw new ArgumentNullException("action"); }

        lock (actionQueuesFixedUpdateFunc)
        {
            actionQueuesFixedUpdateFunc.Add(action);
            noActionQueueToExecuteFixedUpdateFunc = false;
        }
    }

    public void FixedUpdate()
    {
        if (noActionQueueToExecuteFixedUpdateFunc) { return; }

        //Clear the old actions from the actionCopiedQueueFixedUpdateFunc queue
        actionCopiedQueueFixedUpdateFunc.Clear();

        lock (actionQueuesFixedUpdateFunc)
        {
            //Copy actionQueuesFixedUpdateFunc to the actionCopiedQueueFixedUpdateFunc variable
            actionCopiedQueueFixedUpdateFunc.AddRange(actionQueuesFixedUpdateFunc);

            //Now clear the actionQueuesFixedUpdateFunc since we've done copying it
            actionQueuesFixedUpdateFunc.Clear();
            noActionQueueToExecuteFixedUpdateFunc = true;
        }

        // Loop and execute the functions from the actionCopiedQueueFixedUpdateFunc
        for (var i = 0; i < actionCopiedQueueFixedUpdateFunc.Count; i++) { actionCopiedQueueFixedUpdateFunc[i].Invoke(); }
    }
#endif

    public void OnDisable()
    {
        if (instance == this) { instance = null; }
    }
}
