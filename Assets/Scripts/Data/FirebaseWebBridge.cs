#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

// Browser-only transport. The native Firebase SDK remains in use on Android/PC.
public sealed class FirebaseWebBridge : MonoBehaviour
{
    [Serializable] private sealed class Request { public int id; public string operation, payload; }
    [Serializable] public sealed class Response { public int id; public string error, value; public int[] metrics; }
    [DllImport("__Internal")] private static extern void MequiFirebaseRequest(string receiver, string request);
    private static FirebaseWebBridge instance;
    private readonly Dictionary<int, TaskCompletionSource<Response>> pending = new Dictionary<int, TaskCompletionSource<Response>>();
    private readonly Dictionary<int, Action<Response>> listeners = new Dictionary<int, Action<Response>>();
    private int nextId;

    private static FirebaseWebBridge Instance => instance != null ? instance :
        instance = FirebaseSessionSync.Instance.gameObject.AddComponent<FirebaseWebBridge>();

    public static Task<Response> Call(string operation, string payload = "")
    {
        FirebaseWebBridge bridge = Instance;
        int id = ++bridge.nextId;
        var completion = new TaskCompletionSource<Response>();
        bridge.pending.Add(id, completion);
        bridge.Send(id, operation, payload);
        return completion.Task;
    }

    public static int Listen(string path, Action<Response> changed)
    {
        FirebaseWebBridge bridge = Instance;
        int id = ++bridge.nextId;
        bridge.listeners.Add(id, changed);
        bridge.Send(id, "listen", path);
        return id;
    }

    public static void Stop(int id)
    {
        if (instance == null) return;
        instance.listeners.Remove(id);
        instance.Send(id, "stop", "");
    }

    private void Send(int id, string operation, string payload) => MequiFirebaseRequest(gameObject.name,
        JsonUtility.ToJson(new Request { id = id, operation = operation, payload = payload }));

    [Preserve]
    public void OnFirebaseWebResponse(string json)
    {
        Response response = JsonUtility.FromJson<Response>(json);
        if (pending.TryGetValue(response.id, out TaskCompletionSource<Response> completion))
        {
            pending.Remove(response.id);
            if (string.IsNullOrEmpty(response.error)) completion.TrySetResult(response);
            else completion.TrySetException(new InvalidOperationException(response.error));
        }
        else if (listeners.TryGetValue(response.id, out Action<Response> listener)) listener(response);
    }

    private void OnDestroy()
    {
        foreach (int id in listeners.Keys) Send(id, "stop", "");
        foreach (var completion in pending.Values) completion.TrySetCanceled();
        listeners.Clear();
        pending.Clear();
        if (instance == this) instance = null;
    }
}
#endif
