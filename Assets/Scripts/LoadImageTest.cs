using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.IO;

public class LoadImageTest : MonoBehaviour
{
    public RawImage displayImage;

    private Texture2D textureIdle;
    private Texture2D textureWaiting;
    private Texture2D textureTalking;

    string basePrompt = "a portrait of a women with blond hair, 150x150 pixels, ";

    void Start()
    {
        StartCoroutine(LoadAllImages());
    }

    IEnumerator LoadAllImages()
    {
        string apiKey = File.ReadAllText(
            Path.Combine(Application.dataPath, "Configuration/keyPollinations.txt")
        ).Trim();

        Coroutine c1 = StartCoroutine(LoadImage(basePrompt + "neutral face", "idle", apiKey, (tex) => textureIdle = tex));
        Coroutine c2 = StartCoroutine(LoadImage(basePrompt + "waiting, thinking, looking up", "waiting", apiKey, (tex) => textureWaiting = tex));
        Coroutine c3 = StartCoroutine(LoadImage(basePrompt + "talking, mouth open, speaking", "talking", apiKey, (tex) => textureTalking = tex));

        yield return c1;
        yield return c2;
        yield return c3;

        displayImage.texture = textureIdle;
        Debug.Log("All images have been uploaded!");
    }

    IEnumerator LoadImage(string prompt, string fileName, string apiKey, System.Action<Texture2D> onDone)
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "GeneratedImages"));
        string savePath = Path.Combine(Application.dataPath, "GeneratedImages", fileName + ".png");

        // If it's already saved, load it from disk.
        if (File.Exists(savePath))
        {
            Debug.Log("Loading from disk: " + fileName);
            byte[] bytes = File.ReadAllBytes(savePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            onDone(tex);
            yield break;
        }

        // Otherwise, we generate through Pollinations
        Debug.Log("Generating image: " + prompt);
        string encodedPrompt = UnityWebRequest.EscapeURL(prompt);
        string url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=150&height=150&model=flux-schnell&nologo=true&key={apiKey}";

        UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error: " + req.error);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(req);

        // Save to disk
        File.WriteAllBytes(savePath, texture.EncodeToPNG());
        Debug.Log("Saved: " + savePath);

        onDone(texture);
    }

    public void ShowIdle()    => displayImage.texture = textureIdle;
    public void ShowWaiting() => displayImage.texture = textureWaiting;
    public void ShowTalking() => displayImage.texture = textureTalking;
}