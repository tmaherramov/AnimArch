using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class AIChatUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text responseText;

    public LoadImageTest avatar;
    public TMP_InputField avatarPromptField;

    private string apiKey;
    private List<ChatEntry> history = new List<ChatEntry>();

    void Start()
    {
        apiKey = File.ReadAllText(
            Path.Combine(Application.dataPath, "Configuration/tokenChat.txt")
        ).Trim();

        inputField.onValueChanged.AddListener(OnTyping);
    }

    private void OnTyping(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            avatar.ShowWaiting();
        }
        else
        {
            avatar.ShowIdle();
        }
    }

    public void SendPrompt()
    {
        if (string.IsNullOrEmpty(inputField.text)) return;
        StartCoroutine(Request(inputField.text));
        inputField.text = "";
    }

    public void ChangeAvatar()
    {
        string prompt = avatarPromptField.text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;
        StartCoroutine(avatar.RegenerateAvatar(prompt));
        avatarPromptField.text = "";
    }

    IEnumerator Request(string prompt)
    {
        history.Add(new ChatEntry { role = "user", content = prompt });
        responseText.text = "Thinking...";
        avatar.ShowThinking();

        // Собираем все сообщения из истории в JSON
        StringBuilder messages = new StringBuilder("[");
        for (int i = 0; i < history.Count; i++)
        {
            string escaped = history[i].content
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
            messages.Append($"{{\"role\":\"{history[i].role}\",\"content\":\"{escaped}\"}}");
            if (i < history.Count - 1) messages.Append(",");
        }
        messages.Append("]");

        string json = $"{{\"model\":\"gpt-4.1-mini\",\"messages\":{messages}}}";

        UnityWebRequest req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            responseText.text = "Error: " + req.error;
            
            avatar.ShowIdle();
            yield break;
        }

        string reply = JsonUtility.FromJson<OpenAIResponse>(req.downloadHandler.text).choices[0].message.content;
        history.Add(new ChatEntry { role = "assistant", content = reply });
        avatar.ShowTalking();

        responseText.text = reply;
        yield return new WaitForSeconds(3f);
        avatar.ShowIdle();
    }
}

[System.Serializable] public class ChatEntry      { public string role; public string content; }
[System.Serializable] public class Message        { public string content; }
[System.Serializable] public class Choice         { public Message message; }
[System.Serializable] public class OpenAIResponse { public Choice[] choices; }