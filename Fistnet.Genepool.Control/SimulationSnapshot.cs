using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Dna.Effects;

namespace Fistnet.Genepool.Control
{
    /// <summary>
    /// Execution-state comparison for a quiescent reference run, not a save/load format.
    /// Covers board clocks, statistics, RNG and options, and the complete reachable domain
    /// object graph, including private/base fields, children, old targets and pending effects.
    /// Excludes runtime locks, production RNG machinery and display resources. Call between
    /// steps with no concurrent writers. Unsupported state fails instead of being omitted.
    /// </summary>
    public sealed class SimulationSnapshot
    {
        public string Json { get; }
        public string Sha256 { get; }

        private SimulationSnapshot(string json)
        {
            Json = json;
            Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }

        public static SimulationSnapshot Capture()
        {
            if (Board.RunOptions == null || Board.RunOptions.Mode != SimulationMode.DeterministicReference)
                throw new InvalidOperationException("Full-state export requires deterministic reference mode.");
            if (Common.RandomSource == null)
                throw new InvalidOperationException("Reference random source is missing.");

            string randomState = Common.RandomSource.State;
            string randomAlgorithm = Common.RandomSource.Algorithm;
            long draws = Common.RandomSource.DrawCount;
            int season = Board.Season;
            int rule = RuleManager.CurrentRuleIndex;
            Exporter exporter = new Exporter();
            var root = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["schema"] = "genepool-reference-state-v2",
                ["organismIdentityCounter"] = Common.OrganismIdentityCounter,
                ["initialized"] = Board.IsInitialized,
                ["season"] = season,
                ["ruleIndex"] = rule,
                ["options"] = exporter.Encode(Board.RunOptions),
                ["random"] = new { algorithm = randomAlgorithm, state = randomState, drawCount = draws },
                ["population"] = Board.BoardOrganismCount,
                ["longestSequenceAge"] = Board.LongestLiving,
                ["dnaStatistics"] = exporter.Encode(Board.DnaUsageStatistics),
                ["patternStatistics"] = exporter.Encode(Board.OrganismUsageStatistics)
            };
            var cells = new List<object>(Board.BOARD_SIZE * Board.BOARD_SIZE);
            for (int x = 0; x < Board.BOARD_SIZE; x++)
                for (int y = 0; y < Board.BOARD_SIZE; y++)
                    cells.Add(new { x, y, square = exporter.Encode(Board.BoardElement[x, y]) });
            root["cells"] = cells;
            root["objects"] = exporter.Finish();
            string json = JsonSerializer.Serialize(root);
            if (Board.Season != season || RuleManager.CurrentRuleIndex != rule ||
                Common.RandomSource.DrawCount != draws || Common.RandomSource.State != randomState ||
                Common.RandomSource.Algorithm != randomAlgorithm)
                throw new InvalidOperationException("Simulation changed during reference-state capture.");
            return new SimulationSnapshot(json);
        }

        private sealed class Exporter
        {
            private readonly Dictionary<object, int> ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            private readonly List<object> pending = new List<object>();
            private readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();

            public object Encode(object value)
            {
                if (value == null) return null;
                Type type = value.GetType();
                if (type.IsEnum)
                    return new { enumType = type.FullName, value = Convert.ToString(value, CultureInfo.InvariantCulture) };
                if (value is float f && !float.IsFinite(f) || value is double d && !double.IsFinite(d))
                    throw new InvalidOperationException("Non-finite number in simulation state.");
                if (value is string || value is bool || value is char || value is byte || value is sbyte ||
                    value is short || value is ushort || value is int || value is uint || value is long ||
                    value is ulong || value is float || value is double || value is decimal)
                    return value;
                if (value is Point point) return new { x = point.X, y = point.Y };
                if (type.IsValueType)
                    throw new NotSupportedException("Unsupported simulation value: " + type.FullName);
                if (!ids.TryGetValue(value, out int id))
                {
                    id = pending.Count + 1;
                    ids.Add(value, id);
                    pending.Add(value);
                }
                return new { reference = id };
            }

            public List<object> Finish()
            {
                var objects = new List<object>();
                // Iterative graph traversal also handles long chains of deceased targets.
                for (int i = 0; i < pending.Count; i++)
                    objects.Add(Describe(pending[i], i + 1));
                return objects;
            }

