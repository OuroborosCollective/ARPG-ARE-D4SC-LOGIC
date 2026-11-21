using System;
using System.Collections.Generic;
using System.Linq;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    /// <summary>
    /// The working day of a village resident as a reviewed controller: version 2 of "ashen.villager".
    ///
    /// Version 1 was written for villagers who stand, rest and eat; its "go about" rule gives way to the slightest
    /// hunger, so nobody would work on it. Version 2 keeps the same archetype - the same permitted actions and the
    /// same inputs - and weighs them for a day of work: go about the work while fed and fit, take a short rest when
    /// tired, eat when hungry, stay down when hurt. It is not selected for new villagers: the economy moves each
    /// working resident to it by the archive's recorded controller migration, so every change of mind has a receipt.
    /// A weight edit needs a new version and a new reviewed hash.
    /// </summary>
    public static class VillageWorkController
    {
        public const string ControllerId = "ashen.villager";
        public const int Version = 2;
        public const string Suite = "ashen.village-work-v1";
        /// <summary>SHA-256 of the canonical controller text; registration fails if the weights below change.</summary>
        public const string ReviewedHash = "3aa226c67b2755d98a6a33a3e5e5896f1c9bc945031e9da098ffac96bd980372";

        private static UtilityRule Rule(ForgeAction action, int priority, params (Feature feature, int weight)[] values)
        {
            var weights = new int[(int)Feature.Count];
            foreach (var value in values) weights[(int)value.feature] = value.weight;
            return new UtilityRule(action, (int)action, priority, weights);
        }

        /// <summary>The registry of the game: the package's reviewed controllers plus the working villager.</summary>
        internal static NpcArchetypeControllerRegistry CreateRegistry()
        {
            var registry = ForgeDefaults.CreateRegistry();
            Register(registry);
            return registry;
        }

        internal static ControllerArtifact Register(NpcArchetypeControllerRegistry registry)
        {
            // The archetype is taken from the reviewed version 1: the same manifest, or the migration is refused.
            ArchetypeDefinition villager = registry.Versions.First(v => v.ArchetypeId == "villager" && v.Version == 1).Archetype;
            var rules = new List<UtilityRule>
            {
                Rule(ForgeAction.Wait, 0),
                // Work: wanted while fed and rested, less with every bit of fatigue, and not at all when hurt.
                Rule(ForgeAction.Travel, 2400, (Feature.Fatigue, -9000), (Feature.Hunger, -2500), (Feature.Injury, -12000), (Feature.TravelCost, -300)),
                Rule(ForgeAction.Rest, 300, (Feature.Fatigue, 12000), (Feature.Injury, 8000), (Feature.Danger, -12000)),
                Rule(ForgeAction.SeekFood, 0, (Feature.Hunger, 12000), (Feature.TravelCost, -1500)),
                Rule(ForgeAction.Eat, 100, (Feature.Hunger, 15000), (Feature.Food, 1000)),
                Rule(ForgeAction.Flee, 1000, (Feature.Danger, 16000), (Feature.Injury, 10000), (Feature.Fear, 7000)),
                Rule(ForgeAction.Observe, 700, (Feature.Trust, 4000), (Feature.Gratitude, 8000), (Feature.Quest, 4000), (Feature.Danger, -10000)),
            };
            var artifact = new ControllerArtifact(villager, ControllerId, Version, rules);
            var cases = new List<RegressionCase>
            {
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait), Array.Empty<Observation>()), ForgeAction.Wait),
                new RegressionCase(new ObservationFrame(0, Array.Empty<Observation>()), ForgeAction.None),
                // Fed and rested with work to do: work.
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Rest, ForgeAction.Travel),
                    new[] { new Observation(Feature.Hunger, 3000), new Observation(Feature.Fatigue, 300) }), ForgeAction.Travel),
                // Mildly hungry is no reason to stop: version 1 stopped here.
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Rest, ForgeAction.Travel),
                    new[] { new Observation(Feature.Hunger, 6000), new Observation(Feature.Fatigue, 200) }), ForgeAction.Travel),
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Rest, ForgeAction.Travel),
                    new[] { new Observation(Feature.Fatigue, 10000) }), ForgeAction.Rest),
                // Hungry enough to eat and food is there: the meal comes before the work.
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Rest, ForgeAction.Travel, ForgeAction.Eat),
                    new[] { new Observation(Feature.Hunger, 7000), new Observation(Feature.Food, 10000) }), ForgeAction.Eat),
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Rest, ForgeAction.Travel),
                    new[] { new Observation(Feature.Injury, 4000) }), ForgeAction.Rest),
                // Nothing to do in the village by day: be among the others.
                new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Rest, ForgeAction.Observe), Array.Empty<Observation>()), ForgeAction.Observe),
            };
            var admission = ControllerAdmission.Run(artifact, Suite, cases);
            registry.Register(artifact, ReviewedHash, admission, selectForNewSpawns: false);
            _artifact = artifact;
            return artifact;
        }

        private static ControllerArtifact _artifact;

        /// <summary>The working controller as registered in the game's registry.</summary>
        internal static ControllerArtifact Artifact
        {
            get
            {
                if (_artifact == null && NpcForgeState.Registry == null) throw new InvalidOperationException("Controller registry is missing.");
                return _artifact;
            }
        }
    }
}
