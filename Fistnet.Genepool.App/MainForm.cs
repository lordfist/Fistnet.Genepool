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
        }

        private GameboardBitmap gameVisualizer = new GameboardBitmap(600);

        private void StartButton_Click(object sender, EventArgs e)
        {
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
            this.StatisticsLabel.Text += "Longest living cell: " + Board.LongestLiving.ToString() + " \r\n";
            this.StatisticsLabel.Text += "DNA usage: \r\n";
            foreach (DnaTypes item in Board.DnaUsageStatistics.Keys)
            {
                this.StatisticsLabel.Text += "  " + Enum.GetName(typeof(DnaTypes), item) + " - " + Board.DnaUsageStatistics[item].ToString() + "\r\n";
            }
        }

        private void ShowComplexStatistics()
        {
            this.TopRatedLabel.Text = "Top rated DNA: \r\n-------------------------------\r\n\r\n";
            var orderedData = Board.OrganismUsageStatistics.ToArray().OrderBy(pair => pair.Value).Reverse();
            string topRatedData = "";
            int topCount = 40;

            foreach (var item in orderedData)
            {
                topCount--;
                topRatedData += item.Value.ToString() + " - " + item.Key + "\r\n";
                if (topCount <= 0)
                    break;
            }

            this.TopRatedLabel.Text += topRatedData;
        }

        private int _gcCollection = 0;

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!BoardWorker.IsBusy)
            {
                this.ShowStatistics();
                BoardWorker.RunWorkerAsync();

                _gcCollection++;
                if (_gcCollection >= 30)
                {
                    _gcCollection = 0;
                    GC.Collect();
                }
            }
        }

        private void BoardWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (AgeRunCheck.Checked)
                    Board.ExecuteOneAge();
                else
                    Board.ExecuteSingleSeason(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }

            gameVisualizer.RefreshAndResize();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            BoardRefreshTimer.Enabled = false;
            StopButton.Enabled = false;
            StartButton.Enabled = true;
        }

        private void BoardWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.BoardVisualizer.Image = gameVisualizer.Picture;
            this.BoardVisualizer.Refresh();
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
                        BoardItemLabel.Text += "Ocuppant sequence age: " + square.Occupant.SequenceAge.ToString() + "\r\n";
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
            BoardRefreshTimer.Interval = 100;
            BoardRefreshTimer.Enabled = false;

            Task.Factory.StartNew(() =>
            {
                while (BoardWorker.IsBusy)
                {
                    Thread.Sleep(100);
                }
                Board.InitalizeBoard(true);
                gameVisualizer.RefreshAndResize();
            });

            this.BoardVisualizer.Image = null;
            this.BoardVisualizer.Refresh();
            GC.Collect();

            AgeRunCheck.Enabled = true;
            StartButton.Enabled = true;
        }
    }
}