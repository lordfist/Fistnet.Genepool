using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Visualization
{
    public class GameboardBitmap
    {
        #region Public properties.

        public Bitmap Picture { get; private set; }

        #endregion Public properties.

        #region Constructor.

        public GameboardBitmap(int pictureSize)
        {
            this.actualPicture = new Bitmap(Board.BOARD_SIZE, Board.BOARD_SIZE, PixelFormat.Format32bppArgb);
            this.Picture = new Bitmap(pictureSize, pictureSize, PixelFormat.Format32bppArgb);
        }

        #endregion Constructor.

        #region Display image.

        public void RefreshAndResize()
        {
            this.ProcessBoard();
            using (Graphics g = Graphics.FromImage(this.Picture))
            {
                g.Clear(Color.Black);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(this.actualPicture, new Rectangle(0, 0, this.Picture.Width, this.Picture.Height));
            }
        }

        #endregion Display image.

        #region Image processing.

        private Bitmap actualPicture;

        private void ProcessBoard()
        {
            unsafe
            {
                BitmapData bitmapData = this.actualPicture.LockBits(new Rectangle(0, 0, this.actualPicture.Width, this.actualPicture.Height), ImageLockMode.ReadWrite, this.Picture.PixelFormat);

                int BytesPerPixel = System.Drawing.Bitmap.GetPixelFormatSize(this.Picture.PixelFormat) / 8;
                int HeightInPixels = bitmapData.Height;
                int WidthInBytes = bitmapData.Width * BytesPerPixel;
                byte* PtrFirstPixel = (byte*)bitmapData.Scan0;

                Parallel.For(0, HeightInPixels, y =>
                {
                    int realX = 0;

                    byte* CurrentLine = PtrFirstPixel + (y * bitmapData.Stride);
                    for (int x = 0; x < WidthInBytes; x = x + BytesPerPixel)
                    {
                        CurrentLine[x] = (byte)(0);         // blue
                        CurrentLine[x + 1] = (byte)(0);     // green
                        CurrentLine[x + 2] = (byte)(0);     // red
                        CurrentLine[x + 3] = (byte)(0);     // alpha

                        if (Board.BoardElement[realX, y].IsOccupied)
                        {
                            Color color = Common.GetOrganismColors(Board.BoardElement[realX, y].Occupant)[0];

                            CurrentLine[x] = (byte)(color.B);
                            CurrentLine[x + 1] = (byte)(color.G);
                            CurrentLine[x + 2] = (byte)(color.R);
                            CurrentLine[x + 3] = (byte)(255);
                        }

                        realX++;
                    }
                });
                this.actualPicture.UnlockBits(bitmapData);
            }
        }

        #endregion Image processing.

        #region Public methods.

        public BoardSquare GetSquareFromLocation(Point location)
        {
            double ratioX = (double)this.Picture.Width / Board.BOARD_SIZE;
            double ratioY = (double)this.Picture.Height / Board.BOARD_SIZE;

            int actualLocationX = (int)Math.Ceiling(location.X / ratioX) - 1;
            int actualLocationY = (int)Math.Ceiling(location.Y / ratioY) - 1;

            return Board.BoardElement[actualLocationX, actualLocationY];
        }

        #endregion Public methods.
    }
}