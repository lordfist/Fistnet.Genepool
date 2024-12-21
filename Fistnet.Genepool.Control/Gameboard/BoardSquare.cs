using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.Control.Gameboard
{
    public class BoardSquare
    {
        public const byte STARTING_FOOD = 3;
        public const byte MAX_FOOD = 10;
        public const byte AGE_FOOD_INCREASE = 1;

        #region Properties.

        public Point Position { get; private set; }

        public Organism Occupant { get; private set; }

        public byte FoodRemaining { get; private set; }

        public bool IsOccupied { get { return (this.Occupant != null); } }

        public bool IsNextAge { get { return _nextAge; } }

        #endregion Properties.

        #region Food setup.

        public void SetFoodToMax()
        {
            this.FoodRemaining = MAX_FOOD;
        }

        public void ReduceFood(byte reduction)
        {
            if (this.FoodRemaining > reduction)
                this.FoodRemaining -= reduction;
            else
                this.FoodRemaining = 0;
        }

        public void IncreaseFood(byte increase)
        {
            this.FoodRemaining += increase;

            if (this.FoodRemaining > BoardSquare.MAX_FOOD)
                this.FoodRemaining = BoardSquare.MAX_FOOD;
        }

        #endregion Food setup.

        #region Refresh.

        private int _currentAge;
        private int _currentSeason;
        private bool _nextAge;

        public void Refresh()
        {
            this._currentSeason++;

            if (this._currentSeason == Organism.DNA_SEQUENCE_MAXLENGTH)
            {
                this._currentSeason = 0;
                this._currentAge++;
                this._nextAge = true;
                this.IncreaseFood(BoardSquare.AGE_FOOD_INCREASE);
            }
            else
            {
                this._nextAge = false;
            }
        }

        #endregion Refresh.

        #region Add/Remove occupant.

        public void AddOccupant(Organism organism)
        {
            if (!this.IsOccupied)
                this.Occupant = organism;
            else
                throw new AccessViolationException("Cannot add occupant. Already occupied.");
        }

        public void RemoveOccupant()
        {
            this.Occupant = null;
        }

        #endregion Add/Remove occupant.

        #region Constructor.

        private BoardSquare(Organism organism)
        {
            this._currentAge = 0;
            this._currentSeason = 0;
            this._nextAge = false;
            this.Occupant = organism;
        }

        public BoardSquare(int xPos, int yPos, Organism organism)
            : this(organism)
        {
            this.FoodRemaining = BoardSquare.STARTING_FOOD;
            this.Position = new Point(xPos, yPos);
        }

        public BoardSquare(Point position, Organism organism)
            : this(organism)
        {
            this.FoodRemaining = BoardSquare.STARTING_FOOD;
            this.Position = position;
        }

        #endregion Constructor.

        #region Get neighbor.

        public BoardSquare GetNeightbor(TargetTypes target)
        {
            int xPos = this.Position.X;
            int yPos = this.Position.Y;

            switch (target)
            {
                case TargetTypes.TopLeft:
                    xPos = xPos - 1;
                    yPos = yPos - 1;
                    break;
                case TargetTypes.TopCenter:
                    yPos = yPos - 1;
                    break;
                case TargetTypes.TopRight:
                    xPos = xPos + 1;
                    yPos = yPos - 1;
                    break;
                case TargetTypes.MiddleLeft:
                    xPos = xPos - 1;
                    break;
                case TargetTypes.Self:
                    break;
                case TargetTypes.MiddleRight:
                    xPos = xPos + 1;
                    break;
                case TargetTypes.BottomLeft:
                    xPos = xPos - 1;
                    yPos = yPos + 1;
                    break;
                case TargetTypes.BottomCenter:
                    yPos = yPos + 1;
                    break;
                case TargetTypes.BottomRight:
                    xPos = xPos + 1;
                    yPos = yPos + 1;
                    break;
            }

            if (xPos >= Board.BOARD_SIZE
                || yPos >= Board.BOARD_SIZE
                || xPos < 0
                || yPos < 0)
                return null;
            else
                return Board.BoardElement[xPos, yPos];
        }

        public BoardSquare GetFirstEmptySquare()
        {
            foreach (TargetTypes target in Enum.GetValues(typeof(TargetTypes)))
            {
                BoardSquare freeSquare = this.GetNeightbor(target);

                if (freeSquare != null && !freeSquare.IsOccupied)
                    return freeSquare;
            }

            return null;
        }

        #endregion Get neighbor.

        #region Move.

        public bool TryMove(TargetTypes target)
        {
            BoardSquare targetSquare = this.GetNeightbor(target);

            if (targetSquare == null)
                targetSquare = this.GetNeightbor(Common.GetOppositeTarget(target));

            if (targetSquare == null)
                return false;

            lock (this)
            {
                if (targetSquare.IsOccupied)
                    return false;
                else
                {
                    targetSquare.AddOccupant(this.Occupant);
                    this.Occupant.MoveDone();
                    this.RemoveOccupant();

                    return true;
                }
            }
        }

        #endregion Move.
    }
}