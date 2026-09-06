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
            this.FoodRemaining = (byte)Math.Min(BoardSquare.MAX_FOOD, (int)this.FoodRemaining + increase);
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
                byte foodBefore = this.FoodRemaining;
                this.IncreaseFood(BoardSquare.AGE_FOOD_INCREASE);
                DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                if (diagnostics != null)
                {
                    diagnostics.Count("food.refill_requested", BoardSquare.AGE_FOOD_INCREASE);
                    diagnostics.Count("food.refill_applied", this.FoodRemaining - foodBefore);
                }
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

        private static readonly string[] requestedDirectionCounters =
            { "movement.requested.TopLeft", "movement.requested.TopCenter", "movement.requested.TopRight",
              "movement.requested.MiddleLeft", "movement.requested.Self", "movement.requested.MiddleRight",
              "movement.requested.BottomLeft", "movement.requested.BottomCenter", "movement.requested.BottomRight" };
        private static readonly string[] committedDirectionCounters =
            { "movement.committed.TopLeft", "movement.committed.TopCenter", "movement.committed.TopRight",
              "movement.committed.MiddleLeft", "movement.committed.Self", "movement.committed.MiddleRight",
              "movement.committed.BottomLeft", "movement.committed.BottomCenter", "movement.committed.BottomRight" };

        public bool TryMove(TargetTypes target)
        {
            DiagnosticSession diagnostics = SimulationDiagnostics.Current;
            if (diagnostics != null)
            {
                diagnostics.Count("movement.attempted");
                if ((byte)target < requestedDirectionCounters.Length)
                    diagnostics.Count(requestedDirectionCounters[(byte)target]);
            }
            TargetTypes resolvedTarget = target;
            BoardSquare targetSquare = this.GetNeightbor(target);

            if (targetSquare == null)
            {
                resolvedTarget = Common.GetOppositeTarget(target);
                targetSquare = this.GetNeightbor(resolvedTarget);
                diagnostics?.Count("movement.edge_fallback");
            }

            if (targetSquare == null)
            {
                diagnostics?.Count("movement.blocked.no_destination");
                if (diagnostics != null)
                    diagnostics.Event("movement.blocked", this.Occupant, this.Position.X, this.Position.Y,
                        diagnostics.CanSampleEvents ? "reason=no_destination;requested=" + target + ";resolved=" + resolvedTarget : null);
                return false;
            }

            lock (Board.BoardElement)
            {
                if (!this.IsOccupied || targetSquare.IsOccupied)
                {
                    diagnostics?.Count(this.IsOccupied ? "movement.blocked.occupied" : "movement.blocked.empty_source");
                    if (diagnostics != null)
                        diagnostics.Event("movement.blocked", this.Occupant, this.Position.X, this.Position.Y,
                            diagnostics.CanSampleEvents ? "requested=" + target + ";resolved=" + resolvedTarget + ";destination="
                            + targetSquare.Position.X + "," + targetSquare.Position.Y : null);
                    return false;
                }
                else
                {
                    Organism observedMover = this.Occupant;
                    targetSquare.AddOccupant(this.Occupant);
                    this.Occupant.MoveDone();
                    this.RemoveOccupant();

                    if (diagnostics != null)
                    {
                        diagnostics.Count("movement.committed");
                        if ((byte)resolvedTarget < committedDirectionCounters.Length)
                            diagnostics.Count(committedDirectionCounters[(byte)resolvedTarget]);
                        diagnostics.Event("movement.committed", observedMover, this.Position.X, this.Position.Y,
                            diagnostics.CanSampleEvents ? "requested=" + target + ";resolved=" + resolvedTarget + ";destination="
                            + targetSquare.Position.X + "," + targetSquare.Position.Y : null);
                    }

                    return true;
                }
            }
        }

        #endregion Move.
    }
}
