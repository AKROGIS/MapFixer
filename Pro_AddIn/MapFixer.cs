using System;
using System.Collections.Generic;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
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
        public static void FixMap(Map map, Moves moves)
        {
            MessageBox.Show("Implement Fix Map", "MapFixer");
        }
    }
}