﻿using System.Windows.Forms;
using MovesDatabase;

namespace MapFixer
{
    public partial class SelectionForm : Form
    {
        private Moves.Solution _solution;

        public SelectionForm()
        {
            InitializeComponent();
        }

        public string LayerName { private get; set; }

        //public Moves.GisDataset GisDataset { get; set; }
        public Moves.Solution Solution
        {
            private get { return _solution; }
            set
            {
                _solution = value;
                SetupForm();
            }
        }

        // If true, the user would like to fix the path/name to the dataset in the broken layer
        public bool UseDataset => radioButton4.Checked;

        // The new path/name of the dataset
        // based on the user's selection of the datasets in the solution
        // Currently I am only allowing the user to choose the NewDataset; assuming the ReplacementDataset is null 
        public Moves.GisDataset? Dataset => Solution.NewDataset;

        // if true, the user would like to add a layer file to the map
        public bool UseLayerFile => radioButton2.Checked || radioButton3.Checked;

        // The layer file the user would like to use as a replacement
        // Currently the solution only supports on layer file.
        // In the future, we might support multiple layer files
        public string LayerFile => Solution.ReplacementLayerFilePath;

        // Does the user want to keep or remove the broken layer?
        // only applicable if the user is adding a new layer file.
        public bool KeepBrokenLayer => radioButton3.Checked;

        private void SetupForm()
        {
            Size = MinimumSize;
            if (Solution.ReplacementLayerFilePath != null)
            {
                radioButton2.Checked = true;
            }
            else if (Solution.NewDataset != null)
            {
                radioButton4.Checked = true;
            }
            else
            {
                radioButton1.Checked = true;
            }

            radioButton1.Enabled = true;
            radioButton2.Enabled = true;
            radioButton3.Enabled = true;
            radioButton4.Enabled = true;
            radioButton4.Visible = true;
            radioButton2.Text = "Replace with the new layer file";
            radioButton4.Text = "Fix the path/name of the data set";
            var dataLocation = "\nThe data has been moved and/or renamed.";
            if (Solution.NewDataset == null)
            {
                radioButton4.Enabled = false;
                radioButton4.Visible = false;
                dataLocation = "\nThe data has been deleted.";
            }
            else
            {
                var workspace = Solution.NewDataset.Value.Workspace;
                if (workspace.IsInArchive)
                {
                    radioButton4.Text = "Use the archived data set";
                    dataLocation = "\nThe data has been archived.";
                }
                if (workspace.IsInTrash)
                {
                    radioButton4.Text = "Use the data set in the trash";
                    dataLocation = "\nThe data has been moved to the trash.";
                }
            }
            
            if (Solution.ReplacementLayerFilePath == null)
            {
                radioButton2.Enabled = false;
                radioButton3.Enabled = false;
            }
            else
            {
                radioButton2.Text += " (recommended)";
                radioButton4.Text += " (not recommended)";
            }


            msgBox.Text = $"The layer '{LayerName}' is broken.";
            msgBox.Text += dataLocation;
            //msgBox.Text += $"\nSolution: {GisDataset.Workspace.Folder}/{GisDataset.DatasourceName} -> {Solution.NewDataset?.Workspace.Folder}/{Solution.NewDataset?.DatasourceName}";
            var optionalNot = Solution.ReplacementLayerFilePath == null ? " not " : " ";
            msgBox.Text += $"\nA replacement theme (layer file) is{optionalNot}available.";
            if (Solution.Remarks != null)
            {
                msgBox.Text += "\n\nNOTE: " + Solution.Remarks;
                Size = new System.Drawing.Size(Size.Width, Size.Height + 25);
            }
            msgBox.Text += "\n\nHow would you like to fix this layer?";
        }

        //TODO: Add help to assist the user in making the choice.

    }
}
