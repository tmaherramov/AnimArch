using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine.UI;
using Visualization.UI;
using Visualization.Animation;

public class AIChatUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField inputField;
    public TMP_Text responseText;
    public ScrollRect scrollRect;

    public LoadImageTest avatar;
    public TMP_InputField avatarPromptField;

    private string apiKey;
    private List<ChatEntry> history = new List<ChatEntry>();

    private string EscapeJson(string text)
{
    return text
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "")
        .Replace("\n", "\\n");
}


    private void AddMessage(string text)
    {
        responseText.text += text + "\n";

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
    
    void Start()
    {

        responseText.text = "";
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

        string prompt = inputField.text;

        inputField.onValueChanged.RemoveListener(OnTyping);
        inputField.text = "";
        inputField.onValueChanged.AddListener(OnTyping);

        StartCoroutine(Request(prompt));
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

        if (responseText.text.Length > 0)
        {
            AddMessage("");
        }
        AddMessage("<color=#4FC3F7><b>You:</b></color> " + prompt);


        avatar.ShowThinking();

        
        StringBuilder messages = new StringBuilder("[");

        string plantUml = DiagramContextProvider.GetPlantUML();
        Debug.Log(plantUml);
        if (plantUml != null)
        {
            
            string systemContent =
            @"You are an experienced software engineer.

            The user is working on a UML class diagram.

            The current diagram is provided below in PlantUML format:

            " + plantUml + @"

            When discussing or modifying the diagram:

            - Base your answers on the provided diagram
            - Use the existing classes and relationships whenever possible
            - If you propose changes to the diagram, return ONLY valid PlantUML
            - The PlantUML must start with @startuml and end with @enduml
            - Do not include explanations inside the PlantUML

            The PlantUML will be parsed automatically by the application.";

            string escapedSystemContent = EscapeJson(systemContent);
            
            messages.Append($"{{\"role\":\"system\",\"content\":\"{escapedSystemContent}\"}},");
        }

        for (int i = 0; i < history.Count; i++)
        {
            string escaped = EscapeJson(history[i].content);
            messages.Append($"{{\"role\":\"{history[i].role}\",\"content\":\"{escaped}\"}}");
            if (i < history.Count - 1) messages.Append(",");
        }
        messages.Append("]");

        string json = $"{{\"model\":\"gpt-4.1-mini\",\"messages\":{messages}}}";
        Debug.Log(json);
        UnityWebRequest req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(req.downloadHandler.text);

            AddMessage(
                "<color=red><b>Error:</b></color> "
                + req.error
            );

            avatar.ShowIdle();
            yield break;
        }

        string reply = JsonUtility.FromJson<OpenAIResponse>(req.downloadHandler.text).choices[0].message.content;
        history.Add(new ChatEntry { role = "assistant", content = reply });
        avatar.ShowTalking();


        if (UIEditorManager.Instance != null &&
            UIEditorManager.Instance.active)
        {
            Debug.Log("Editor is active");

            string suggestedPlantUml = PlantUmlExtractor.Extract(reply);

            if (!string.IsNullOrWhiteSpace(suggestedPlantUml))
            {
                Debug.Log(suggestedPlantUml);


                SuggestedDiagram.ShowSuggestionFromPlantUML(
                    suggestedPlantUml
                );
            }
        }


        AddMessage("<color=#81C784><b>AI:</b></color> " + reply);


        yield return new WaitForSeconds(3f);
        avatar.ShowIdle();
    }
}

[System.Serializable] public class ChatEntry      { public string role; public string content; }
[System.Serializable] public class Message        { public string content; }
[System.Serializable] public class Choice         { public Message message; }
[System.Serializable] public class OpenAIResponse { public Choice[] choices; }