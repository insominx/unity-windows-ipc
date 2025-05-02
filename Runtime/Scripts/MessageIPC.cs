using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class MessageIPC
{
    [FormerlySerializedAs("command")]
    public string type;
    public string value;

    // Optional helper: Convert to JSON string
    public string ToJson() => JsonUtility.ToJson(this);

    // Optional helper: Create from JSON string
    public static MessageIPC FromJson(string json) => JsonUtility.FromJson<MessageIPC>(json);
}
