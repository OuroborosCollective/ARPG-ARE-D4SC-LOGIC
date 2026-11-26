using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Plays the chapter like a person holding the phone. It looks at what the screen shows (positions, health,
    /// cooldowns, the offered journal buttons) and answers only with the stick and taps on visible buttons. It never
    /// moves a figure, changes health or calls a game method; travel, saving and the story all run inside the game.
    /// </summary>
    internal sealed class JourneyPlayer
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Func<ArenaRuntime> _runtime;
        private readonly Navigator _navigator = new Navigator();
        private readonly int _frameBudget;
        private int _lastSceneTap = -100, _lastRestartTap = -100;
        private bool _counted;

        public readonly ScriptedPhone Phone = new ScriptedPhone();
        public readonly List<string> Trail = new List<string>();
        public int Frames { get; private set; }
        public int Deaths { get; private set; }
        public int StoryTaps { get; private set; }
        /// <summary>Where the last passage put the player: the area's checkpoint.</summary>
        public long ArrivalX { get; private set; }
        public long ArrivalZ { get; private set; }

        public JourneyPlayer(Func<ArenaRuntime> runtime, int frameBudget) { _runtime = runtime; _frameBudget = frameBudget; }

        public ArenaRuntime Runtime { get { return _runtime(); } }
        public ArenaSimulation Game { get { return (ArenaSimulation)typeof(ArenaRuntime).GetField("_game", Private).GetValue(Runtime); } }

        /// <summary>The same layout the game computes for its fixed test screen.</summary>
        public ArenaHudLayout Layout
        {
            get
            {
                Vector2 s = ArenaRuntime.HudScreenOverride.Value;
                return new ArenaHudLayout(s.x, s.y, new Rect(0f, 0f, s.x, s.y));
            }
        }

        private T Read<T>(string field) { return (T)typeof(ArenaRuntime).GetField(field, Private).GetValue(Runtime); }
        private bool JournalOpen { get { return Read<bool>("_hungerJournalOpen"); } }
        private bool MenuOpen { get { return Runtime.MenuOpen; } }
        private int PendingStory { get { return Read<int>("_pendingHungerAction"); } }
        private string Message { get { return Read<string>("_message"); } }
        private HungerAction[] OfferedStory { get { return (HungerAction[])typeof(ArenaRuntime).GetMethod("HungerActions", Private).Invoke(Runtime, null); } }
        private bool HunterButtonShown { get { return (bool)typeof(ArenaRuntime).GetMethod("CanShowHunterAction", Private).Invoke(Runtime, null); } }
        private bool StickCaptured { get { return Read<ArenaHudTouchCapture>("_movementTouch").IsCaptured; } }

        /// <summary>Remember one moment of the run for the evidence log.</summary>
        public void Note(string text)
        {
            var g = Game;
            string line = "frame " + Frames + " · tick " + g.CurrentTick + " · " + g.ChapterName + " · " + text;
            Trail.Add(line);
            Debug.Log("[PlayerRun] " + line); // follow a long run in the editor log
        }

        /// <summary>What the screen shows right now, for a failure message.</summary>
        public string Describe()
        {
            var g = Game;
            return g.ChapterName + " act " + g.Progression.EntryAct + " stage " + g.Progression.HungerStage + " at (" + g.PlayerXmm + ", " +
                g.PlayerZmm + ") hp " + g.PlayerHealth + "/" + g.MaxPlayerHealth + " frame " + Frames + " message \"" + Message + "\"";
        }

        /// <summary>Show the next frame the committed touches, then let the game run it.</summary>
        public IEnumerator Frame()
        {
            if (Phone.StickDown && !StickCaptured && Frames > 0) Phone.ForgetStick(); // the game let go of the finger
            Phone.Commit();
            yield return null;
            Frames++;
            if (Frames > _frameBudget) Assert.Fail("The run took longer than " + _frameBudget + " frames: " + Describe());
        }

        /// <summary>Screens that stop play: the arrival scene wants taps, defeat wants RESTART. True while one is up.</summary>
        private bool Interrupted()
        {
            var g = Game;
            Assert.IsFalse(MenuOpen, "A menu opened that the player never asked for: " + Describe());
            if (g.GameOver)
            {
                Phone.Stick(Layout, null);
                if (!_counted) { _counted = true; Deaths++; Note("defeated, tapping RESTART"); }
                if (Frames - _lastRestartTap >= 10) { _lastRestartTap = Frames; Phone.Tap(Layout, Layout.RestartRect.center); }
                return true;
            }
            _counted = false;
            if (g.ArrivalScenePending)
            {
                Phone.Stick(Layout, null);
                // A person reads each line and taps on; the scene ignores taps that come before a line was readable.
                if (Frames - _lastSceneTap >= 12) { _lastSceneTap = Frames; Phone.Tap(Layout, new Vector2(Layout.Width * .5f, Layout.Height * .4f)); }
                return true;
            }
            return false;
        }

        /// <summary>Wait until no interrupting screen is up any more.</summary>
        public IEnumerator Settle()
        {
            int start = Frames;
            while (Interrupted() || Phone.Busy)
            {
                if (Frames - start > 1200) Assert.Fail("A scene or defeat screen never ended: " + Describe());
                yield return Frame();
            }
        }

        /// <summary>
        /// Walk with the stick until <paramref name="goal"/> is within <paramref name="reachMm"/>. Ends early when the
        /// world changed (travel, restart) or <paramref name="stop"/> holds.
        /// </summary>
        public IEnumerator WalkTo(string what, Func<Vector2Int> goal, long reachMm, Func<bool> stop = null)
        {
            ArenaSimulation world = Game;
            List<Vector2Int> path = null;
            int plannedAt = -1000, progressAt = Frames, replans = 0;
            long best = long.MaxValue;
            Vector2Int plannedFor = goal();
            while (true)
            {
                if (Interrupted()) { path = null; yield return Frame(); world = Game; continue; }
                var g = Game;
                if (!ReferenceEquals(g, world) || (stop != null && stop())) { Phone.Stick(Layout, null); yield break; }
                Vector2Int target = goal();
                long dx = target.x - g.PlayerXmm, dz = target.y - g.PlayerZmm, d2 = dx * dx + dz * dz;
                if (d2 <= reachMm * reachMm)
                {
                    Phone.Stick(Layout, null);
                    yield return Frame();
                    yield break;
                }
                if (d2 < best - 300L * 300L) { best = d2; progressAt = Frames; replans = 0; }
                long moved = (long)(target - plannedFor).sqrMagnitude;
                bool stuck = Frames - progressAt > 25;
                if (path == null || Frames - plannedAt > 40 || moved > 2000L * 2000L || stuck)
                {
                    if (stuck)
                    {
                        replans++; progressAt = Frames;
                        if (replans > 6) Assert.Fail(what + ": the way stays blocked: " + Describe());
                    }
                    path = _navigator.Plan(g, g.PlayerXmm, g.PlayerZmm, target.x, target.y);
                    Assert.IsNotNull(path, what + ": no walkable way to (" + target.x + ", " + target.y + "): " + Describe());
                    plannedAt = Frames; plannedFor = target;
                }
                Vector2Int next = _navigator.Next(path, g.PlayerXmm, g.PlayerZmm, target);
                Steer(next.x - g.PlayerXmm, next.y - g.PlayerZmm);
                yield return Frame();
            }
        }

        /// <summary>Turn a wish in world directions into a stick push, seen through the current camera.</summary>
        private void Steer(long worldX, long worldZ)
        {
            if (worldX == 0 && worldZ == 0) { Phone.Stick(Layout, null); return; }
            var rig = Read<ArenaCameraRig>("_cameraRig");
            float yaw = rig != null ? rig.Yaw * Mathf.Deg2Rad : 0f, cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);
            // The game turns the stick by the camera yaw (HeroCameraPresentation.CameraRelativeMove); undo that turn.
            float x = worldX * cos - worldZ * sin, z = worldX * sin + worldZ * cos;
            Phone.Stick(Layout, new Vector2(x, z).normalized);
        }

        private static long Distance2(long ax, long az, long bx, long bz) { return (ax - bx) * (ax - bx) + (az - bz) * (az - bz); }

        /// <summary>Fight every living enemy of this area with the four skill buttons, dodging the bear's red swipe.</summary>
        public IEnumerator FightAll(string what, Func<MonsterState, bool> counts = null)
        {
            ArenaSimulation world = Game;
            int start = Frames, plannedAt = -1000;
            List<Vector2Int> path = null;
            while (true)
            {
                if (Interrupted()) { path = null; yield return Frame(); continue; }
                var g = Game;
                if (!ReferenceEquals(g, world)) { world = g; path = null; continue; } // a restart puts the enemies back: fight on
                MonsterState target = null; long best = long.MaxValue;
                foreach (var m in g.Monsters)
                {
                    if (m.IsDead || (counts != null && !counts(m))) continue;
                    long d2 = Distance2(m.Xmm, m.Zmm, g.PlayerXmm, g.PlayerZmm);
                    if (d2 < best) { best = d2; target = m; }
                }
                if (target == null) { Phone.Stick(Layout, null); Note(what + " won"); yield return Frame(); yield break; }
                if (Frames - start > 3000) Assert.Fail(what + ": the fight does not end: " + Describe());
                var road = g.Progression.Road;
                if (target.Kind == 4 && target.BossWindupTicks > 0 && road.StrikePlayer &&
                    Distance2(g.PlayerXmm, g.PlayerZmm, road.StrikeX, road.StrikeZ) <= 3200L * 3200L)
                {
                    // The red swipe lands where it was aimed: step out of it, sideways from the bear.
                    long ax = g.PlayerXmm - road.StrikeX, az = g.PlayerZmm - road.StrikeZ;
                    if (ax == 0 && az == 0) { ax = g.PlayerXmm - target.Xmm; az = g.PlayerZmm - target.Zmm; long t = ax; ax = -az; az = t; }
                    if (ax == 0 && az == 0) ax = 1;
                    Steer(ax, az);
                    // Combat v2: tap AUSWEICHEN while the stick points away; the game dodges along the stick.
                    if (g.CombatV2 && g.ActionKind != CombatActionKind.Dodge && g.Stamina >= CombatV2Rules.Dodge.Stamina && !Phone.Busy)
                        Phone.Tap(Layout, Layout.DodgeRect.center);
                    yield return Frame();
                    continue;
                }
                long distance = (long)Math.Sqrt(best);
                if (distance > 2300)
                {
                    // Around pillars and walls the way to the enemy is walked, not pushed straight into stone.
                    var goal = new Vector2Int((int)target.Xmm, (int)target.Zmm);
                    if (path == null || Frames - plannedAt > 15)
                    { path = _navigator.Plan(g, g.PlayerXmm, g.PlayerZmm, goal.x, goal.y); plannedAt = Frames; }
                    Vector2Int next = path != null ? _navigator.Next(path, g.PlayerXmm, g.PlayerZmm, goal) : goal;
                    Steer(next.x - g.PlayerXmm, next.y - g.PlayerZmm);
                }
                else Phone.Stick(Layout, null);
                if (g.CombatV2 && g.ActionKind == CombatActionKind.None && !Phone.Busy &&
                    g.PlayerHealth * 100 < g.MaxPlayerHealth * 45 && g.Progression.Crafting.PotionCount(PotionType.Heal) > 0)
                    Phone.Tap(Layout, Layout.FlaskRect.center); // low on health: drink while the hands are free
                int slot = ChooseSkill(g, distance, target);
                if (slot >= 0 && !Phone.Busy) Phone.Tap(Layout, Layout.SkillRect(slot).center);
                yield return Frame();
            }
        }

        /// <summary>The button a player would press now: heal when low, otherwise the strongest ready attack in reach.</summary>
        private static int ChooseSkill(ArenaSimulation g, long distance, MonsterState target)
        {
            // Combat v2: a swing must end before the next one, slot 2 guards instead of healing, swings cost stamina, and
            // Ruin's long windup is only started when it ends before the bear can strike again.
            if (g.CombatV2 && (g.ActionKind != CombatActionKind.None || g.Stamina <= 0)) return -1;
            if (!g.CombatV2 && g.PlayerHealth * 100 < g.MaxPlayerHealth * 45 && g.Cooldowns[2] == 0) return 2;
            bool ruinSafe = !g.CombatV2 || target.Kind != 4 || target.AttackCooldownTicks > CombatV2Rules.Ruin.Startup;
            int near = 0;
            foreach (var m in g.Monsters) if (!m.IsDead && Distance2(m.Xmm, m.Zmm, g.PlayerXmm, g.PlayerZmm) <= 6000L * 6000L) near++;
            if (g.Cooldowns[3] == 0 && ruinSafe && distance <= 5500 && (near >= 2 || target.Kind == 4)) return 3;
            if (g.Cooldowns[1] == 0 && g.Resource >= SkillCatalog.Get(1).FocusCost && distance <= 4000) return 1;
            if (g.Cooldowns[0] == 0 && distance <= 2900) return 0;
            return -1;
        }

        /// <summary>
        /// Walk to every drop on the ground and tap the pickup button beside it: the weapons that fell and the supply
        /// bags (loot revision 2), nearest first.
        /// </summary>
        public IEnumerator PickUpAll()
        {
            int start = Frames;
            while (Game.LootOnGround)
            {
                if (Frames - start > 2000) Assert.Fail("Loot could not be collected: " + Describe());
                var g = Game;
                long best = long.MaxValue, x = 0, z = 0;
                foreach (var l in g.GroundLoot)
                {
                    long d2 = Distance2(l.Xmm, l.Zmm, g.PlayerXmm, g.PlayerZmm);
                    if (d2 < best) { best = d2; x = l.Xmm; z = l.Zmm; }
                }
                foreach (var bag in g.GroundSupplies)
                {
                    long d2 = Distance2(bag.Xmm, bag.Zmm, g.PlayerXmm, g.PlayerZmm);
                    if (d2 < best) { best = d2; x = bag.Xmm; z = bag.Zmm; }
                }
                var spot = new Vector2Int((int)x, (int)z);
                yield return WalkTo("loot", () => spot, 900);
                int before = Game.GroundLoot.Count + Game.GroundSupplies.Count;
                Phone.Tap(Layout, Layout.PickupRect.center);
                for (int i = 0; i < 6 && Game.GroundLoot.Count + Game.GroundSupplies.Count == before; i++) yield return Frame();
                if (Game.GroundLoot.Count + Game.GroundSupplies.Count < before)
                    Note("picked up loot (" + Game.Progression.Items.Count + " items carried, " + Game.Progression.Social.Provisions + " rations)");
            }
        }

        /// <summary>Open the SPUREN journal, tap the offered story button, read the answer, close the journal.</summary>
        public IEnumerator Story(HungerAction action, Func<Vector2Int> where, long reachMm, Func<bool> done)
        {
            for (int attempt = 0; attempt < 6 && !done(); attempt++)
            {
                yield return Settle();
                yield return WalkTo(action.ToString(), where, Math.Max(600, reachMm - attempt * 300));
                yield return Settle();
                if (!JournalOpen) Phone.Tap(Layout, ArenaRuntime.HungerToggleRect(Layout).center);
                for (int i = 0; i < 8 && !JournalOpen; i++) yield return Frame();
                Assert.IsTrue(JournalOpen, "The SPUREN button did not open the journal: " + Describe());
                var offered = OfferedStory;
                int index = Array.IndexOf(offered, action);
                Assert.GreaterOrEqual(index, 0, "The journal does not offer " + action + " (offered: " + string.Join(", ", offered) + "): " + Describe());
                var rect = (Rect)typeof(ArenaRuntime).GetMethod("HungerActionRect", PrivateStatic).Invoke(null, new object[] { Layout, index });
                Phone.Tap(Layout, rect.center); StoryTaps++;
                for (int i = 0; i < 12 && (Phone.Busy || PendingStory >= 0); i++) yield return Frame();
                yield return Frame();
                Note(action + (done() ? " accepted: " : " refused: ") + Message);
                Phone.Tap(Layout, ArenaRuntime.HungerToggleRect(Layout).center);
                for (int i = 0; i < 8 && JournalOpen; i++) yield return Frame();
                Assert.IsFalse(JournalOpen, "The journal did not close: " + Describe());
            }
            Assert.IsTrue(done(), action + " never took effect: " + Describe());
        }

        /// <summary>Walk into a passage and let the game travel, save and rebuild the next area on its own.</summary>
        public IEnumerator Travel(string what, long z, int expectedAct) { return Travel(what, 0, z, expectedAct); }

        /// <summary>Walk into the passage at (<paramref name="x"/>, <paramref name="z"/>), as Aschenwacht's bell tower door.</summary>
        public IEnumerator Travel(string what, long x, long z, int expectedAct)
        {
            ArenaSimulation world = Game;
            for (int attempt = 0; attempt < 4 && ReferenceEquals(Game, world); attempt++)
            {
                yield return Settle();
                yield return WalkTo(what, () => new Vector2Int((int)x, (int)z), 400, () => !ReferenceEquals(Game, world));
                for (int i = 0; i < 6 && ReferenceEquals(Game, world); i++) yield return Frame();
                if (!ReferenceEquals(Game, world) && Game.GameOver) { yield return Settle(); world = Game; }
            }
            Assert.IsFalse(ReferenceEquals(Game, world), what + ": the passage did not take the player on: " + Describe());
            Assert.AreEqual(expectedAct, Game.Progression.EntryAct, what + ": arrived in the wrong area: " + Describe());
            ArrivalX = Game.PlayerXmm; ArrivalZ = Game.PlayerZmm;
            Note("arrived");
        }

        /// <summary>Help the hunter: fight the bear to the end, then tap the hunter's button to talk to him.</summary>
        public IEnumerator RescueHunter()
        {
            var road = Game.Progression.Road;
            yield return WalkTo("hunter", () => new Vector2Int((int)HunterEncounter.HunterX, (int)HunterEncounter.HunterZ - 4000), 1500,
                () => Game.Progression.Road.Started);
            yield return FightAll("bear", m => m.Kind == 4);
            road = Game.Progression.Road;
            Assert.IsTrue(road.Rescued, "The hunter did not survive the fight: " + Describe());
            yield return WalkTo("hunter", () => new Vector2Int((int)HunterEncounter.HunterX, (int)HunterEncounter.HunterZ), 1500);
            for (int attempt = 0; attempt < 4 && !Game.Progression.Road.FavorClaimed; attempt++)
            {
                yield return Settle();
                if (!HunterButtonShown) { yield return WalkTo("hunter", () => new Vector2Int((int)HunterEncounter.HunterX, (int)HunterEncounter.HunterZ), 900); continue; }
                Phone.Tap(Layout, Layout.ContextActionRect.center);
                for (int i = 0; i < 10 && !Game.Progression.Road.FavorClaimed; i++) yield return Frame();
            }
            Assert.IsTrue(Game.Progression.Road.FavorClaimed, "Talking to the rescued hunter had no effect: " + Describe());
            Note("hunter rescued and thanked: " + Message);
        }

        /// <summary>
        /// A* over the area's own collision (Core CanStand) on a half-metre grid; house doors count as walls. Aschenwacht
        /// reaches far beyond the arena: there the route is the place's own (CourtLayout.Route).
        /// </summary>
        private sealed class Navigator
        {
            private const long Cell = 500, Limit = ArenaSimulation.ArenaLimitMm;
            private const int Size = (int)(2 * Limit / Cell) + 1;
            private ArenaSimulation _world;
            private VillagePlace _place;
            private bool[] _open;
            private bool _court, _avoid;
            private long _avoidX, _avoidZ;
            private const long AvoidMm = 2400;

            private static long Coord(int i) { return -Limit + i * Cell; }
            private static int Index(long v) { return (int)Math.Max(0, Math.Min(Size - 1, (v + Limit + Cell / 2) / Cell)); }

            private void Prepare(ArenaSimulation g)
            {
                if (ReferenceEquals(g, _world) && g.InsidePlace == _place) return;
                _world = g; _place = g.InsidePlace; _open = new bool[Size * Size];
                for (int j = 0; j < Size; j++)
                    for (int i = 0; i < Size; i++)
                        _open[j * Size + i] = g.CanStand(Coord(i), Coord(j)) && !DoorNear(g, Coord(i), Coord(j));
            }

            /// <summary>A built house's doorway pulls the player inside; the route keeps clear of it.</summary>
            private static bool DoorNear(ArenaSimulation g, long x, long z)
            {
                if (g.Progression.EntryAct != 2 || g.InsidePlace != VillagePlace.None) return false;
                for (long ox = -400; ox <= 400; ox += 400)
                    for (long oz = -400; oz <= 400; oz += 400)
                    {
                        var place = VillagePlaceLayout.DoorAt(x + ox, z + oz);
                        if (place != VillagePlace.None && g.Progression.Houses.Built(place)) return true;
                    }
                return false;
            }

            private bool Open(int i, int j) { return i >= 0 && j >= 0 && i < Size && j < Size && _open[j * Size + i]; }

            private int Nearest(int i, int j)
            {
                for (int r = 0; r < 12; r++)
                    for (int dj = -r; dj <= r; dj++)
                        for (int di = -r; di <= r; di++)
                            if ((Math.Abs(di) == r || Math.Abs(dj) == r) && Open(i + di, j + dj)) return (j + dj) * Size + i + di;
                return -1;
            }

            public List<Vector2Int> Plan(ArenaSimulation g, long fromX, long fromZ, long toX, long toZ)
            {
                _court = g.InCourt;
                if (_court)
                {
                    // The living Aschbeter is passed at 2.4 m, beyond his strike; the straight shortcuts keep that distance too.
                    var a = g.Aschbeter;
                    _avoid = a != null && !a.Down;
                    if (_avoid) { _avoidX = a.Xmm; _avoidZ = a.Zmm; }
                    long[] route = _avoid ? CourtLayout.Route(fromX, fromZ, toX, toZ, _avoidX, _avoidZ, AvoidMm) : CourtLayout.Route(fromX, fromZ, toX, toZ);
                    if (route.Length == 0) return null;
                    var way = new List<Vector2Int>();
                    for (int k = 0; k < route.Length / 2; k += 2) way.Add(new Vector2Int((int)route[2 * k], (int)route[2 * k + 1]));
                    way.Add(new Vector2Int((int)route[route.Length - 2], (int)route[route.Length - 1]));
                    return way;
                }
                Prepare(g);
                int start = Nearest(Index(fromX), Index(fromZ)), goal = Nearest(Index(toX), Index(toZ));
                if (start < 0 || goal < 0) return null;
                var cost = new int[Size * Size]; var from = new int[Size * Size];
                for (int k = 0; k < cost.Length; k++) cost[k] = int.MaxValue;
                var heap = new List<KeyValuePair<int, int>>();
                cost[start] = 0; from[start] = -1; Push(heap, Heuristic(start, goal), start);
                while (heap.Count > 0)
                {
                    int node = Pop(heap);
                    if (node == goal) break;
                    int ni = node % Size, nj = node / Size;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            if (di == 0 && dj == 0 || !Open(ni + di, nj + dj)) continue;
                            if (di != 0 && dj != 0 && (!Open(ni + di, nj) || !Open(ni, nj + dj))) continue; // no corner cutting
                            int next = (nj + dj) * Size + ni + di, step = di != 0 && dj != 0 ? 14 : 10;
                            if (cost[node] + step >= cost[next]) continue;
                            cost[next] = cost[node] + step; from[next] = node;
                            Push(heap, cost[next] + Heuristic(next, goal), next);
                        }
                }
                if (cost[goal] == int.MaxValue) return null;
                var path = new List<Vector2Int>();
                for (int node = goal; node >= 0; node = from[node]) path.Add(new Vector2Int((int)Coord(node % Size), (int)Coord(node / Size)));
                path.Reverse();
                return path;
            }

            /// <summary>The furthest of the next waypoints in straight open sight; the real target once the path is used up.</summary>
            public Vector2Int Next(List<Vector2Int> path, long x, long z, Vector2Int target)
            {
                while (path.Count > 1 && Distance2(path[0].x, path[0].y, x, z) < 700L * 700L) path.RemoveAt(0);
                if (path.Count <= 1 && Straight(x, z, target.x, target.y)) return target;
                int best = 0;
                for (int k = 1; k < Math.Min(path.Count, 10); k++) { if (Straight(x, z, path[k].x, path[k].y)) best = k; else break; }
                return path[best];
            }

            private bool Straight(long ax, long az, long bx, long bz)
            {
                long length = (long)Math.Sqrt(Distance2(ax, az, bx, bz));
                for (long s = 0; s <= length; s += 250)
                {
                    long x = ax + (bx - ax) * s / Math.Max(1, length), z = az + (bz - az) * s / Math.Max(1, length);
                    if (_court ? !CourtLayout.CanStand(x, z) || (_avoid && Distance2(x, z, _avoidX, _avoidZ) <= AvoidMm * AvoidMm) : !Open(Index(x), Index(z))) return false;
                }
                return true;
            }

            private static int Heuristic(int a, int b)
            {
                int dx = Math.Abs(a % Size - b % Size), dz = Math.Abs(a / Size - b / Size);
                return 10 * Math.Max(dx, dz) + 4 * Math.Min(dx, dz);
            }

            private static void Push(List<KeyValuePair<int, int>> heap, int priority, int node)
            {
                heap.Add(new KeyValuePair<int, int>(priority, node));
                for (int c = heap.Count - 1; c > 0;)
                {
                    int p = (c - 1) / 2;
                    if (heap[p].Key <= heap[c].Key) break;
                    var t = heap[p]; heap[p] = heap[c]; heap[c] = t; c = p;
                }
            }

            private static int Pop(List<KeyValuePair<int, int>> heap)
            {
                int top = heap[0].Value;
                heap[0] = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
                for (int p = 0; ;)
                {
                    int l = 2 * p + 1, r = l + 1, m = p;
                    if (l < heap.Count && heap[l].Key < heap[m].Key) m = l;
                    if (r < heap.Count && heap[r].Key < heap[m].Key) m = r;
                    if (m == p) break;
                    var t = heap[p]; heap[p] = heap[m]; heap[m] = t; p = m;
                }
                return top;
            }
        }
    }
}
