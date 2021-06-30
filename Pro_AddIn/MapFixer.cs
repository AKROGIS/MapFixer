using System;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using MovesDatabase;
using System.Linq;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;

namespace MapFixer
{

    // Notes for Pro Implementation:
    // Get all maps
    //   foreach (var mpi in Project.Current.GetItems<MapProjectItem>()) { map = mpi.GetMap(); }
    // get all broken layers for a map (ConnectionStatus is inherited from MapMember)
    //   foreach (var layer = map.GetLayersAsFlattenedList().Where(l => l.ConnectionStatus == ConnectionStatus.Broken))
    // fix layers by changing workspace path
    //   layer.FindAndReplaceWorkspacePath(str1, str2, check)
    // or by replacing datasource - need to build newDataset by creating new <Type>DataSource(connector) then OpenDataset<Type>(uri)
    //   if (Layer.CanReplaceDataSource(newDataset)) { layer.ReplaceDataSource(newDataset); }

    public class MapFixer
    {
        /// <summary>
        /// Fix the broken links in map that have solutions in moves.
        /// Must be called on the MCT (inside QueuedTask.Run)
        /// </summary>
        /// <param name="map">The map to check</param>
        /// <param name="moves">A database of known solutions to broken links</param>
        public static void FixMap(Map map, Moves moves)
        {
            if (map == null) { return; }

            var autoFixesApplied = 0;
            var unFixableLayers = 0;
            var intentionallyBroken = 0;
            foreach (var layer in map.GetLayersAsFlattenedList().Where(l => l.ConnectionStatus == ConnectionStatus.Broken))
            {
                Moves.GisDataset? oldDataset = GetDataset(layer);
                if (oldDataset == null)
                {
                    unFixableLayers += 1;
                    continue;
                }
                Moves.Solution? maybeSolution = moves.GetSolution(oldDataset.Value);
                if (maybeSolution == null)
                {
                    unFixableLayers += 1;
                    continue;
                }
                Moves.Solution solution = maybeSolution.Value;
                if (solution.NewDataset != null && solution.ReplacementDataset == null &&
                    solution.ReplacementLayerFilePath == null && solution.Remarks == null)
                {
                    // This is the typical solution (only choice is to update the dataset path).
                    // The user is not prompted, since there is no good reason for a user not to click OK.
                    // The user will be warned that layers have been fixed, and they can choose to not save the changes.
                    autoFixesApplied += 1;
                    RepairWithDataset(layer, oldDataset.Value, solution.NewDataset.Value);
                }
                else
                {
                    var selector = new SelectorWindow
                    {
                        // TODO: fix this -
                        // Setting the owner to the MainWindow will keep the dialog tied to the MainWindow, but it causes the following exception
                        // System.InvalidOperationException: 'The calling thread cannot access this object because a different thread owns it.'
                        // neither Dispatch.invoke(() => { ... }) on selector or the MainWindow worked.
                        //Owner = FrameworkApplication.Current.MainWindow,
                        LayerName = layer.Name,
                        Solution = solution
                    };
                    selector.ShowDialog();
                    if (selector.UseLayerFile)
                    {
                        RepairWithLayerFile(map, layer, selector.LayerFile, selector.KeepBrokenLayer);
                        if (selector.KeepBrokenLayer)
                        {
                            intentionallyBroken += 1;
                        }
                    }
                    else if (selector.UseDataset && selector.Dataset.HasValue)
                    {
                        RepairWithDataset(layer, oldDataset.Value, selector.Dataset.Value);
                    }
                    else
                    {
                        intentionallyBroken += 1;
                    }
                }

            }

            // Refresh TOC
            //TODO - if needed.

            // Print a Summary
            var brokenDataSourcesCount = map.GetLayersAsFlattenedList().Count(l => l.ConnectionStatus == ConnectionStatus.Broken);
            // Some unfixable layers may actually have been corrected by fixing the Mosaic Dataset Layer
            // Limit unfixable to no more than the actual number of broken layers
            unFixableLayers = Math.Min(unFixableLayers, brokenDataSourcesCount);
            if (autoFixesApplied > 0 || unFixableLayers > 0 || brokenDataSourcesCount > intentionallyBroken)
            {
                string msg = "";
                if (autoFixesApplied > 0)
                {
                    msg +=
                        $"{autoFixesApplied} broken layers were automatically fixed based on the new locations of known data sources. " +
                        "Close the document without saving if this is not what you want.";
                }
                if (autoFixesApplied > 0 && (unFixableLayers > 0 || brokenDataSourcesCount > 0))
                {
                    msg += "\n\n";
                }
                if (unFixableLayers > 0)
                {
                    msg +=
                        $"{unFixableLayers} broken layers could not be fixed; breakage is not due to changes on the PDS (X drive).";
                }
                if (unFixableLayers < brokenDataSourcesCount - intentionallyBroken)
                {
                    // We know that brokenDataSources.Count must be >= unFixableLayers, therefore some of the fixes need fixing
                    if (unFixableLayers > 0)
                    {
                        msg += "\n\n";
                    }
                    msg += "Additional fixes are possible and needed.  Please save, close and reopen your map.";
                }
                var title = @"Map Fixer Summary";
                MessageBox.Show(msg, title);
            }
        }

