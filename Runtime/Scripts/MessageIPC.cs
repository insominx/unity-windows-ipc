using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class MessageIPC
{
    public string type;
    public string value;

    public string ToJson() => JsonUtility.ToJson(this);
    public static MessageIPC FromJson(string json) => JsonUtility.FromJson<MessageIPC>(json);
}
