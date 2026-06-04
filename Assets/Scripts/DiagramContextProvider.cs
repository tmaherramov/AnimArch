using Visualization.Animation;

public static class DiagramContextProvider
{
    public static string GetPlantUML()
    {
        string uml = new PlantUMLBuilder().GetDiagram();
        // If the diagram is empty, the builder will return only @startuml/@enduml without classes.
        if (string.IsNullOrWhiteSpace(uml))
            return null;

        if (!uml.Contains("class "))
            return null;
        return uml;
    }
}