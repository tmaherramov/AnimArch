using System.Collections.Generic;
using Assets.Scripts.AnimationControl.UMLDiagram;
using OALProgramControl;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using Visualization.Animation;
using Visualization.ClassDiagram;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Text;
using UnityEngine.UI.Extensions;
using Visualization.ClassDiagram.Diagrams;
using Visualization.ClassDiagram.Editors;
using Visualization.ClassDiagram.MarkedDiagram;
using Visualization.ClassDiagram.Relations;
using Visualization.UI;
using EditorChangesHistory;
using Visualization.Changes;


public static class PlantUmlExtractor
{
    public static string Extract(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var m = Regex.Match(
            input,
            @"@startuml.*?@enduml",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return m.Success ? m.Value : string.Empty;
    }
}

public class SuggestedDiagram : MonoBehaviour
{
    private sealed class SuggestionRequestResult
    {
        public string RequestPrompt;
        public string RawResponse;
        public string ExtractedPlantUml;
    }

    private static bool _suggestionsEnabled = false;
    private static int _minorChangesCount = 0;
    private static ResettableTimer _timer;
    private static bool _isDisplayingSuggestions;
    
    private static CancellationTokenSource _debounceCts;
    private static CancellationTokenSource _requestCts;
    private static int _requestVersion = 0;

    private static bool _refreshPending = false;
    private static bool _suppressTracking = true;

    public static void SetTimer(ResettableTimer timer)
    {
        _timer = timer;
    }
    
    public static bool SuggestionsEnabled
    {
        get => _suggestionsEnabled;
        set => _suggestionsEnabled = value;
    }
    
    public static bool SuppressTracking
    {
        get => _suppressTracking;
        set => _suppressTracking = value;
    }
    
    public static void ToggleSuggestions()
    {
        _suggestionsEnabled = !_suggestionsEnabled;

        if (_suggestionsEnabled)
        {
            Debug.Log("Suggestions enabled");
            SuppressTracking = false;
            ScheduleSuggestionsRefresh(immediate: true);
        }
        else
        {
            Debug.Log("Suggestions disabled");
            SuppressTracking = true;
            CancelAllWork();
            ClearSuggestions();
        }
    }
    
    private static void RunWithoutTracking(Action action)
    {
        bool prev = _suppressTracking;
        _suppressTracking = true;
        try
        {
            action();
        }
        finally
        {
            _suppressTracking = prev;
        }
    }

    public static void HandleNextSuggestionsRetrieval(DiagramChangeEvent changeEvent)
    {
        if (!_suggestionsEnabled)
            return;

        _timer?.OnUserAction();

        bool shouldScheduleRefresh = false;
        bool shouldClearSuggestions = false;

        if (changeEvent.Type == ChangeType.AddClass ||
            changeEvent.Type == ChangeType.RemoveClass ||
            changeEvent.Type == ChangeType.UpdateClass)
        {
            _minorChangesCount = 0;
            shouldClearSuggestions = true;
            shouldScheduleRefresh = true;
        }
        else if (changeEvent.Type == ChangeType.AddMethod ||
                 changeEvent.Type == ChangeType.UpdateMethod ||
                 changeEvent.Type == ChangeType.RemoveMethod ||
                 changeEvent.Type == ChangeType.AddAttribute ||
                 changeEvent.Type == ChangeType.UpdateAttribute ||
                 changeEvent.Type == ChangeType.RemoveAttribute ||
                 changeEvent.Type == ChangeType.AddRelation ||
                 changeEvent.Type == ChangeType.RemoveRelation)
        {
            if (_minorChangesCount == 0)
            {
                shouldClearSuggestions = true;
            }

            _minorChangesCount++;

            if (_minorChangesCount > 2)
            {
                _minorChangesCount = 0;
                shouldScheduleRefresh = true;
            }
            
            Debug.Log($"Minor changes: {_minorChangesCount}");
        }
        
        if (shouldClearSuggestions)
        {
            ClearSuggestions();
        }

        if (shouldScheduleRefresh)
        {
            ScheduleSuggestionsRefresh();
        }
    }
    
