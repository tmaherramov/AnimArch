using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text.RegularExpressions;

public class CopyHistoryButton : MonoBehaviour
{
    public TMP_Text chatText;
    public Button copyButton;
    public TMP_Text buttonLabel;

    void Start()
    {
        copyButton.onClick.AddListener(() => StartCoroutine(CopyWithFeedback()));
    }

    IEnumerator CopyWithFeedback()
    {
        GUIUtility.systemCopyBuffer = CleanText(chatText.text);
        buttonLabel.text = "Copied!";
        yield return new WaitForSeconds(1.5f);
        buttonLabel.text = "Copy";
    }

    string CleanText(string input)
    {
        string result = input;
        result = Regex.Replace(result, @"<color=[^>]+>", string.Empty);
        result = Regex.Replace(result, @"</color>", string.Empty);
        result = Regex.Replace(result, @"<b>", string.Empty);
        result = Regex.Replace(result, @"</b>", string.Empty);
        return result;
    }
}