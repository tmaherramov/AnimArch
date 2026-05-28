using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Collections.Generic;


public class LoadImageTest : MonoBehaviour
{
    public RawImage displayImage;

    private Texture2D textureIdle;
    private Texture2D textureWaiting;
    private Texture2D textureTalking;
    private Texture2D textureThinking;

    private string apiKey;
    private AvatarStatesJson avatarStates;



    void Start()
    {
        apiKey = File.ReadAllText(
            Path.Combine(Application.dataPath, "Configuration/tokenChat.txt")
        ).Trim();

        string configJson = File.ReadAllText(
            Path.Combine(Application.dataPath, "Configuration/avatarStates.json")
        );
        avatarStates = JsonUtility.FromJson<AvatarStatesJson>(configJson);

        StartCoroutine(LoadDefaultImages());
    }

    IEnumerator LoadDefaultImages()
    {
        string folder = Path.Combine(Application.dataPath, "GeneratedImages");

        textureIdle     = LoadFromDisk(Path.Combine(folder, "idle.png"));
        textureWaiting  = LoadFromDisk(Path.Combine(folder, "waiting.png"));
        textureTalking  = LoadFromDisk(Path.Combine(folder, "talking.png"));
        textureThinking = LoadFromDisk(Path.Combine(folder, "thinking.png"));

        if (textureIdle == null)
            Debug.LogError("idle.png not found! Put default images into Assets/GeneratedImages/");

        displayImage.texture = textureIdle;
        Debug.Log("Default avatar loaded.");
        yield break;
    }

    private Texture2D LoadFromDisk(string path)
    {
        if (!File.Exists(path)) return null;
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        return tex;
    }

    public IEnumerator RegenerateAvatar(string newBasePrompt)
    {
        Debug.Log("Generating new avatar: " + newBasePrompt);

        Texture2D newIdle = null;
        yield return StartCoroutine(GenerateImage(
            newBasePrompt + ", " + avatarStates.states[0].prompt,
            null,
            (tex) => newIdle = tex
        ));

        if (newIdle == null) { Debug.LogError("Failed to generate idle"); yield break; }

        Texture2D newWaiting = null, newTalking = null, newThinking = null;

        Coroutine c2 = StartCoroutine(GenerateImage(avatarStates.states[1].prompt, newIdle, (tex) => newWaiting  = tex));
        Coroutine c3 = StartCoroutine(GenerateImage(avatarStates.states[2].prompt, newIdle, (tex) => newTalking  = tex));
        Coroutine c4 = StartCoroutine(GenerateImage(avatarStates.states[3].prompt, newIdle, (tex) => newThinking = tex));

        yield return c2; yield return c3; yield return c4;

        if (newWaiting != null && newTalking != null && newThinking != null)
        {
            textureIdle     = newIdle;
            textureWaiting  = newWaiting;
            textureTalking  = newTalking;
            textureThinking = newThinking;
            displayImage.texture = textureIdle;
            Debug.Log("New avatar applied (in memory only).");
        }
    }

    IEnumerator GenerateImage(string prompt, Texture2D referenceImage, System.Action<Texture2D> onDone)
    {
        UnityWebRequest req;

        if (referenceImage == null)
        {
            string json = "{\"model\":\"gpt-image-1\",\"prompt\":\"" + prompt.Replace("\"", "\\\"") + "\",\"n\":1,\"size\":\"1024x1024\"}";
            req = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        }
        else
        {
            byte[] imageBytes = referenceImage.EncodeToPNG();
            string boundary = "---boundary" + System.DateTime.Now.Ticks.ToString("x");
            var bodyList = new System.Collections.Generic.List<byte>();
            var enc = System.Text.Encoding.UTF8;

            bodyList.AddRange(enc.GetBytes($"--{boundary}\r\nContent-Disposition: form-data; name=\"model\"\r\n\r\ngpt-image-1\r\n"));
            bodyList.AddRange(enc.GetBytes($"--{boundary}\r\nContent-Disposition: form-data; name=\"prompt\"\r\n\r\n{prompt}\r\n"));
            bodyList.AddRange(enc.GetBytes($"--{boundary}\r\nContent-Disposition: form-data; name=\"image[]\"; filename=\"reference.png\"\r\nContent-Type: image/png\r\n\r\n"));
            bodyList.AddRange(imageBytes);
            bodyList.AddRange(enc.GetBytes($"\r\n--{boundary}--\r\n"));

            req = new UnityWebRequest("https://api.openai.com/v1/images/edits", "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyList.ToArray());
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", $"multipart/form-data; boundary={boundary}");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        }

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image gen error: " + req.downloadHandler.text);
            onDone(null);
            yield break;
        }

        ImageResponse imgResponse = JsonUtility.FromJson<ImageResponse>(req.downloadHandler.text);
        byte[] pngBytes = System.Convert.FromBase64String(imgResponse.data[0].b64_json);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(pngBytes);
        onDone(texture);
    }

    public void ShowIdle()    => displayImage.texture = textureIdle;
    public void ShowWaiting() => displayImage.texture = textureWaiting;
    public void ShowTalking() => displayImage.texture = textureTalking;
    public void ShowThinking() => displayImage.texture = textureThinking;

}

[System.Serializable] public class ImageData     { public string b64_json; }
[System.Serializable] public class ImageResponse { public ImageData[] data; }
[System.Serializable] public class AvatarState  { public string name; public string prompt; }
[System.Serializable] public class AvatarStatesJson { public string basePrompt; public AvatarState[] states; }