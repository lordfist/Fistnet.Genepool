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
        private readonly byte foodRegrowthPerAge = AGE_FOOD_INCREASE;

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

        // Uptake is a single source-cell transaction, before any movement. TakenFood
        // is an observation of this season's transfer, never a deferred debit.
        public int GatherFood(Organism recipient, int requested)
        {
            if (requested < 0) throw new ArgumentOutOfRangeException(nameof(requested));
            lock (Board.BoardElement)
            {
                if (!ReferenceEquals(this.Occupant, recipient) || recipient == null || recipient.IsDead)
                    return 0;
                int offered = Math.Min(requested, Math.Min(this.FoodRemaining, recipient.FoodCapacity));
                int transferred = recipient.ReceiveFood(offered);
                this.ReduceFood((byte)transferred);
                recipient.SetAvailableFood(this.FoodRemaining);
                DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                if (diagnostics != null)
                {
                    diagnostics.Count("food.debit_requested", requested);
                    diagnostics.Count("food.debit_applied", transferred);
                    diagnostics.Count("food.uptake_transferred", transferred);
                    if (transferred < requested) diagnostics.Count("food.uptake_unavailable", requested - transferred);
                }
                return transferred;
            }
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
                this.IncreaseFood(foodRegrowthPerAge);
                DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                if (diagnostics != null)
                {
                    diagnostics.Count("food.refill_requested", foodRegrowthPerAge);
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
            : this(xPos, yPos, organism, STARTING_FOOD, AGE_FOOD_INCREASE) { }

        public BoardSquare(int xPos, int yPos, Organism organism, byte initialFood, byte regrowthPerAge)
            : this(organism)
        {
            if (initialFood > MAX_FOOD || regrowthPerAge > MAX_FOOD) throw new ArgumentOutOfRangeException(nameof(initialFood));
            this.FoodRemaining = initialFood;
            this.foodRegrowthPerAge = regrowthPerAge;
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

        public BoardSquare GetRandomEmptyNeighbor()
        {
            var available = new List<BoardSquare>(8);
            foreach (TargetTypes direction in Enum.GetValues(typeof(TargetTypes)))
            {
                if (direction == TargetTypes.Self) continue;
                BoardSquare candidate = this.GetNeightbor(direction);
                if (candidate != null && !candidate.IsOccupied) available.Add(candidate);
            }
            return available.Count == 0 ? null : available[Common.GetRandomIntegerSeed(available.Count)];
        }

        public bool TryPlaceChild(Organism otherParent)
        {
            lock (Board.BoardElement)
            {
                Organism parent = this.Occupant;
                DiagnosticSession diagnostics = SimulationDiagnostics.Current;
                diagnostics?.Count("birth.placement_attempted");
                if (parent == null || !parent.CanReproduceWith(otherParent))
                {
                    diagnostics?.Count("birth.placement_ineligible");
                    return false;
                }
                BoardSquare destination = this.GetRandomEmptyNeighbor();
                if (destination == null)
                {
                    diagnostics?.Count("birth.placement_blocked");
                    return false;
                }
                // The board lock secures this empty destination. No child is created
                // and no fertility/cost is charged until this placement can commit.
                Organism child = parent.CommitBirth(otherParent);
                if (child == null) return false;
                destination.AddOccupant(child);
                Board.ObserveBirth(destination.Position.X, destination.Position.Y);
                if (diagnostics != null)
                {
                    diagnostics.Count("birth.placed");
                    int dx = destination.Position.X - this.Position.X;
                    int dy = destination.Position.Y - this.Position.Y;
                    diagnostics.Count("birth.placed." + (TargetTypes)((dy + 1) * 3 + dx + 1));
                    diagnostics.Event("birth.placed", child, destination.Position.X, destination.Position.Y,
                        diagnostics.CanSampleEvent("birth.placed") ? "parentId=" + diagnostics.OrganismId(parent)
                        + ";parentCell=" + this.Position.X + "," + this.Position.Y : null);
                }
                return true;
            }
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

        public bool TryMove(TargetTypes target, ActionDecision decision = null)
        {
            Organism mover = this.Occupant;
            // A request belongs to one turn, even when blocked or without a destination.
            mover?.MoveDone();
            if (decision != null)
            {
                decision.Outcome.Moved = false;
                decision.Outcome.Committed = false;
                decision.Outcome.Status = "movement.blocked";
            }
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
                    targetSquare.AddOccupant(this.Occupant);
                    this.RemoveOccupant();

                    if (decision != null)
                    {
                        decision.Outcome.Moved = true;
                        decision.Outcome.Committed = true;
                        decision.Outcome.Status = "movement.completed";
                        decision.Outcome.DestinationX = targetSquare.Position.X;
                        decision.Outcome.DestinationY = targetSquare.Position.Y;
                    }

                    if (diagnostics != null)
                    {
                        diagnostics.Count("movement.committed");
                        if ((byte)resolvedTarget < committedDirectionCounters.Length)
                            diagnostics.Count(committedDirectionCounters[(byte)resolvedTarget]);
                        diagnostics.Event("movement.committed", mover, this.Position.X, this.Position.Y,
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
