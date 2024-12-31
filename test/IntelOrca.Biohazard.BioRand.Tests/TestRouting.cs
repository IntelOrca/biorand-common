using System;
using System.Linq;
using IntelOrca.Biohazard.BioRand.Routing;
using Xunit;
using Xunit.Abstractions;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestRouting
    {
        private const int Retries = 100;

        private readonly ITestOutputHelper _output;

        public TestRouting(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Tests an OR gate, i.e. two entrances to a room.
        /// </summary>
        [Fact]
        public void AltWaysInSameRoom()
        {
            var builder = new DependencyGraphBuilder();
            var room0 = builder.AndGate("ROOM 0");
            var room1 = builder.AndGate("ROOM 1", room0);
            var room2 = builder.AndGate("ROOM 2", room0);
            var room3 = builder.OrGate("ROOM 3", room1, room2);
            var route = builder.GenerateRoute();
            Assert.True(route.AllNodesVisited);
        }

        /// <summary>
        /// A simple 3 room map with items and keys.
        /// </summary>
        [Fact]
        public void Basic()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);

                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);

                var room2 = builder.AndGate("ROOM 2", room1, key1);
                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a, item0b);
                AssertKeyOnce(route, key1, item0a, item0b, item1a);
                Assert.Equal((RouteSolverResult)0, route.Solve());
            }
        }

        /// <summary>
        /// A door that can only be opened from one side.
        /// </summary>
        [Fact]
        public void BlockedDoor()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var key0 = builder.Key("KEY 0", 1);
                var key1 = builder.Key("KEY 1", 1);
                var room0 = builder.Room("ROOM 0");
                var room1 = builder.Room("ROOM 1");
                var room2 = builder.Room("ROOM 2");
                var room3 = builder.Room("ROOM 3");
                var item1 = builder.Item("ITEM 1", 1, room1);
                var item2 = builder.Item("ITEM 2", 1, room2);
                builder.BlockedDoor(room0, room1, key0);
                builder.BlockedDoor(room1, room2);
                builder.Door(room1, room3, key1);
                builder.Door(room2, room0);
                var route = builder.GenerateRoute(i);
                AssertKeyOnce(route, key0, item2);
                AssertKeyOnce(route, key1, item1);
                Assert.Equal(RouteSolverResult.Ok, route.Solve());
            }
        }

        [Fact]
        public void KeyBehindKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var item0c = builder.Item(1, "ITEM 0.C", room0, key0);
                var room1 = builder.AndGate("ROOM 1", room0, key0, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a, item0b);
                AssertKeyOnce(route, key1, item0a, item0b, item0c);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// An edge case where the keys must be placed in a certain order,
        /// otherwise we run out of accessible item nodes for all the keys.
        /// </summary>
        [Fact]
        public void KeyOrderMatters()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0, key0);
                var room1 = builder.AndGate("ROOM 1", room0, key0, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a);
                AssertKeyOnce(route, key1, item0b);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Test that when we fulfill a key, we don't add its edges to the next list.
        /// This prevents cases where we place keys prematurely before we require them.
        /// </summary>
        [Fact]
        public void NoKey()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var key2 = builder.ReusuableKey(1, "KEY 2");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var room2 = builder.AndGate("ROOM 2", room1, key1);
                var room3 = builder.AndGate("ROOM 3", room2, key0, key2);
                var route = builder.GenerateRoute(i);

                Assert.False(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where a key must be placed in both
        /// segments to prevent softlock if player does not collect key in first segment.
        /// </summary>
        [Fact]
        public void EnsureKeyPlacedAgain()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var room2 = builder.NoReturn("ROOM 2", room0);
                var item2a = builder.Item(1, "ITEM 2.A", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key0);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                AssertItem(route, item2a, key0);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where a key only needs to be placed
        /// once as the key is required to get to the second segment.
        /// </summary>
        [Fact]
        public void EnsureKeyNotPlacedAgain()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.NoReturn("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                AssertItemNotFulfilled(route, item1a);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where a key to get another key
        /// only needs to be placed once.
        /// </summary>
        [Fact]
        public void EnsureNestedKeysNotPlacedAgain()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();
                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.AndGate("ROOM 1", room0);
                var item1a = builder.Item(1, "ITEM 1.A", room1, key0);
                var room2 = builder.NoReturn("ROOM 2", room1, key1);
                var room2a = builder.Item(1, "ITEM 2.A", room2);
                var room2b = builder.Item(1, "ITEM 2.B", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key0);
                var room4 = builder.AndGate("ROOM 4", room3, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a);
                AssertKeyOnce(route, key1, item1a);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where k1 needs to be placed
        /// again because there are two paths leading to k0.
        /// </summary>
        [Fact]
        public void EnsureSemiNestedKeys()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();
                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key1);
                var item1a = builder.Item(1, "ITEM 1.A", room1);
                var room2 = builder.OrGate("ROOM 2", room0, room1);
                var room3 = builder.NoReturn("ROOM 3", room2, key0);
                var room3a = builder.Item(1, "ITEM 3.A", room3);
                var room3b = builder.Item(1, "ITEM 3.B", room3);
                var room4 = builder.AndGate("ROOM 5", room3, key0, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a, item1a);
                AssertKeyQuantity(route, key1, 2);
                AssertItem(route, room3a, key1, null);
                AssertItem(route, room3b, key1, null);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with two segments where k1 needs to be placed
        /// again because there are two paths leading to k0.
        /// </summary>
        [Fact]
        public void EnsureKeyToRequiredNodeNotPlacedAgain()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();
                var key0 = builder.ReusuableKey(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var room1 = builder.AndGate("ROOM 1", room0);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var room3 = builder.NoReturn("ROOM 3", room1, room2);
                var item3a = builder.Item(1, "ITEM 3.A", room3);
                var room4 = builder.AndGate("ROOM 4", room3, key0);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0a);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a simple map with a one way edge.
        /// </summary>
        [Fact]
        public void BasicOneWay()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var room0 = builder.Room("ROOM 0");
                var room1 = builder.Room("ROOM 1");
                var room2 = builder.Room("ROOM 2");
                builder.Door(room0, room1);
                builder.Door(room1, room2);
                builder.OneWay(room0, room2);

                var route = builder.GenerateRoute(i);
                Assert.True(route.AllNodesVisited);
            }
        }

        /// <summary>
        /// Tests a map with a mini segment which can go back to main segment.
        /// </summary>
        [Fact]
        public void MiniSegment()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var key0 = builder.Key("KEY 0", 1);
                var key1 = builder.Key("KEY 1", 1);

                var room1 = builder.Room("ROOM 1");
                var room2 = builder.Room("ROOM 2");
                var room3 = builder.Room("ROOM 3");
                var room4 = builder.Room("ROOM 4");
                var room5 = builder.Room("ROOM 5");
                var room6 = builder.Room("ROOM 6");

                var item2 = builder.Item("Item 2", 1, room2);
                var item3 = builder.Item("Item 3", 1, room3);

                builder.Door(room1, room2);
                builder.Door(room2, room5);
                builder.Door(room3, room4, key1);
                builder.Door(room3, room6, key0);
                builder.BlockedDoor(room4, room5);
                builder.OneWay(room1, room3);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key0, item2);
                AssertKeyOnce(route, key1, item3);
            }
        }

        /// <summary>
        /// Tests a map with a two segments which you can go between.
        /// </summary>
        [Fact(Skip = "Failing")]
        public void CircularSegments()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ReusuableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(2, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var room1 = builder.OneWay("ROOM 1", room0);
                var room2 = builder.AndGate("ROOM 2", room1);
                var room3 = builder.AndGate("ROOM 3", room0);
                var room4 = builder.AndGate("ROOM 4", room2);
                var room5 = builder.OrGate("ROOM 5", room3, builder.OneWay(null, room4));
                var room6 = builder.AndGate("ROOM 6", room2, key1);
                var room7 = builder.AndGate("ROOM 7", room3, key0);
                var item2 = builder.Item(1, "Item 2", room2);
                var item3 = builder.Item(2, "Item 3", room3);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key0, item2);
                AssertKeyOnce(route, key1, item3);
            }
        }

        /// <summary>
        /// Tests a map with a mini segment which you do once then never
        /// return to.
        /// </summary>
        [Fact]
        public void SingleTimeMiniSegment()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var key0 = builder.Key("KEY 0", 1);
                var key1 = builder.Key("KEY 1", 1);

                var room0 = builder.Room("ROOM 0");
                var room1 = builder.Room("ROOM 1");
                var room2 = builder.Room("ROOM 2");
                var room3 = builder.Room("ROOM 3");
                var room4 = builder.Room("ROOM 4");
                var room5 = builder.Room("ROOM 5");

                var item0 = builder.Item("ITEM 0", 1, room0);
                var item2 = builder.Item("ITEM 2", 1, room2);
                var item3 = builder.Item("ITEM 3", 1, room3, key1);

                builder.Door(room0, room1);
                builder.Door(room1, room4);
                builder.Door(room4, room5, key0);
                builder.OneWay(room1, room2);
                builder.Door(room2, room3);
                builder.NoReturn(room3, room4);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key0, item0);
                AssertKeyOnce(route, key1, item2);
            }
        }

        /// <summary>
        /// Tests a one way edge, where a key might be placed again,
        /// due to a door requiring it in new area.
        /// </summary>
        [Fact]
        public void OneWay_KeyNotPlacedTwice()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new GraphBuilder();
                var key0 = builder.Key("KEY 0", 1);
                var key1 = builder.Key("KEY 1", 1);
                var key2 = builder.Key("KEY 2", 1);

                var room0 = builder.Room("ROOM 0");
                var room1 = builder.Room("ROOM 1");
                var room2 = builder.Room("ROOM 2");
                var room3 = builder.Room("ROOM 3");
                var room4 = builder.Room("ROOM 4");
                var room5 = builder.Room("ROOM 5");

                var item0 = builder.Item("ITEM 0", 1, room0);
                var item1 = builder.Item("ITEM 1", 1, room1);
                var item2 = builder.Item("ITEM 2", 1, room2);
                var item4 = builder.Item("ITEM 4", 1, room4);

                builder.Door(room0, room1);
                builder.OneWay(room1, room2, key1);
                builder.Door(room1, room5, key0);
                builder.Door(room2, room4);
                builder.BlockedDoor(room4, room1, key2);
                builder.Door(room2, room3, key0);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key0, item0, item1, item2, item4);
            }
        }

        [Fact]
        public void SingleUseKey_DoorAfterDoor()
        {
            var builder = new DependencyGraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0);
            var item1 = builder.Item(1, "ITEM 1", room1);
            var room2 = builder.AndGate("ROOM 2", room1, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_TwoDoors()
        {
            var builder = new DependencyGraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var item1 = builder.Item(1, "ITEM 1", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0);
            var room2 = builder.AndGate("ROOM 2", room0, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_TwoDoors_NoPossibleSoftlock()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0A", room0);
                var item0b = builder.Item(1, "ITEM 0B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key0);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0a, key0);
                AssertItem(route, item0b, key0);
                AssertItemNotFulfilled(route, item1);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void SingleUseKey_RouteOrderMatters()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var room3 = builder.AndGate("ROOM 3", room0, key0);
                var item3a = builder.Item(1, "ITEM 3.A", room3);
                var item3b = builder.Item(1, "ITEM 3.B", room3);

                var route = builder.GenerateRoute(i);

                AssertItem(route, item0, key0);
                AssertKeyOnce(route, key1, item3a, item3b);
                AssertKeyQuantity(route, key0, 2);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void SingleUseKey_RouteOrderMatters_Flexible()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var room3 = builder.AndGate("ROOM 3", room0, key0);
                var item3a = builder.Item(1, "ITEM 3.A", room3);
                var item3b = builder.Item(1, "ITEM 3.B", room3);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key1, item3a, item3b);
            }
        }

        [Fact]
        public void SingleUseKey_TwoOneWayDoors()
        {
            var builder = new DependencyGraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var item1 = builder.Item(1, "ITEM 1", room0);
            var room1 = builder.OneWay("ROOM 1", room0, key0);
            var room2 = builder.OneWay("ROOM 2", room0, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_TwoKeyDoor()
        {
            var builder = new DependencyGraphBuilder();

            var key0 = builder.ConsumableKey(1, "KEY 0");
            var key1 = builder.ReusuableKey(2, "KEY 1");
            var room0 = builder.AndGate("ROOM 0");
            var item0 = builder.Item(1, "ITEM 0", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0);
            var item1a = builder.Item(1, "ITEM 1A", room1);
            var item1b = builder.Item(2, "ITEM 1B", room1);
            var room2 = builder.AndGate("ROOM 2", room0, key0, key1);
            var route = builder.GenerateRoute();

            AssertItem(route, item0, key0);
            AssertItem(route, item1a, key0);
            AssertItem(route, item1b, key1);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void SingleUseKey_KeyOrderMatters()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.ConsumableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");
                var key2 = builder.ReusuableKey(1, "KEY 2");
                var room0 = builder.AndGate("ROOM 0");
                var item0a = builder.Item(1, "ITEM 0.A", room0);
                var item0b = builder.Item(1, "ITEM 0.B", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1a = builder.Item(1, "ITEM 1.A", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key1);
                var item2a = builder.Item(1, "ITEM 2.A", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key2);
                var room4 = builder.AndGate("ROOM 4", room3, key0);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void Key2xRequired()
        {
            var builder = new DependencyGraphBuilder();

            var key0 = builder.ReusuableKey(1, "KEY 0");
            var room0 = builder.AndGate("ROOM 0");
            var item0a = builder.Item(1, "ITEM 0.A", room0);
            var item0b = builder.Item(1, "ITEM 0.B", room0);
            var room1 = builder.AndGate("ROOM 1", room0, key0, key0);

            var route = builder.GenerateRoute();

            AssertItem(route, item0a, key0);
            AssertItem(route, item0b, key0);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void TwoRoutes()
        {
            var builder = new GraphBuilder();

            var keyTop = builder.Key("KEY TOP", 1);
            var keyBottom = builder.Key("KEY BOTTOM", 1);
            var keyEnd = builder.Key("KEY END", 1);

            var roomStart = builder.Room("ROOM START");
            var roomTop1 = builder.Room("ROOM TOP 1");
            var roomTop2 = builder.Room("ROOM TOP 2");
            var roomBottom1 = builder.Room("ROOM BOTTOM 1");
            var roomBottom2 = builder.Room("ROOM BOTTOM 2");
            var roomMerge = builder.Room("ROOM MERGE");
            var roomEnd = builder.Room("ROOM END");

            var itemTop1 = builder.Item("ITEM TOP 1", 1, roomTop1);
            var itemBottom1 = builder.Item("ITEM BOTTOM 1", 1, roomBottom1);
            var itemMerge = builder.Item("ITEM MERGE", 1, roomMerge);

            builder.NoReturn(roomStart, roomTop1);
            builder.NoReturn(roomStart, roomBottom1);
            builder.BlockedDoor(roomTop1, roomTop2, keyTop);
            builder.Door(roomTop2, roomMerge);
            builder.BlockedDoor(roomBottom1, roomBottom2, keyBottom);
            builder.Door(roomBottom2, roomMerge);
            builder.Door(roomMerge, roomEnd, keyEnd);

            var route = builder.GenerateRoute();

            AssertItem(route, itemTop1, keyTop);
            AssertItem(route, itemBottom1, keyBottom);
            AssertItem(route, itemMerge, keyEnd);
            Assert.True(route.AllNodesVisited);
        }

        [Fact]
        public void Removable_SingleKeyRequired()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.RemovableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 1");

                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key0);
                var item2 = builder.Item(1, "ITEM 1", room2);
                var room3 = builder.AndGate("ROOM 2", room2, key1);

                var route = builder.GenerateRoute(i);

                AssertKeyOnce(route, key0, item0);
                AssertKeyOnce(route, key1, item1, item2);
                Assert.True(route.AllNodesVisited);
            }
        }

        [Fact]
        public void Removable_MultipleKeysRequired()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.RemovableKey(1, "KEY 0");

                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room1, key0);
                var item2 = builder.Item(1, "ITEM 2", room2);
                var room3 = builder.AndGate("ROOM 3", room2, key0);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertItem(route, item0, key0);
                AssertKeyQuantity(route, key0, 3);
            }
        }

        [Fact]
        public void Removable_MultipleKeysRequiredOnce()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key0 = builder.RemovableKey(1, "KEY 0");
                var key1 = builder.ReusuableKey(1, "KEY 0");

                var room0 = builder.AndGate("ROOM 0");
                var item0 = builder.Item(1, "ITEM 0", room0);
                var room1 = builder.AndGate("ROOM 1", room0, key0);
                var item1 = builder.Item(1, "ITEM 1", room1);
                var room2 = builder.AndGate("ROOM 2", room0, key0);
                var item2 = builder.Item(1, "ITEM 2", room2);
                var room3 = builder.AndGate("ROOM 3", room1, key0);
                var room4 = builder.AndGate("ROOM 4", room2, key0);
                var room5 = builder.AndGate("ROOM 5", room4, key1);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key1, item1, item2);
                AssertItem(route, item0, key0);
                AssertItem(route, item1, key0, key1);
                AssertItem(route, item2, key0, key1);
                AssertKeyQuantity(route, key0, 2);
            }
        }

        /// <summary>
        /// Tests that keys only get placed in items with a group mask that
        /// fits the key's mask.
        /// </summary>
        [Fact]
        public void KeysRestrictedToZones()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();

                var key1 = builder.ReusuableKey(1, "KEY 1");
                var key2 = builder.ReusuableKey(2, "KEY 2");
                var key3 = builder.ReusuableKey(3, "KEY 3");

                var room0 = builder.AndGate("ROOM 0");
                var room1 = builder.AndGate("ROOM 3", room0, key1);
                var room2 = builder.AndGate("ROOM 4", room0, key2);
                var room3 = builder.AndGate("ROOM 5", room0, key3);
                var room4 = builder.OrGate("ROOM 6", room1, room2, room3);

                var item1 = builder.Item(1, "ITEM 1", room0);
                var item2 = builder.Item(2, "ITEM 2", room0);
                var item3 = builder.Item(3, "ITEM 3", room0);
                var item7 = builder.Item(7, "ITEM 7", room0);

                var route = builder.GenerateRoute(i);

                Assert.True(route.AllNodesVisited);
                AssertKeyOnce(route, key1, item1, item3, item7);
                AssertKeyOnce(route, key2, item2, item3, item7);
                AssertKeyOnce(route, key3, item3, item7);
            }
        }

        [Fact]
        public void RequiresNonExistantRoom()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();
                var room0 = builder.AndGate("ROOM 0");
                var room1 = builder.AndGate("ROOM 1");
                var room2 = builder.AndGate("ROOM 2", room0, room1);

                var route = builder.GenerateRoute(i);
                Assert.False(route.AllNodesVisited);
            }
        }

        [Fact]
        public void RequiresRoomPreviousSegment()
        {
            for (var i = 0; i < Retries; i++)
            {
                var builder = new DependencyGraphBuilder();
                var room0 = builder.AndGate("ROOM 0");
                var room1 = builder.AndGate("ROOM 1", room0);
                var room2 = builder.NoReturn("ROOM 2", room0, room1);
                var room3 = builder.AndGate("ROOM 3", room2, room1);

                var route = builder.GenerateRoute(i);
                Assert.True(route.AllNodesVisited);
            }
        }

        private static void AssertItemNotFulfilled(Route route, Node item)
        {
            var actual = route.GetItemContents(item);
            Assert.True(actual == null,
                string.Format("Expected {0} to be unfulfilled but was {1}",
                    item,
                    actual));
        }

        private static void AssertItem(Route route, Node item, params Key?[] expected)
        {
            var actual = route.GetItemContents(item);
            Assert.True(Array.IndexOf(expected, actual) != -1,
                string.Format("Expected {0} to be {{{1}}} but was {2}",
                    item,
                    string.Join(", ", expected),
                    actual?.ToString() ?? "(null)"));
        }

        private static void AssertKeyOnce(Route route, Key key, params Node[] expected)
        {
            var items = route.Graph.Nodes
                .Where(x => x.Kind == NodeKind.Item)
                .Where(x => route.GetItemContents(x) is Key k && k == key)
                .ToArray();

            if (items.Length == 0)
            {
                Assert.True(items.Length == expected.Length,
                    string.Format("Expected {0} to be at {{{1}}} but was not placed",
                        key,
                        string.Join(", ", expected)));
            }
            else
            {
                foreach (var item in items)
                {
                    Assert.True(Array.IndexOf(expected, item) != -1,
                        string.Format("Expected {0} to be at {{{1}}} but was at {2}",
                            key,
                            string.Join(", ", expected),
                            item));
                }
            }
            Assert.True(items.Length == 1, "Expected key to only be placed once");
        }

        private static void AssertKeyQuantity(Route route, Key key, int expectedCount)
        {
            var actualCount = route.GetItemsContainingKey(key).Count;
            Assert.Equal(expectedCount, actualCount);
        }
    }
}
