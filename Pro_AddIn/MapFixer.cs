using System;
using System.Collections.Generic;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using MovesDatabase;
using System.Linq;

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
        // Must be called on the MCT (inside QueuedTask.Run)
        public static void FixMap(Map map, Moves moves)
        {
            if (map == null) { return; }

            var autoFixesApplied = 0;
            var unFixableLayers = 0;
            var intentionallyBroken = 0;
            foreach (var layer in map.GetLayersAsFlattenedList().Where(l => l.ConnectionStatus == ConnectionStatus.Broken))
            {
                Moves.GisDataset oldDataset = GetDataset(layer);
                Moves.Solution? maybeSolution = moves.GetSolution(oldDataset);
                if (maybeSolution == null)
                {
                    unFixableLayers += 1;
                    continue;
                }
                Moves.Solution solution = maybeSolution.Value;
                if (solution.NewDataset != null && solution.ReplacementDataset == null &&
                    solution.ReplacementLayerFilePath == null && solution.Remarks == null)
                {
                    // This is the optimal action.
                    // The user is not prompted, since there is no good reason for a user not to click OK.
                    // The user will be warned that layers have been fixed, and they can choose to not save the changes.
                    autoFixesApplied += 1;
                    RepairWithDataset(layer, oldDataset, solution.NewDataset.Value);
                }
                else
                {
                    selector.LayerName = layer.Name;
                    //selector.GisDataset = oldDataset;
                    selector.Solution = solution;
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
                        RepairWithDataset(map, layer, oldDataset, selector.Dataset.Value);
                    }
                    else
                    {
                        intentionallyBroken += 1;
                    }
                }

            }

            // Refresh TOC
            ArcMap.Document.UpdateContents(); //update the TOC
            ArcMap.Document.ActivatedView.Refresh(); // refresh the view

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
            // Add Layer File to ActiveView Snippet: (http://help.arcgis.com/en/sdk/10.0/arcobjects_net/componenthelp/index.html#//004900000050000000)
            IGxLayer gxLayer = new GxLayer();
            IGxFile gxFile = (IGxFile)gxLayer;
            gxFile.Path = newLayerFile;
            int mapIndex = 0; //TODO:  need layer.Index
            if (gxLayer.Layer != null)
            {
                // AddLayer will add the new layer at the most appropriate point in the TOC.
                //   This is much easier and potentially less confusing than adding at the old data location. 
                ArcMap.Document.Maps.Item[mapIndex].AddLayer(gxLayer.Layer);
                if (!keepBrokenLayer)
                {
                    ArcMap.Document.Maps.Item[mapIndex].DeleteLayer((ILayer)dataLayer);
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

        private static void RepairWithDataset(Map map, Layer layer, Moves.GisDataset oldDataset, Moves.GisDataset newDataset)
        {
            // This routine, can only repair workspace path, and dataset name.
            // The workspace type and data type must be the same.
            // This can be checked with the CSV verifier. Violations will be ignored in the CSV loader
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
                DataSource dataset = null; //  new <Type>DataSource(connector)
                //OpenDataset<Type>(uri)
                if (dataset == null || !Layer.CanReplaceDataSource(dataset))
                {
                    var title = @"Map Fixer Error";
                    var msg = $"Map Fixer is unable to repair the layer {layer.Name}. " +
                              "Use the 'Set Data Source button' on the Source tab of the layer properties dialog to " +
                              $"set the data source to {newDataset.Workspace.Folder}\\{newDataset.DatasourceName}";
                    MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
                layer.ReplaceDataSource(dataset);
            }
        }

        private static Moves.GisDataset GetDataset(Layer dataLayer)
        {
            
            //TODO: dataLayer.DataSourceName is an IName.  Are we guaranteed this cast will not throw an exception?
            var datasetName = (IDatasetName)dataLayer.DataSourceName;
            IWorkspaceName workspaceName = datasetName.WorkspaceName;
            //TODO: If the workspace.PathName is null (probably true for SDE or OleDB) then the GisDataset ctor will throw an exception.
            // Maybe check IWorkspaceName.Type != esriWorkspaceType.esriRemoteDatabaseWorkspace (esriFileSystemWorkspace and esriLocalDatabaseWorkspace are ok)
            // Looking at ~6300 data sources in Theme Manager, all have a pathName, for SDE it is the connection file, for web services it is the URL
            return new Moves.GisDataset(workspaceName.PathName, workspaceName.WorkspaceFactoryProgID,
                datasetName.Name, datasetName.Type.ToString());
        }

    }
}
}