using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Nice3point.Revit.Extensions;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace HelloPlugin;

[Transaction(TransactionMode.Manual)]
public sealed class HelloCommand : ExternalCommand
{
    public override void Execute()
    {
        new TaskDialog("Hello Plugin")
        {
            MainInstruction = "This is your first plugin",
            MainContent = "Installed by DevLoader. Edit HelloCommand.cs, rebuild, publish, update.",
            CommonButtons = TaskDialogCommonButtons.Ok
        }.Show();
    }
}