            private object Describe(object value, int id)
            {
                Type type = value.GetType();
                var node = new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["id"] = id,
                    ["type"] = ComparisonTypeName(type)
                };
                Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (value is IDictionary dictionary &&
                    (definition == typeof(Dictionary<,>) || definition == typeof(SortedDictionary<,>) ||
                     definition == typeof(System.Collections.Concurrent.ConcurrentDictionary<,>)))
                {
                    var entries = new List<DictionaryEntry>();
                    foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
                    entries.Sort((a, b) => StringComparer.Ordinal.Compare(Key(a.Key), Key(b.Key)));
                    node["entries"] = entries.Select(entry => new { key = Encode(entry.Key), value = Encode(entry.Value) }).ToArray();
                    return node;
                }
                if (value is IList list && (definition == typeof(List<>) || type.IsArray && type.GetArrayRank() == 1))
                {
                    var items = new List<object>(list.Count);
                    foreach (object item in list) items.Add(Encode(item));
                    node["items"] = items;
                    return node;
                }
                if (type.Assembly != typeof(Organism).Assembly && type.Assembly != typeof(Board).Assembly)
                    throw new NotSupportedException("Unsupported object in simulation state: " + type.FullName);

                var state = new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (FieldInfo field in GetFields(type))
                {
                    // Explicit compatibility defaults preserve the Part 2 comparison
                    // format. Changed settings are included; display-only choice text
                    // never affects a future decision and is excluded deliberately.
                    if (value is SimulationRunOptions options && (
                        field.Name == "<InitialCellFood>k__BackingField" && options.InitialCellFood == 3 ||
                        field.Name == "<FoodRegrowthPerAge>k__BackingField" && options.FoodRegrowthPerAge == 1 ||
                        field.Name == "<InitialOrganismFood>k__BackingField" && options.InitialOrganismFood == 5 ||
                        field.Name == "<FounderRepertoire>k__BackingField" && options.FounderRepertoire == FounderRepertoire.UnrestrictedRandom ||
                        field.Name == "<CellExecution>k__BackingField" && options.CellExecution == CellExecutionMode.Automatic)) continue;
                    if (value is BoardSquare && field.Name == "foodRegrowthPerAge" && (byte)field.GetValue(value) == 1) continue;
                    if (value is ActionDecision && field.Name == "<ChoiceLabel>k__BackingField") continue;
                    if (value is Organism && field.DeclaringType == typeof(Organism) &&
                        (field.Name == "<EffectsStack>k__BackingField" || field.Name == "_referenceEffects"))
                        continue; // PendingEffects below exposes the actual execution order once.
                    state[field.DeclaringType.FullName + "." + field.Name] = Encode(field.GetValue(value));
                }
                if (value is Organism organism)
                    state["PendingEffects"] = organism.PendingEffects.Select(effect => Encode(effect)).ToArray();
                if (value is IDnaElement gene)
                {
                    state["DnaType"] = Encode(gene.DnaType);
                    state["Target"] = Encode(gene.Target);
                }
                if (value is IDnaEffect effectValue)
                    state["Effect"] = Encode(effectValue.Effect);
                node["fields"] = state;
                return node;
            }

            private FieldInfo[] GetFields(Type type)
            {
                if (!fields.TryGetValue(type, out FieldInfo[] result))
                {
                    var found = new List<FieldInfo>();
                    for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                    {
                        if (current.Assembly != typeof(Organism).Assembly && current.Assembly != typeof(Board).Assembly)
                            throw new NotSupportedException("Unsupported simulation base class: " + current.FullName);
                        found.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
                    }
                    result = found.OrderBy(field => field.DeclaringType.FullName + "." + field.Name, StringComparer.Ordinal).ToArray();
                    fields.Add(type, result);
                }
                return result;
            }

            private static string ComparisonTypeName(Type type)
            {
                // Schema v2 retains the accepted .NET 9 core-library qualification in
                // generic type labels. This is a comparison label, not the loaded runtime;
                // preserve type arguments, domain assembly identities and all state values.
                return type.FullName.Replace(typeof(object).Assembly.FullName,
                    "System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e",
                    StringComparison.Ordinal);
            }

            private static string Key(object key)
            {
                if (key == null) throw new NotSupportedException("Null simulation dictionary key.");
                Type type = key.GetType();
                if (!(key is string) && !type.IsEnum &&
                    !(key is byte || key is sbyte || key is short || key is ushort || key is int ||
                      key is uint || key is long || key is ulong))
                    throw new NotSupportedException("Unsupported simulation dictionary key: " + type.FullName);
                return type.FullName + ":" + Convert.ToString(key, CultureInfo.InvariantCulture);
            }
        }
    }
}
