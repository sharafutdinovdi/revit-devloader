using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Nice3point.Revit.Extensions;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace ElementCounter;

[Transaction(TransactionMode.Manual)]
public sealed class CountCommand : ExternalCommand
{
    public override void Execute()
    {
        var document = UiApplication.ActiveUIDocument?.Document;
        if (document is null || !FilteredElementCollector.IsViewValidForElementIteration(document, document.ActiveView.Id))
        {
            new TaskDialog("Element Counter") { MainInstruction = "Open a model view to count its elements.", CommonButtons = TaskDialogCommonButtons.Ok }.Show();
            return;
        }
        using var collector = document.CollectElements(document.ActiveView).Instances();
        var categories = collector.ToElements()
            .GroupBy(element => element.Category?.Name ?? "Uncategorized")
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count).ThenBy(group => group.Name).Take(8)
            .Select(group => $"{group.Name}: {group.Count}").ToList();
        new TaskDialog("Element Counter")
        {
            MainInstruction = "Elements in the active view: top categories",
            MainContent = categories.Count == 0 ? "No elements in this view. Open a populated model view and run again." : string.Join("\n", categories),
            CommonButtons = TaskDialogCommonButtons.Ok
        }.Show();
    }
}
