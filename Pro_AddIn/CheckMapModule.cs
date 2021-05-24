using ArcGIS.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System.Threading.Tasks;

namespace MapFixer
{
    internal class CheckMapModule : Module
    {
        private static CheckMapModule _this = null;
        private static string _moves = null; //TODO Implement

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
            await CheckMap(eventArgs.MapView.Map);
        }

        internal async Task CheckMap(Map map)
        {
            await LoadMoves();
            MessageBox.Show("Implement CheckMap", "MapFixer");
        }

        private async Task LoadMoves()
        {
            if (_moves == null)
            {
                await Task.Run(() =>
                {
                    _moves = "move"; //TODO Implement
                    MessageBox.Show("Loaded Moves", "MapFixer");
                });
            }
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
}
