﻿using System;
using System.Collections.Generic;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Catalog;
using MovesDatabase;

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
        public void FixMap(Moves moves)
        {
            var brokenDataSources = GetBrokenDataSources();
            // We do not need to do anything if there was nothing to fix
            if (brokenDataSources.Count == 0) {
                return;
            }
            var alert = new AlertForm();
            var selector = new SelectionForm();
            var autoFixesApplied = 0;
            var unFixableLayers = 0;
            var intentionallyBroken = 0;
            foreach (var item in brokenDataSources)
            {
                var mapIndex = item.Key;
                foreach (IDataLayer2 dataLayer in item.Value)
                {
                    var layerName = dataLayer is IDataset dataset ? dataset.Name : ((ILayer2)dataLayer).Name;
                    Moves.GisDataset oldDataset = GetDataset(dataLayer);
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
                        RepairWithDataset(dataLayer, oldDataset, solution.NewDataset.Value, alert);
                    }
                    else
                    {
                        selector.LayerName = layerName;
                        //selector.GisDataset = oldDataset;
                        selector.Solution = solution;
                        selector.ShowDialog(new WindowWrapper(new IntPtr(ArcMap.Application.hWnd)));
                        if (selector.UseLayerFile)
                        {
                            RepairWithLayerFile(mapIndex, dataLayer, selector.LayerFile, selector.KeepBrokenLayer, alert);
                            if (selector.KeepBrokenLayer)
                            {
                                intentionallyBroken += 1;
                            }
                        }
                        else if (selector.UseDataset && selector.Dataset.HasValue)
                        {
                            RepairWithDataset(dataLayer, oldDataset, selector.Dataset.Value, alert);
                        }
                        else
                        {
                            intentionallyBroken += 1;
                        }
                    }
                }
            }

            // Refresh TOC
            ArcMap.Document.UpdateContents(); //update the TOC
            ArcMap.Document.ActivatedView.Refresh(); // refresh the view

            // Print a Summary
            brokenDataSources = GetBrokenDataSources();
            // Some unfixable layers may actually have been corrected by fixing the Mosaic Dataset Layer
            // Limit unfixable to no more than the actual number of broken layers
            unFixableLayers = Math.Min(unFixableLayers, brokenDataSources.Count);
            if (autoFixesApplied > 0 || unFixableLayers > 0 || brokenDataSources.Count > intentionallyBroken)
            {
                string msg = "";
                if (autoFixesApplied > 0) {
                    msg +=
                        $"{autoFixesApplied} broken layers were automatically fixed based on the new locations of known data sources. " +
                        "Close the document without saving if this is not what you want.";
                }
                if (autoFixesApplied > 0 && (unFixableLayers > 0 || brokenDataSources.Count > 0)) {
                    msg += "\n\n";
                }
                if (unFixableLayers > 0) {
                    msg +=
                        $"{unFixableLayers} broken layers could not be fixed; breakage is not due to changes on the PDS (X drive).";
                }
                if (unFixableLayers < brokenDataSources.Count - intentionallyBroken) {
                    // We know that brokenDataSources.Count must be >= unFixableLayers, therefore some of the fixes need fixing
                    if (unFixableLayers > 0) {
                        msg += "\n\n";
                    }
                    msg += "Additional fixes are possible and needed.  Please save, close and reopen your map.";
                }
                alert.Text = @"Map Fixer Summary";
                alert.msgBox.Text = msg;
                alert.ShowDialog(new WindowWrapper(new IntPtr(ArcMap.Application.hWnd)));
            }
        }

        private void RepairWithLayerFile(int mapIndex, IDataLayer2 dataLayer, string newLayerFile, bool keepBrokenLayer, AlertForm alert)
        {
            // Add Layer File to ActiveView Snippet: (http://help.arcgis.com/en/sdk/10.0/arcobjects_net/componenthelp/index.html#//004900000050000000)
            IGxLayer gxLayer = new GxLayer();
            IGxFile gxFile = (IGxFile)gxLayer;
            gxFile.Path = newLayerFile;
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
                alert.Text = @"Error";
                alert.msgBox.Text = $"The layer file '{newLayerFile}' could not be opened.";
                alert.ShowDialog(new WindowWrapper(new IntPtr(ArcMap.Application.hWnd)));
            }
        }

        private void RepairWithDataset(IDataLayer2 dataLayer, Moves.GisDataset oldDataset, Moves.GisDataset newDataset, AlertForm alert)
        {
            // This routine, can only repair workspace path, and dataset name.
            // The workspace type and data type must be the same.
            // This can be checked with the CSV verifier. Violations will be ignored in the CSV loader
            if (oldDataset.DatasourceType != newDataset.DatasourceType ||
                oldDataset.WorkspaceProgId != newDataset.WorkspaceProgId)
            {
                return;
            }
            var helper = (IDataSourceHelperLayer)new DataSourceHelper();
            if (oldDataset.DatasourceName == newDataset.DatasourceName)
            {
                helper.FindAndReplaceWorkspaceNamePath((ILayer)dataLayer, oldDataset.Workspace.Folder, newDataset.Workspace.Folder, false);
            }
            else
            {
                // I can't find a way to simply change the name of the dataset in a layer.
                // To set the data source of a layer I need to first open the data source (using the newDataset properties)
                // I can then use the Name (as IName) of the data source to fix the layer.
                IDataset dataset = TryOpenDataset(newDataset);
                if (dataset == null)
                {
                    alert.Text = @"Error";
                    alert.msgBox.Text = $"Map Fixer is unable to repair the layer {((ILayer2)dataLayer).Name}. " +
                                        "Use the 'Set Data Source button' on the Source tab of the layer properties dialog to " + 
                                        $"set the data source to {newDataset.Workspace.Folder}\\{newDataset.DatasourceName}";
                    alert.ShowDialog(new WindowWrapper(new IntPtr(ArcMap.Application.hWnd)));
                    return;
                }
                helper.ReplaceName((ILayer)dataLayer, dataset.FullName, false);
            }
        }

        private Dictionary<int,List<IDataLayer2>> GetBrokenDataSources()
        {
            var brokenDataSources = new Dictionary<int, List<IDataLayer2>>();
            IMaps maps = ArcMap.Document.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                IMap map = maps.Item[i];
                // ReSharper disable once RedundantArgumentDefaultValue
                IEnumLayer layerEnumerator = map.Layers[null];
                ILayer layer;
                while((layer = layerEnumerator.Next()) != null)
                {
                    if (layer is ILayer2 layer2)
                    {
                        IDataLayer2 dataLayer = null;
                        if (!layer2.Valid)
                        {
                            dataLayer = layer2 as IDataLayer2;
                        }
                        // An IMosaicLayer (raster mosaic dataset) will report valid when it is not; need to check the sub layers.
                        // Repairing the sub layers is insufficient.  The IMosaicLayer must be repaired (this will repair the sub layers as well)
                        // NOTE: It would be nice to remove the sub-layers (they will report invalid),
                        //       1) they do not need to be fixed if the parent layer is fixed (must do)
                        //       2) they will report as unfixable after the parent layer is fixed because the new, correct path isn't in the moves db 
                        else if (layer is IMosaicLayer)
                        {
                            var groupLayer = layer as ICompositeLayer;
                            var groupCount = groupLayer?.Count ?? 0;
                            for (int j = 0; j < groupCount; j++)
                            {
                                // Checking the Valid property on a Mosaic sublayer as ILayer will crash; Casting to ILayer2 is works.  Why????
                                // The raster sub layer does not implement ILayer2.  That is ok, since the boundary and footprint will indicate validity
                                if (groupLayer?.Layer[j] is ILayer2 subLayer)
                                {
                                    if (!subLayer.Valid)
                                    {
                                        dataLayer = layer as IDataLayer2;
                                        break;
                                    }
                                }
                            }
                        }
                        if (dataLayer != null)
                        {
                            if (!brokenDataSources.ContainsKey(i))
                            {
                                brokenDataSources[i] = new List<IDataLayer2>();
                            }
                            brokenDataSources[i].Add(dataLayer);
                        }
                    }
                }
            }
            return brokenDataSources;
        }

        private Moves.GisDataset GetDataset(IDataLayer2 dataLayer)
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

        private IDataset TryOpenDataset(Moves.GisDataset dataset)
        {
            IWorkspaceName workspaceName = new WorkspaceNameClass()
            {
                WorkspaceFactoryProgID = dataset.WorkspaceProgId, // i.e. "esriDataSourcesGDB.AccessWorkspaceFactory";
                PathName = dataset.Workspace.Folder
            };
            IWorkspace workspace;
            try
            {
                workspace = workspaceName.WorkspaceFactory.Open(null, 0);
            }
            catch (Exception)
            {
                // This may fail for any number of reasons, bad input (progID or path), network or filesystem error permissions, ...
                return null;
            }
            if (workspace == null)
                return null;
            if (dataset.DatasourceType.AsDatasetType() == null)
                return null;
            var datasetNames = workspace.DatasetNames[dataset.DatasourceType.AsDatasetType().Value];
            IDatasetName datasetName;
            while ((datasetName = datasetNames.Next()) != null)
            {
                if (datasetName.ToString() == dataset.DatasourceType && string.Compare(datasetName.Name, dataset.DatasourceName, StringComparison.OrdinalIgnoreCase) == 0)
                    return TryOpenDataset(dataset, workspace, datasetName);
            }
            return null;
        }

        private IDataset TryOpenDataset(Moves.GisDataset dataset, IWorkspace workspace, IDatasetName datasetName)
        {
            if (dataset.DatasourceType.AsDatasetType() == null)
                return null;
            var datasetType = dataset.DatasourceType.AsDatasetType().Value;
            if (datasetType == esriDatasetType.esriDTFeatureClass)
            {
                try
                {
                    IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspace;
                    IFeatureClass featureClass = featureWorkspace.OpenFeatureClass(datasetName.Name);
                    return (IDataset)featureClass;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            // For opening raster data see https://desktop.arcgis.com/en/arcobjects/10.5/net/webframe.htm#62937a09-b1c5-47d7-a1ac-f7a5daab3c89.htm
            if (datasetType == esriDatasetType.esriDTRasterDataset)
            {
                try
                {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    // Raster Workspace Class is in ESRI.ArcGIS.DataSourcesRaster
                    IRasterWorkspace2 rasterWorkspace = (IRasterWorkspace2)workspace;
                    IRasterDataset rasterDataset = rasterWorkspace.OpenRasterDataset(datasetName.Name);
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    // Three possible co-classes FunctionRasterDataset, RasterBand, RasterDataset are in ESRI.ArcGIS.DataSourcesRaster
                    return (IDataset)rasterDataset;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            if (datasetType == esriDatasetType.esriDTRasterCatalog || datasetType == esriDatasetType.esriDTMosaicDataset)
            {
                try
                {
                    IRasterWorkspaceEx rasterWorkspace = (IRasterWorkspaceEx)workspace;
                    IRasterDataset rasterDataset = rasterWorkspace.OpenRasterDataset(datasetName.Name);
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    // Three possible co-classes FunctionRasterDataset, RasterBand, RasterDataset are in ESRI.ArcGIS.DataSourcesRaster
                    return (IDataset)rasterDataset;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            //TODO: Open additional types of data sources, support at least all in theme Manager

            return null;
        }
    }

    public static class StringExtensions
    {
        public static esriDatasetType? AsDatasetType(this string source)
        {
            if (esriDatasetType.TryParse(source, out esriDatasetType tmp))
            {
                return tmp;
            }
            return null;
        }
    }

}
