using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace MapFixer
{
    internal class CheckMapButton : Button
    {
        async protected override void OnClick()
        {
            // Button has a precondition that there is an Active MapView
            await CheckMapModule.Current.CheckMap(MapView.Active.Map);
        }
    }
}
