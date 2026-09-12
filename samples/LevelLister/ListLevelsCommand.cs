using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Nice3point.Revit.Extensions;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace LevelLister;

[Transaction(TransactionMode.Manual)]
public sealed class ListLevelsCommand : ExternalCommand
{
    public override void Execute()
    {
        var document = UiApplication.ActiveUIDocument?.Document;
        if (document is null)
        {
            new TaskDialog("Level Lister") { MainInstruction = "Open a model to list its levels.", CommonButtons = TaskDialogCommonButtons.Ok }.Show();
            return;
        }
        using var collector = document.CollectElements().OfClass<Level>();
        var levels = collector.ToElements().Cast<Level>()
            .OrderBy(level => level.Elevation).ThenBy(level => level.Name)
            .Select(level => $"{level.Name}: {level.Elevation.ToMillimeters():0.##} mm").ToList();
        new TaskDialog("Level Lister")
        {
            MainInstruction = "Model levels",
            MainContent = levels.Count == 0 ? "No levels in this model. Add a level and run again." : string.Join("\n", levels),
            CommonButtons = TaskDialogCommonButtons.Ok
        }.Show();
    }
}
