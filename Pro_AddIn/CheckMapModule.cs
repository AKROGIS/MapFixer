using ArcGIS.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using MovesDatabase;
using System.Linq;
using System.Threading.Tasks;

namespace MapFixer
{
    internal class CheckMapModule : Module
    {
        private static CheckMapModule _this = null;
        private static readonly string _movesPath = @"X:\GIS\ThemeMgr\DataMoves.csv";
        private static Moves _moves = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static CheckMapModule Current
        {
            get
            {
                return _this ?? (_this = (CheckMapModule)FrameworkApplication.FindModule("MapFixer_CheckMapModule"));
            }
        }

        public CheckMapModule()
        {
            SubscribeToEvents();
        }

        #region Events

        // The MapViewInitializedEvent event is called whenever a map pane is opened.
        // Even if the map is a new default map;  It is not called when switching between open map panes.

        private SubscriptionToken _mapOpenEvent = null;

        private void SubscribeToEvents()
        {
            if (_mapOpenEvent == null)
            {
                _mapOpenEvent = MapViewInitializedEvent.Subscribe(CheckMap);
            }

            //MessageBox.Show("Subscribed To Events", "MapFixer");
        }

        private void UnsubscribeToEvents()
        {
            MapViewInitializedEvent.Unsubscribe(_mapOpenEvent);
            _mapOpenEvent = null;

            //MessageBox.Show("Unubscribed To Events", "MapFixer");
        }

        #endregion

        internal async void CheckMap(MapViewEventArgs eventArgs)
        {
            await CheckMapAsync(eventArgs.MapView.Map);
        }

        internal async Task CheckMapAsync(Map map)
        {
            if (map.HasBrokenLayers()) {
                if (_moves == null)
                {
                    await LoadMovesAsync();
                }
                await QueuedTask.Run(() =>
                {
                    MapFixer.FixMap(map, _moves);
                });
            }
        }

        private async Task LoadMovesAsync()
        {
            await Task.Run(() =>
            {
                _moves = new Moves(_movesPath);
                // MessageBox.Show("Loaded Moves", "MapFixer");
            });
        }

        #region Overrides

        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            UnsubscribeToEvents();
            return true;
        }

        #endregion Overrides

    }

    public static class MapExtensions
    {
        public static bool HasBrokenLayers(this Map map)
        {
            return map.GetLayersAsFlattenedList().Any(l => l.ConnectionStatus == ConnectionStatus.Broken);
        }
    }

}
