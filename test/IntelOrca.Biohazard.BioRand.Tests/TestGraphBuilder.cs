using IntelOrca.Biohazard.BioRand.Routing;
using Xunit;

namespace IntelOrca.Biohazard.BioRand.Common.Tests
{
    public class TestGraphBuilder
    {
        [Fact]
        public void NoDuplicateEdge_Simple()
        {
            var builder = new GraphBuilder();
            var roomA = builder.Room("ROOM A");
            var roomB = builder.Room("ROOM B");
            var edgeA = builder.Door(roomA, roomB);
            var edgeB = builder.Door(roomA, roomB);
            var edgeC = builder.Door(roomB, roomA);
            Assert.Equal(edgeA, edgeB);
            Assert.Equal(edgeA, edgeC);

            var g = builder.ToGraph();
            Assert.Single(g.Edges);
        }

        [Fact]
        public void NoDuplicateEdge_NewRequirements()
        {
            var builder = new GraphBuilder();
            var key = builder.Key("KEY 0", 1);
            var roomA = builder.Room("ROOM A");
            var roomB = builder.Room("ROOM B");
            var edgeA = builder.Door(roomA, roomB);
            var edgeB = builder.Door(roomB, roomA, key);
            Assert.NotEqual(edgeA, edgeB);

            var g = builder.ToGraph();
            Assert.Single(g.Edges);
        }

        /// <summary>
        /// Checks that an exception is not thrown if a no return never leads back to somewhere
        /// we have already been.
        /// </summary>
        [Fact]
        public void NoReturnThatDoesNotReturn()
        {
            var builder = new GraphBuilder();
            var room0 = builder.Room();
            var room1 = builder.Room();
            var room2 = builder.Room();
            var room3 = builder.Room();
            var room4 = builder.Room();
            var room5 = builder.Room();
            var room6 = builder.Room();
            builder.Door(room0, room1);
            builder.Door(room1, room2);
            builder.Door(room1, room3);
            builder.NoReturn(room3, room4);
            builder.Door(room4, room5);
            builder.Door(room5, room6);
            builder.ToGraph();
        }

        /// <summary>
        /// Checks that an exception is thrown if a no return leads back to somewhere
        /// we have already been.
        /// </summary>
        [Fact(Skip = "No longer validated")]
        public void NoReturnThatReturns()
        {
            var builder = new GraphBuilder();
            var room0 = builder.Room();
            var room1 = builder.Room();
            var room2 = builder.Room();
            var room3 = builder.Room();
            var room4 = builder.Room();
            var room5 = builder.Room();
            var room6 = builder.Room();
            builder.Door(room0, room1);
            builder.Door(room1, room2);
            builder.Door(room1, room3);
            builder.NoReturn(room3, room4);
            builder.Door(room4, room5);
            builder.Door(room5, room6);
            builder.Door(room5, room2);
            Assert.Throws<GraphException>(builder.ToGraph);
        }

        [Fact]
        public void Example_RE2()
        {
            var builder = new GraphBuilder();
            var keyBlueKeycard = builder.Key("Blue Keycard");
            var keyUnicorn = builder.Key("Unicorn Medal");
            var keySpade = builder.Key("Spade Key");
            var keyDiamond = builder.Key("Diamond Key");
            var keySmall = builder.Key("Small Key", kind: KeyKind.Consumable);
            var keyRedJewel = builder.Key("Red Jewel", kind: KeyKind.Consumable);

            var room103 = builder.Room("103 - RPD FRONT");
            var room200 = builder.Room("200 - RPD MAIN HALL");
            var room201 = builder.Room("201 - RPD WAITING");
            var room202 = builder.Room("202 - RPD MARVIN");
            var room203 = builder.Room("203 - RPD LICKER");
            var room204 = builder.Room("204 - RPD RECORDS");
            var room205 = builder.Room("205 - RPD ARMS");
            var room208 = builder.Room("208 - RPD FIREPLACE");
            var room206 = builder.Room("206 - RPD DARK STAIRS");
            var room207 = builder.Room("207 - RPD EVIDENCE");

            builder.Item("103 - PLANT", room103);
            builder.Item("200 - DESK 1", room200);
            builder.Item("200 - DESK 2", room200);
            builder.Item("200 - FOUNTAIN", room200, keyUnicorn);
            builder.Item("201 - DOCUMENT", room201);
            builder.Item("201 - LOCKED DRAWER", room201, keySmall);
            builder.Item("202 - MARVIN GIFT", room200);
            builder.Item("203 - FLOOR", room203);
            builder.Item("203 - BODY", room203);
            builder.Item("204 - STEPLADDER TOP", room204);
            builder.Item("204 - STEPLADDER MID", room204);
            builder.Item("204 - DOOR DOCUMENT", room204);
            builder.Item("204 - DOOR HIDDEN", room204);
            builder.Item("206 - STAIRS 1", room206);
            builder.Item("206 - STAIRS 2", room206);
            builder.Item("208 - FIREPLACE", room208, keyRedJewel, keyRedJewel);
            builder.Item("208 - DOCUMENT", room208);
            builder.Item("208 - CORNER", room208);
            builder.Item("208 - TABLES", room208);

            builder.Door(room103, room200);
            builder.Door(room200, room201, keyBlueKeycard);
            builder.Door(room201, room203);
            builder.Door(room203, room204, keySpade);
            builder.OneWay(room202, room200);
            builder.Door(room203, room205);
            builder.Door(room205, room206);
            builder.Door(room205, room208);
            builder.Door(room206, room207, keyDiamond);
            builder.Door(room207, room202);

            var graph = builder.ToGraph();
            var route = new RouteFinder().Find(graph);
            Assert.Equal(7, route.ItemToKey.Count);
        }

        [Fact]
        public void Example_RE2R()
        {
            var builder = new GraphBuilder();
            var gasStationKey = builder.Key("Gas Station Key");

            var outside = builder.Room("GAS STATION OUTSIDE");
            var shop = builder.Room("GAS STATION SHOP");
            var corridor = builder.Room("GAS STATION CORRIDOR");
            var backRoom = builder.Room("GAS STATION BACK ROOM");

            builder.Door(outside, shop);
            builder.Door(shop, corridor);
            builder.OneWay(corridor, backRoom);
            builder.Door(backRoom, shop, gasStationKey);
            var item0 = builder.Item("SHOP ITEM 1", shop);
            var item1 = builder.Item("SHOP ITEM 2", shop);
            var item2 = builder.Item("BACK ROOM ITEM 1", backRoom);
            var item3 = builder.Item("BACK ROOM ITEM 2", backRoom);

            var route = builder.GenerateRoute();
        }
    }
}
