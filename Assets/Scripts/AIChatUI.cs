using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.IO;
using System.Text;

public class AIChatUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text responseText;

    private string apiKey;

    void Start()
    {
        apiKey = File.ReadAllText(
            Path.Combine(Application.dataPath, "Configuration/tokenChat.txt")
        ).Trim();
    }

    public void SendPrompt()
    {
        if (string.IsNullOrEmpty(inputField.text)) return;
        StartCoroutine(Request(inputField.text));
    }

    IEnumerator Request(string prompt)
    {
        responseText.text = "Thinking...";

        // Escape quotes and special characters
        string escaped = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        string json = $@"
        {{
            ""model"": ""gpt-4.1-mini"",
            ""messages"": [
                {{
                    ""role"": ""user"",
                    ""content"": ""{escaped}""
                }}
            ]
        }}";
        
        UnityWebRequest req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            responseText.text = "Error: " + req.error;
            yield break;
        }

    // Parse the response and display the content of the first choice
        responseText.text = JsonUtility.FromJson<OpenAIResponse>(req.downloadHandler.text).choices[0].message.content;
    }
}

[System.Serializable] public class Message  { public string content; }
[System.Serializable] public class Choice   { public Message message; }
[System.Serializable] public class OpenAIResponse { public Choice[] choices; }