    private static void ScheduleSuggestionsRefresh(bool immediate = false)
    {
        _refreshPending = true;
        CancelCurrentRequest();

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();

        var token = _debounceCts.Token;
        if (immediate)
        {
            _ = DisplaySuggestionsAsync(token);
        }
        else
        {
            _ = DebounceAndDisplayAsync(token);
        }
    }

    private static async Task DebounceAndDisplayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), token);

            if (token.IsCancellationRequested || !_suggestionsEnabled || !_refreshPending)
                return;

            await DisplaySuggestionsAsync(token);
        }
        catch (OperationCanceledException)
        {
           
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DebounceAndDisplayAsync failed: {ex}");
            UXEventLogger.DebugLog("suggestions_debounce_error", new
            {
                error = ex.ToString()
            });
        }
    }
    
    private static void CancelCurrentRequest()
    {
        if (_requestCts != null)
        {
            _requestCts.Cancel();
            _requestCts.Dispose();
            _requestCts = null;
        }
        _requestVersion++;
    }

    private static void CancelAllWork()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        CancelCurrentRequest();

        _refreshPending = false;
    }
    
    public static void ClearSuggestions()
    {
        if (DiagramPool.Instance.CurrentDiffResult == null)
            return;

        RunWithoutTracking(() =>
        {
            ClassDiagramChangesVisualizer visualizer = new ClassDiagramChangesVisualizer(DiagramPool.Instance.CurrentDiffResult);
            visualizer.CleanupSuggestions();
            DiagramPool.Instance.CurrentDiffResult = null;

            if (DiagramPool.Instance.ClassDiagram?.graph != null)
            {
                DiagramPool.Instance.ClassDiagram.graph.Layout();
            }
        });
    }
    
    private static string BuildSystemPrompt(string plantUml, string changesHistory)
    {
        return $@"
        Imagine you're an experienced software engineer. You will be given a UML diagram in the form of PlantUML code.
        Your task is to suggest 3  small changes that the user would most likely want to make in the next step of their work.
        The changes don't have to be significant. Your limit on the number of changes: 3.

        Changes can be such as:
        - Adding/Removing relations/classes/methods or class attributes
        - Changing the name of a class/method/attribute

        Your answer should contain only PlantUML code (starting with the @startuml tag and ending with @enduml).
        Your answer should contain not only the changes, but simply all the code you received with your changes.

        The PlantUML code is provided below:
        {plantUml}

        Also you have provided a history of recent changes performed by user below:
        {changesHistory}

        If the revision history is empty and you're unsure what changes to make, add the ones a user would most likely make to complete the diagram.
        ".Trim();
    }
    
    private static string BuildRetryPromptContext(
        List<string> promptHistory,
        string failedModelOutput,
        string exceptionDetails,
        int attemptNumber)
    {
        if (promptHistory.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Previous attempt failed. Fix the previous result and return valid PlantUML only.");
        promptBuilder.AppendLine($"Retry attempt: {attemptNumber}.");
        promptBuilder.AppendLine("All previous prompts (accumulated):");

        for (int i = 0; i < promptHistory.Count; i++)
        {
            promptBuilder.AppendLine($"Prompt #{i + 1}:");
            promptBuilder.AppendLine(promptHistory[i]);
        }

        if (!string.IsNullOrWhiteSpace(failedModelOutput))
        {
            promptBuilder.AppendLine("Failed model output from previous attempt:");
            promptBuilder.AppendLine(failedModelOutput);
        }

        if (!string.IsNullOrWhiteSpace(exceptionDetails))
        {
            promptBuilder.AppendLine("Exception details from previous attempt:");
            promptBuilder.AppendLine(exceptionDetails);
        }

        promptBuilder.AppendLine("Return only complete PlantUML between @startuml and @enduml.");
        return promptBuilder.ToString().Trim();
    }

    private static string BuildFailedModelOutput(string rawResponse, string extractedPlantUml)
    {
        StringBuilder modelBuilder = new StringBuilder();
        modelBuilder.AppendLine("Raw model response:");
        modelBuilder.AppendLine(string.IsNullOrWhiteSpace(rawResponse) ? "<empty>" : rawResponse);
        modelBuilder.AppendLine("Extracted PlantUML:");
        modelBuilder.AppendLine(string.IsNullOrWhiteSpace(extractedPlantUml) ? "<empty>" : extractedPlantUml);
        return modelBuilder.ToString().Trim();
    }

    private static string BuildRequestPrompt(string retryPromptContext)
    {
        PlantUMLBuilder plantUmlBuilder = new PlantUMLBuilder();
        string plantUMLString = plantUmlBuilder.GetDiagram();
        Debug.Log($"Parsed PlantUML: {plantUMLString}");

        string changesHistory = DiagramChangeTracker.Instance.SerializeChanges();
        string systemPrompt = BuildSystemPrompt(plantUMLString, changesHistory);

        if (!string.IsNullOrWhiteSpace(retryPromptContext))
        {
            systemPrompt = $"{systemPrompt}\n\n{retryPromptContext}";
        }

        return systemPrompt;
    }

    private static async Task<SuggestionRequestResult> GetSuggestions(string requestPrompt, CancellationToken token)
    {
        Debug.Log($"Request GPT: {requestPrompt}");
        UXEventLogger.DebugLog("llm_request", new
        {
            prompt = requestPrompt
        });

        GPTMessage message = new GPTMessage();

        string response = await message.SendMessage(requestPrompt);

        token.ThrowIfCancellationRequested();

        Debug.Log($"Response GPT: {response}");
        UXEventLogger.DebugLog("llm_response", new
        {
            response = response
        });

        return new SuggestionRequestResult
        {
            RequestPrompt = requestPrompt,
            RawResponse = response,
            ExtractedPlantUml = PlantUmlExtractor.Extract(response)
        };
    }

    public static async Task DisplaySuggestionsAsync(CancellationToken outerToken)
    {
        const int maxRetries = 5;
        const int maxAttempts = maxRetries + 1;

        _refreshPending = false;

        CancelCurrentRequest();
        _requestCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        var token = _requestCts.Token;

        int myVersion = ++_requestVersion;
        List<string> promptHistory = new List<string>();
        string failedModelOutput = string.Empty;
        string previousExceptionDetails = string.Empty;

        try
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                int retryNumber = attempt - 1;
                string retryPromptContext = BuildRetryPromptContext(promptHistory, failedModelOutput, previousExceptionDetails, retryNumber);
                SuggestionRequestResult suggestionResult = null;

                try
                {
                    string requestPrompt = BuildRequestPrompt(retryPromptContext);
                    promptHistory.Add(requestPrompt);

                    if (retryNumber > 0)
                    {
                        Debug.LogWarning($"DisplaySuggestionsAsync retry attempt {retryNumber}/{maxRetries}. Prompt:\n{requestPrompt}");
                        UXEventLogger.DebugLog("llm_retry", new
                        {
                            retryNumber,
                            maxRetries,
                            attempt,
                            maxAttempts
                        });
                    }

                    suggestionResult = await GetSuggestions(requestPrompt, token);

                    if (token.IsCancellationRequested || myVersion != _requestVersion)
                    {
                        Debug.Log("Request has been cancelled");
                        UXEventLogger.DebugLog("llm_request_canceled", new
                        {
                            reason = "token_or_version_mismatch",
                            requestVersion = myVersion,
                            currentRequestVersion = _requestVersion
                        });
                        return;
                    }

                    string extractedPlantUML = suggestionResult.ExtractedPlantUml;
                    Debug.Log($"Serialized Plant UML: {extractedPlantUML}");

                    if (string.IsNullOrWhiteSpace(extractedPlantUML))
                    {
                        throw new InvalidOperationException($"Empty PlantUML suggestions. Raw response:\n{suggestionResult.RawResponse}");
                    }

                    ClassDiagramManager manager;
                    try
                    {
                        manager = UMLParserBridge.Parse(extractedPlantUML);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to parse suggested PlantUML:\n{extractedPlantUML}", ex);
                    }

                    if (token.IsCancellationRequested || myVersion != _requestVersion)
                    {
                        Debug.Log("Request has been cancelled");
                        UXEventLogger.DebugLog("llm_request_canceled", new
                        {
                            reason = "token_or_version_mismatch",
                            requestVersion = myVersion,
                            currentRequestVersion = _requestVersion
                        });
                        return;
                    }

                    ClassDiagramDiffer differ;
                    try
                    {
                        differ = ClassDiagramDiffer.CreateClassDiagramDifferWithCurrentDiagram();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to create ClassDiagramDiffer.", ex);
                    }

                    DiffResult diff;
                    try
                    {
                        diff = differ.GetDifference(manager);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to calculate diagram diff for suggestions.", ex);
                    }

                    if (token.IsCancellationRequested || myVersion != _requestVersion)
                    {
                        Debug.Log("Request has been cancelled");
                        UXEventLogger.DebugLog("llm_request_canceled", new
                        {
                            reason = "token_or_version_mismatch",
                            requestVersion = myVersion,
                            currentRequestVersion = _requestVersion
                        });
                        return;
                    }

                    DiagramPool.Instance.CurrentDiffResult = diff;

                    try
                    {
                        ClassDiagramChangesVisualizer visualizer = new ClassDiagramChangesVisualizer(diff);
                        visualizer.Visualize();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to visualize suggested changes.", ex);
                    }

                    if (token.IsCancellationRequested || myVersion != _requestVersion)
                    {
                        Debug.Log("Request has been cancelled");
                        UXEventLogger.DebugLog("llm_request_canceled", new
                        {
                            reason = "token_or_version_mismatch",
                            requestVersion = myVersion,
                            currentRequestVersion = _requestVersion
                        });
                        return;
                    }

                    try
                    {
                        DiagramPool.Instance.ClassDiagram.graph.Layout();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to layout diagram after suggestion visualization.", ex);
                    }

                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (suggestionResult != null)
                    {
                        failedModelOutput = BuildFailedModelOutput(suggestionResult.RawResponse, suggestionResult.ExtractedPlantUml);
                    }
                    else
                    {
                        failedModelOutput = BuildFailedModelOutput(string.Empty, string.Empty);
                    }

                    previousExceptionDetails = ex.ToString();
                    Debug.LogWarning($"DisplaySuggestionsAsync attempt {attempt}/{maxAttempts} failed: {ex}");
                    UXEventLogger.DebugLog("llm_attempt_failed", new
                    {
                        attempt,
                        maxAttempts,
                        retryNumber,
                        maxRetries,
                        error = ex.ToString()
                    });

                    if (retryNumber == maxRetries)
                    {
                        Debug.LogError($"DisplaySuggestionsAsync retries are exhausted after {maxRetries} retries.");
                        UXEventLogger.DebugLog("llm_retries_exhausted", new
                        {
                            maxRetries,
                            finalError = ex.ToString()
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Suggestions request canceled.");
            UXEventLogger.DebugLog("llm_request_canceled", new
            {
                reason = "operation_canceled_exception"
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DisplaySuggestionsAsync failed: {ex}");
            UXEventLogger.DebugLog("llm_display_failed", new
            {
                error = ex.ToString()
            });
        }
    }

    // Tymur
    public static void ShowSuggestionFromPlantUML(string plantUml)
    {
        if (string.IsNullOrWhiteSpace(plantUml))
            return;

        ClearSuggestions();


        ClassDiagramManager manager = UMLParserBridge.Parse(plantUml);

        ClassDiagramDiffer differ =
            ClassDiagramDiffer.CreateClassDiagramDifferWithCurrentDiagram();

        DiffResult diff = differ.GetDifference(manager);

        DiagramPool.Instance.CurrentDiffResult = diff;

        ClassDiagramChangesVisualizer visualizer =
            new ClassDiagramChangesVisualizer(diff);

        visualizer.Visualize();

        DiagramPool.Instance.ClassDiagram.graph.Layout();
    }
}
