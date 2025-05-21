using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Base functionality shared by Named pipe IPC server and client.
/// Handles message queueing, event dispatching and thread-safe logging.
/// </summary>
public abstract class NamedPipeIPCBase<T> : MonoBehaviour where T : NamedPipeIPCBase<T>
{
    public delegate void DataReceived(string newData);
    public static event DataReceived OnDataReceived;

    protected const int MaxPayloadBytes = 4096;
    protected readonly ConcurrentQueue<string> sendQueue = new();
    protected SynchronizationContext unityContext;

    //---------------------------------------------------------------------------
    protected virtual void Awake()
    {
        unityContext = SynchronizationContext.Current;
    }

    //---------------------------------------------------------------------------
    protected bool EnqueueMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return true;

        int bytes = Encoding.UTF8.GetByteCount(message);
        if (bytes > MaxPayloadBytes)
        {
            LogError($"IPC Send rejected – payload {bytes} B exceeds 4 KB limit.");
            return false;
        }

        sendQueue.Enqueue(message);
        return true;
    }

    //---------------------------------------------------------------------------
    protected void RaiseDataReceived(string data)
    {
        if (OnDataReceived == null) return;
        Debug.Log(data);

        void InvokeEvent()
        {
            try { OnDataReceived?.Invoke(data); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        if (unityContext != null)
            unityContext.Post(_ => InvokeEvent(), null);
        else
            InvokeEvent();
    }

    //---------------------------------------------------------------------------
    protected void Post(SendOrPostCallback cb)
    {
        if (unityContext != null)
            unityContext.Post(cb, null);
        else
            cb(null);
    }

    //---------------------------------------------------------------------------
    protected virtual void Log(string msg) =>
        Post(_ => { if (Application.isPlaying) Debug.Log(msg); });


    //---------------------------------------------------------------------------
    protected virtual void LogError(string msg) =>
        Post(_ => { if (Application.isPlaying) Debug.LogError(msg); });
}
