using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.BioRand.Routing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = false)]

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestRoutingExamples
    {
        [Fact]
        public void Example_RE2_LEON_A()
        {
            var graph = GetGraph("re2");
            var mNoItems = graph.ToMermaid(useLabels: true, includeItems: false);
            var mItems = graph.ToMermaid(useLabels: true, includeItems: true);
            var route = graph.GenerateRoute(0);

            var expectedCounts = new Dictionary<string, (int, int)>()
            {
                ["SmallKey"] = (2, 2),
                ["RedJewel"] = (2, 2),
                ["Lighter"] = (1, 3),
                ["ValveHandle"] = (1, 2),
            };
            foreach (var k in route.Graph.Keys)
            {
                var count = route.GetItemsContainingKey(k).Count;
                if (!expectedCounts.TryGetValue(k.Label, out var expectedCount))
                    expectedCount = (1, 1);
                // Assert.True(count >= expectedCount.Item1, $"{k.Label} was placed {count} times");
                // Assert.True(count <= expectedCount.Item2, $"{k.Label} was placed {count} times");
            }
            Assert.True(route.AllNodesVisited);
        }

        private static Graph GetGraph(string name)
        {
            var exampleGraph = GetExampleGraph("re2");
            var player = 0;
            var scenario = 0;

            var builder = new GraphBuilder();
            var keys = new Dictionary<int, Key>();
            foreach (var kvp in exampleGraph.Keys)
            {
                keys[kvp.Key] = builder.Key(kvp.Value.Name, 1, (KeyKind)Enum.Parse(typeof(KeyKind), kvp.Value.Kind, true));
            }

            var roomNodes = new Dictionary<string, Node>();
            foreach (var kvp in exampleGraph.Rooms)
            {
                var roomName = exampleGraph.RoomNames[kvp.Key];
                roomNodes[kvp.Key] = builder.Room($"ROOM:{roomName}");

                foreach (var item in kvp.Value.Items)
                {
                    if (item.Player != null && item.Player != player)
                        continue;
                    if (item.Scenario != null && item.Scenario != scenario)
                        continue;

                    if (item.Kind == "low")
                        continue;

                    var requires = item.Requires.Select(TransformRequirement).ToArray();

                    exampleGraph.ItemNames.TryGetValue(item.GlobalId, out var itemName);
                    if (string.IsNullOrEmpty(itemName))
                        itemName = $"{item.GlobalId}";
                    builder.Item($"ITEM:{roomName}/{itemName}", 1, roomNodes[kvp.Key], requires);
                }
            }

            foreach (var kvp in exampleGraph.Rooms)
            {
                foreach (var door in kvp.Value.Doors)
                {
                    if (door.Player != null && door.Player != player)
                        continue;
                    if (door.Scenario != null && door.Scenario != scenario)
                        continue;

                    if (door.Kind == "locked")
                        continue;
                    if (door.Kind == "side")
                        continue;

                    var target = door.Target;
                    if (target.Contains(":"))
                        target = target.Substring(0, target.IndexOf(':'));

                    var requires = door.Requires.Select(TransformRequirement).ToArray();

                    if (door.Kind == "oneway")
                    {
                        builder.OneWay(roomNodes[kvp.Key], roomNodes[target], requires);
                    }
                    else if (door.Kind == "noreturn")
                    {
                        builder.NoReturn(roomNodes[kvp.Key], roomNodes[target], requires);
                    }
                    else if (door.Kind == "unblock")
                    {
                        builder.BlockedDoor(roomNodes[kvp.Key], roomNodes[target], requires);
                    }
                    else
                    {
                        builder.Door(roomNodes[kvp.Key], roomNodes[target], requires);
                    }
                }
                foreach (var flag in kvp.Value.Flags)
                {
                    if (flag.Player != null && flag.Player != player)
                        continue;
                    if (flag.Scenario != null && flag.Scenario != scenario)
                        continue;

                    if (!roomNodes.ContainsKey(flag.Name))
                        roomNodes.Add(flag.Name, builder.Room($"FLAG:{flag.Name}"));

                    var requires = flag.Requires.Select(TransformRequirement).ToArray();
                    builder.Edge(roomNodes[kvp.Key], roomNodes[flag.Name], EdgeKind.TwoWay, requires);
                }
            }

            var graph = builder.ToGraph();
            return graph;

            Requirement TransformRequirement(string s)
            {
                var match = Regex.Match(s, @"([a-z]+)\(([A-Za-z0-9_]+)\)");
                if (match.Success)
                {
                    var kind = match.Groups[1].Value;
                    var value = match.Groups[2].Value;
                    if (kind == "item")
                    {
                        if (int.TryParse(value, out var itemId))
                        {
                            return new Requirement(keys[itemId]);
                        }
                    }
                    else if (kind == "room")
                    {
                        return new Requirement(roomNodes[value]);
                    }
                    else if (kind == "flag")
                    {
                        if (!roomNodes.ContainsKey(value))
                            roomNodes.Add(value, builder.Room(value));
                        return new Requirement(roomNodes[value]);
                    }
                }
                throw new Exception($"Failed to parse requirement: {s}");
            }
        }

        private static ExampleGraph GetExampleGraph(string name)
        {
            var resourceName = $"IntelOrca.Biohazard.BioRand.Common.Tests.data.{name}.json";
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return JsonSerializer.Deserialize<ExampleGraph>(ms, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        private class ExampleGraph
        {
            public BeginEndRoom[] BeginEndRooms { get; set; } = [];
            public Dictionary<string, Room> Rooms { get; set; } = [];
            public Dictionary<int, Key> Keys { get; set; } = [];
            public Dictionary<string, string> RoomNames { get; set; } = [];
            public Dictionary<int, string> ItemNames { get; set; } = [];

            public class BeginEndRoom
            {
                public string Start { get; set; } = "";
                public string End { get; set; } = "";
                public int Player { get; set; }
                public int Scenario { get; set; }
            }

            public class Room
            {
                public Door[] Doors { get; set; } = [];
                public Flag[] Flags { get; set; } = [];
                public Item[] Items { get; set; } = [];
            }

            public class Door
            {
                public string Target { get; set; }
                public string Kind { get; set; }
                public string[] Requires { get; set; } = [];
                public int? Player { get; set; }
                public int? Scenario { get; set; }
            }

            public class Flag
            {
                public string Name { get; set; }
                public string[] Requires { get; set; } = [];
                public int? Player { get; set; }
                public int? Scenario { get; set; }
            }

            public class Item
            {
                public int GlobalId { get; set; }
                public string Kind { get; set; }
                public string[] Requires { get; set; } = [];
                public int? Player { get; set; }
                public int? Scenario { get; set; }
            }

            public class Key
            {
                public string Name { get; set; }
                public string Kind { get; set; }
            }
        }
    }
}
