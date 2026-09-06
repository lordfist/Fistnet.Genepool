using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.App
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            gameVisualizer = new GameboardBitmap(BoardVisualizer.ClientSize.Width);
            PopulationGraph.History = populationHistory;
            Board.SeasonCompleted += RecordCompletedSeason;
        }

        private GameboardBitmap gameVisualizer;
        private readonly PopulationHistory populationHistory = new PopulationHistory();
        private bool resetRequested;
        private bool closeRequested;
        private void RecordCompletedSeason(int season, int population) => populationHistory.Add(season, population);

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (BoardWorker.IsBusy || resetRequested) return;
            Board.InitalizeBoard();
            BoardRefreshTimer.Interval = 100;
            BoardRefreshTimer.Enabled = true;
            AgeRunCheck.Enabled = false;
            StartButton.Enabled = false;
            StopButton.Enabled = true;
        }

        private void ShowStatistics()
        {
            this.StatisticsLabel.Text = "Global statistics:\r\n-------------------------------\r\n\r\n";
            this.StatisticsLabel.Text += "Board age: " + Board.Age.ToString() + " \r\n";
            this.StatisticsLabel.Text += "Board season: " + Board.Season.ToString() + " \r\n";
            this.StatisticsLabel.Text += "Cell number: " + Board.BoardOrganismCount.ToString() + " \r\n";
            this.StatisticsLabel.Text += "Oldest sequence age: " + Board.LongestLiving.ToString() + "\r\n  (may be inherited)\r\n";
            this.StatisticsLabel.Text += "DNA usage: \r\n";
            foreach (DnaTypes item in Board.DnaUsageStatistics.Keys)
            {
                this.StatisticsLabel.Text += "  " + Enum.GetName(typeof(DnaTypes), item) + " - " + Board.DnaUsageStatistics[item].ToString() + "\r\n";
            }
        }

        private void ShowComplexStatistics()
        {
            this.TopRatedLabel.Text = "Most common action patterns: \r\n-------------------------------\r\n\r\n";
            var orderedData = Board.OrganismUsageStatistics.ToArray().OrderBy(pair => pair.Value).Reverse();
            string topRatedData = "";
            int topCount = Math.Max(1, (StartButton.Top - TopRatedLabel.Top) / TopRatedLabel.Font.Height - 4);

            foreach (var item in orderedData)
            {
                topCount--;
                topRatedData += item.Value.ToString() + " - " + item.Key + "\r\n";
                if (topCount <= 0)
                    break;
            }

            this.TopRatedLabel.Text += topRatedData;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (BoardRefreshTimer.Enabled && !BoardWorker.IsBusy && !resetRequested)
            {
                BoardWorker.RunWorkerAsync(AgeRunCheck.Checked);
            }
        }

        private void BoardWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if ((bool)e.Argument) Board.ExecuteOneAge();
            else Board.ExecuteSingleSeason(true);
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            BoardRefreshTimer.Enabled = false;
            StopButton.Enabled = false;
            StartButton.Enabled = !BoardWorker.IsBusy && !resetRequested;
            AgeRunCheck.Enabled = !BoardWorker.IsBusy;
        }

        private void BoardWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (closeRequested) { Close(); return; }
            if (resetRequested) { CompleteReset(); return; }
            if (e.Error != null)
            {
                StopButton_Click(sender, EventArgs.Empty);
                MessageBox.Show(this, e.Error.Message, "Simulation stopped");
                return;
            }
            // Painting and control access stay on the UI thread.
            gameVisualizer.RefreshAndResize();
            this.BoardVisualizer.Image = gameVisualizer.Picture;
            this.BoardVisualizer.Refresh();
            ShowStatistics();
            PopulationGraph.Invalidate();
            if (!BoardRefreshTimer.Enabled) { StartButton.Enabled = true; AgeRunCheck.Enabled = true; }
        }

        private void BoardVisualizer_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                BoardSquare square = gameVisualizer.GetSquareFromLocation(e.Location);

                if (square == null)
                    return;

                lock (square)
                {
                    BoardItemLabel.Text = "Board square: \r\n ------------------- \r\n";
                    BoardItemLabel.Text += "Board position: " + square.Position.X.ToString() + ", " + square.Position.Y.ToString() + "\r\n";
                    BoardItemLabel.Text += "Food: " + square.FoodRemaining.ToString() + "\r\n";
                    BoardItemLabel.Text += "Occupied: " + square.IsOccupied.ToString() + "\r\n";
                    if (square.IsOccupied)
                    {
                        BoardItemLabel.Text += "Ocuppant dna code: " + square.Occupant.DnaCode.ToString() + "\r\n";
                        BoardItemLabel.Text += "Sequence age: " + square.Occupant.SequenceAge.ToString() + "\r\n  (may be inherited)\r\n";
                        BoardItemLabel.Text += "Ocuppant cell age: " + square.Occupant.Age.ToString() + "\r\n";
                        BoardItemLabel.Text += "Ocuppant health: " + square.Occupant.Health.ToString() + "\r\n";
                        BoardItemLabel.Text += "Ocuppant food: " + square.Occupant.FoodBalance.ToString() + "\r\n";
                        BoardItemLabel.Text += "Ocuppant has child: " + square.Occupant.HasChild.ToString() + "\r\n";

                        BoardItemLabel.Text += "Occupant dna sequence: \r\n";

                        for (int i = 0; i < Organism.DNA_SEQUENCE_MAXLENGTH; i++)
                        {
                            BoardItemLabel.Text += "  - " + Enum.GetName(typeof(DnaTypes), square.Occupant.DnaSequence[i].DnaType) + "\r\n";
                        }

                        BoardItemLabel.Text += "\r\n";
                    }
                }
            }
        }

        private void ButtonComplexStats_Click(object sender, EventArgs e)
        {
            ShowComplexStatistics();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StopButton_Click(sender, e);
            resetRequested = true;
            StartButton.Enabled = false;
            ResetButton.Enabled = false;
            if (!BoardWorker.IsBusy) CompleteReset();
        }

        private void CompleteReset()
        {
            populationHistory.Reset();
            Board.InitalizeBoard(true);
            gameVisualizer.RefreshAndResize();
            BoardVisualizer.Image = gameVisualizer.Picture;
            BoardVisualizer.Refresh();
            PopulationGraph.Invalidate();
            ShowStatistics();
            TopRatedLabel.Text = "Most common action patterns:";
            BoardItemLabel.Text = "Board item:";
            resetRequested = false;
            AgeRunCheck.Enabled = true;
            StartButton.Enabled = true;
            ResetButton.Enabled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopButton_Click(this, EventArgs.Empty);
            if (BoardWorker.IsBusy) { closeRequested = true; e.Cancel = true; }
            base.OnFormClosing(e);
        }

        private void ReleaseSimulationResources()
        {
            Board.SeasonCompleted -= RecordCompletedSeason;
            BoardVisualizer.Image = null;
            gameVisualizer.Dispose();
        }
    }
}