        private static void RepairWithLayerFile(Map map, Layer layer, string newLayerFile, bool keepBrokenLayer)
        {
            // Create Layer from *.lyrx file:  https://github.com/esri/arcgis-pro-sdk/wiki/ProSnippets-MapAuthoring#create-layer-from-a-lyrx-file

            // Pro Addin SDK can only open *.lyrx, not *.lyr:
            // Assume Data Manager manually created a shadow *.lyrx file for the *.lyr file in the moves database
            // TODO: If *.lyrx file does not exist, create *.lyrx from *.lyr file; must be done with an external python process
            if (newLayerFile.EndsWith(".lyr", StringComparison.OrdinalIgnoreCase))
            {
                newLayerFile += "x";
            }
            Layer newLayer = null;
            try
            {
                var layerDocument = new LayerDocument(newLayerFile);
                var layerParameters = new LayerCreationParams(layerDocument.GetCIMLayerDocument());
                newLayer = LayerFactory.Instance.CreateLayer<Layer>(layerParameters, map, LayerPosition.AutoArrange);
            }
            catch (Exception) {}
            if (newLayer != null)
            {
                if (!keepBrokenLayer)
                {
                    map.RemoveLayer(layer);
                }
            }
            else
            {
                // Notify the user that the LayerFile could not be opened (missing, corrupt, ...)
                var title = @"Map Fixer Error";
                var msg = $"The layer file '{newLayerFile}' could not be opened.";
                MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private static void RepairWithDataset(Layer layer, Moves.GisDataset oldDataset, Moves.GisDataset newDataset)
        {
            // This routine, can only repair workspace path, and dataset name.
            // The workspace type and data type must be the same.
            // This is checked with a warning in the CSV verifier.
            // Violations are silently ignored in the CSV loader so this code should never see it
            // however if it escapes, it will also be ignored here as well.
            if (oldDataset.DatasourceType != newDataset.DatasourceType ||
                oldDataset.WorkspaceProgId != newDataset.WorkspaceProgId)
            {
                return;
            }
            if (oldDataset.DatasourceName == newDataset.DatasourceName)
            {
                layer.FindAndReplaceWorkspacePath(oldDataset.Workspace.Folder, newDataset.Workspace.Folder, false);
            }
            else
            {
                if (Enum.TryParse(newDataset.DatasourceType, out esriDatasetType dataType) &&
                    Enum.TryParse(newDataset.WorkspaceProgId, out WorkspaceFactory workspaceFactory))
                {
                    //TODO: Replace the existing data connection with the same sub class of CIMDataConnection,
                    //      Need support from the moves database and GetDataset() below to support this.
                    //      Currently GetDataset() will ignore all but CIMStandardDataConnection, so we can
                    //      safely assume that is what we need to create
                    string workspaceConnection = "DATABASE=" + newDataset.Workspace.Folder;
                    CIMStandardDataConnection updatedDataConnection = new CIMStandardDataConnection()
                    {
                        WorkspaceConnectionString = workspaceConnection,
                        WorkspaceFactory = workspaceFactory,
                        Dataset = newDataset.DatasourceName,
                        DatasetType = dataType
                    };
                    layer.SetDataConnection(updatedDataConnection);
                }
                else
                {
                    var title = @"Map Fixer Error";
                    var msg = $"Map Fixer is unable to repair the layer {layer.Name}. " +
                              "Use the 'Set Data Source button' on the Source tab of the layer properties dialog to " +
                              $"set the data source to {newDataset.Workspace.Folder}\\{newDataset.DatasourceName}";
                    MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }

            // Alternative implementation
            // switch on newDataset.WorkspaceProgId, newDataset.DatasourceType
            // uri = Uri(Path.Join(newDataset.Workspace.Folder, newDataset.DatasourceName)
            // connection = <Type>ConnectionPath(uri, newDataset.DatasourceType)
            // var dataset = OpenDataset<Type>(new <Type>DataSource(connection))
            // i.e. file geodatabase DatasourceType = .fgdb //ArcGIS.Core.CIM.esriDatasetType, ArcGIS.Core.CIM.WorkspaceFactory
            // var connection = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(@"newDataset.Workspace.Folder")))
            // var dataset = connection.OpenDataset<FeatureDataset>(newDataset.DatasourceName)
            // for .raster and .shape
            // var connection = new FileSystemDataStore(new FileSystemConnectionPath(new Uri(@"newDataset.Workspace.Folder")))
            // var dataset = connection.OpenDataset<BasicRasterDataset>(newDataset.DatasourceName)
        }

        private static Moves.GisDataset? GetDataset(Layer layer)
        {
            if (!(layer.GetDataConnection() is CIMStandardDataConnection dataConnection)) { return null; }
            // TODO: consider other subclasses of CIMDataConnection; needs to be coordinated with RepairWithDataset() above.
            //       For data connection types, see https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/#topic943.html and
            //       https://github.com/AKROGIS/ThemeManager/blob/f8f6d7ba7b8aa62f577a4c8fde067cbc019eeb2e/ArcGisPro/ProLayer.cs#L381
            // TODO: CIMFeatureDatasetDataConnection is the second most common we might see (although there is currently nothing
            //       in the moves database that would require it).  For this subclass, append "/"+ dataConnnection.FeatureDataset
            //       to the workspace path.  In RepairWithDataset(), look for '/" and remove the feature dataset and create the a
            //       CIMFeatureDatasetDataConnection
            var datasetName = dataConnection.Dataset;
            var datasetType = dataConnection.DatasetType.ToString();
            var workspaceName = dataConnection.WorkspaceConnectionString;
            workspaceName = workspaceName.Replace("DATABASE=", "");
            if (!workspaceName.StartsWith("X:\\",StringComparison.OrdinalIgnoreCase)) { return null; }
            var workspaceFactory = dataConnection.WorkspaceFactory.ToString();
            return new Moves.GisDataset(workspaceName, workspaceFactory, datasetName, datasetType);
        }

    }
}