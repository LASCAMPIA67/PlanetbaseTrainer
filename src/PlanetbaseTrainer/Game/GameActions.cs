using System;
using System.Collections.Generic;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using UnityEngine;
using SteamUserStats = Steamworks.SteamUserStats;

namespace PlanetbaseTrainer.Game
{
    /// <summary>
    /// One-shot operations on the running game. Everything goes through the game's own APIs
    /// (Character.create, Resource.create + StorageSlot, Buildable.updateBuild...) so saves stay 100% vanilla.
    /// </summary>
    internal static class GameActions
    {
        public static readonly int[] SpawnCounts = { 1, 3, 5, 10 };
        public static readonly int[] ResourceAmounts = { 10, 25, 50, 100, 250 };
        public static readonly int[] CoinAmounts = { 100, 500, 1000, 5000 };

        /// <summary>Upper bound of resources created per refill tick (keeps the frame time smooth).</summary>
        private const int RefillBudgetPerTick = 40;

        // ================================================================== Colonists & bots

        public static ActionResult HealEveryone()
        {
            int healed = 0;
            var characters = G.Characters;
            if (characters != null)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    var character = characters[i];
                    if (character == null || character.isDead() || !G.IsFriendlyHuman(character))
                        continue;
                    G.FillAll(character);
                    ((Human)character).clearCondition();
                    healed++;
                }
            }
            return healed > 0 ? ActionResult.Ok(Loc.F("res.healed", healed)) : ActionResult.Fail(Loc.T("res.nothing"));
        }

        public static void CureEveryone()
        {
            var characters = G.Characters;
            if (characters == null)
                return;
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i] is Human human && !human.isDead() && G.IsFriendlyHuman(human))
                    human.clearCondition();
            }
        }

        public static ActionResult RepairBots()
        {
            int repaired = 0;
            var characters = G.Characters;
            if (characters != null)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    if (!(characters[i] is Bot bot) || bot.isDead())
                        continue;
                    G.Fill(G.GetIndicator(bot, CharacterIndicator.Condition));
                    G.Fill(G.GetIndicator(bot, CharacterIndicator.Integrity));
                    if (bot.isKo())
                        bot.setIdle();
                    repaired++;
                }
            }
            return repaired > 0 ? ActionResult.Ok(Loc.F("res.botsrepaired", repaired)) : ActionResult.Fail(Loc.T("res.nothing"));
        }

        public static ActionResult SpawnColonists() =>
            Spawn(SpecializationCatalog.Colonists[Cheats.ColonistType.Value], SpawnCounts[Cheats.ColonistCount.Value]);

        public static ActionResult SpawnBots() =>
            Spawn(SpecializationCatalog.Bots[Cheats.BotType.Value], SpawnCounts[Cheats.BotCount.Value]);

        private static ActionResult Spawn(string typeName, int count)
        {
            var specialization = SpecializationCatalog.Resolve(typeName);
            if (specialization == null)
                return ActionResult.Fail(Loc.F("res.failed", typeName));
            if (!TryGetSpawnPoint(out var origin))
                return ActionResult.Fail(Loc.T("res.nospawnpoint"));
            for (int i = 0; i < count; i++)
            {
                // Same pattern as the game's landing ships: a small grid of positions around the pad.
                var offset = new Vector3((i % 5 - 2) * 0.9f, 0f, i / 5 * 0.9f) + MathUtil.randFlatVector(0.2f);
                Character.create(specialization, origin + offset, Location.Exterior);
            }
            return ActionResult.Ok(Loc.F("res.spawned", count, SpecializationCatalog.Name(typeName)));
        }

        /// <summary>Exterior position where characters / resources can appear: landing pad, else in front of an airlock.</summary>
        private static bool TryGetSpawnPoint(out Vector3 position)
        {
            var center = Construction.getCount() > 0 ? Construction.getAveragePosition() : Vector3.zero;
            var pad = Module.findOperational(center, ModuleType.FlagLandingPad) ??
                      Module.findOperational(center, ModuleType.FlagStarport);
            if (pad != null)
            {
                position = pad.getPosition() + MathUtil.randFlatVector(pad.getRadius() * 0.3f);
                return true;
            }
            var airlocks = Module.getCategoryModules(Module.Category.Airlock);
            if (airlocks != null)
            {
                for (int i = 0; i < airlocks.Count; i++)
                {
                    var airlock = airlocks[i];
                    if (airlock == null || !airlock.isBuilt())
                        continue;
                    var outward = airlock.getPosition() - center;
                    outward.y = 0f;
                    if (outward.sqrMagnitude < 0.01f)
                        outward = Vector3.forward;
                    position = airlock.getPosition() + outward.normalized * (airlock.getRadius() + 2.5f);
                    return true;
                }
            }
            position = default;
            return false;
        }

        // ================================================================== Construction

        /// <summary>Completes every construction that is waiting for materials or a builder.</summary>
        public static int CompleteConstructions()
        {
            var constructions = G.Constructions;
            if (constructions == null)
                return 0;
            int completed = 0;
            // Pass 0: modules, pass 1: connections (they need both modules built first).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < constructions.Count; i++)
                {
                    var construction = constructions[i];
                    if (construction == null || construction.isDestroyed() || construction.isBuilt())
                        continue;
                    bool isConnection = construction is Connection;
                    if (isConnection != (pass == 1))
                        continue;
                    if (construction is Connection connection)
                    {
                        var m1 = connection.getModule1();
                        var m2 = connection.getModule2();
                        if (m1 == null || m2 == null || !m1.isBuilt() || !m2.isBuilt())
                            continue;
                    }
                    if (TryComplete(construction))
                        completed++;
                }
            }
            // Pass 2: equipment inside built modules.
            for (int i = 0; i < constructions.Count; i++)
            {
                if (!(constructions[i] is Module module) || !module.isBuilt())
                    continue;
                var components = module.getComponents();
                for (int j = 0; j < components.Count; j++)
                {
                    var component = components[j];
                    if (component != null && !component.isDestroyed() && !component.isBuilt() && TryComplete(component))
                        completed++;
                }
            }
            return completed;
        }

        /// <summary>
        /// Finishes a buildable through the game's own build path (startBuilding + updateBuild → onBuilt).
        /// Buildables currently worked on are skipped: their builder finishes them instantly through the build patch,
        /// which avoids completing the same buildable twice.
        /// </summary>
        public static bool TryComplete(Buildable buildable)
        {
            var state = buildable.getState();
            if (state != BuildableState.Placed && state != BuildableState.Building)
                return false;
            if (buildable.anyInteractions())
                return false;
            try
            {
                buildable.getPendingConstructionCosts()?.clear();
                buildable.startBuilding();
                for (int guard = 0; guard < 64 && !buildable.isBuilt(); guard++)
                    buildable.updateBuild(10000f);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("complete." + buildable.GetType().Name, ex);
                return false;
            }
            return buildable.isBuilt();
        }

        /// <summary>Removes the material requirements of every construction site (used when "Free construction" is enabled).</summary>
        public static void ClearPendingCosts()
        {
            var constructions = G.Constructions;
            if (constructions == null)
                return;
            for (int i = 0; i < constructions.Count; i++)
            {
                var construction = constructions[i];
                if (construction == null || construction.isDestroyed())
                    continue;
                if (!construction.isBuilt())
                    ClearPendingCosts(construction);
                if (construction is Module module && module.isBuilt())
                {
                    var components = module.getComponents();
                    for (int j = 0; j < components.Count; j++)
                    {
                        if (components[j] != null && !components[j].isBuilt())
                            ClearPendingCosts(components[j]);
                    }
                }
            }
        }

        private static void ClearPendingCosts(Buildable buildable)
        {
            var state = buildable.getState();
            if (state == BuildableState.Placed || state == BuildableState.Building)
                buildable.getPendingConstructionCosts()?.clear();
        }

        public static ActionResult RepairStructures()
        {
            var constructions = G.Constructions;
            if (constructions == null)
                return ActionResult.Fail(Loc.T("res.nothing"));
            int repaired = 0;
            for (int i = 0; i < constructions.Count; i++)
            {
                var construction = constructions[i];
                if (construction == null || construction.isDestroyed() || !construction.isBuilt())
                    continue;
                if (RepairConstruction(construction))
                    repaired++;
                if (construction is Module module)
                {
                    var components = module.getComponents();
                    for (int j = 0; j < components.Count; j++)
                    {
                        if (RepairComponent(components[j]))
                            repaired++;
                    }
                }
            }
            return repaired > 0 ? ActionResult.Ok(Loc.F("res.repaired", repaired)) : ActionResult.Fail(Loc.T("res.nothing"));
        }

        /// <summary>Uses Construction.repair so the game re-enables glass, unlocks doors and recomputes grids.</summary>
        public static bool RepairConstruction(Construction construction)
        {
            if (G.ConstructionCondition == null)
                return false;
            var condition = G.ConstructionCondition(construction);
            if (condition == null || !condition.isValidValue() || condition.getValue() >= condition.getMax())
                return false;
            construction.repair(condition.getMax());
            return true;
        }

        private static bool RepairComponent(ConstructionComponent component)
        {
            if (component == null || !component.isBuilt() || G.ComponentCondition == null)
                return false;
            var condition = G.ComponentCondition(component);
            if (condition == null || !condition.isValidValue() || condition.getValue() >= condition.getMax())
                return false;
            condition.setValue(condition.getMax());
            return true;
        }

        // ================================================================== Resources

        public static ActionResult AddResources()
        {
            int index = Cheats.AddResourceType.Value;
            int amount = ResourceAmounts[Cheats.AddResourceAmount.Value];
            var type = ResourceCatalog.Resolve(index);
            if (type == null)
                return ActionResult.Fail(Loc.F("res.failed", ResourceCatalog.TypeNames[index]));
            string name = ResourceCatalog.Name(index);

            int stored = AddToStorage(type, amount);
            int remaining = amount - stored;
            if (remaining <= 0)
                return ActionResult.Ok(Loc.F("res.resadded", amount, name));
            if (TryGetSpawnPoint(out var dropPoint))
            {
                // Same path as merchant deliveries: colonists carry them to storage when room is available.
                new ProductResource(type).instantiate(dropPoint, remaining);
                return ActionResult.Ok(Loc.F("res.resoverflow", amount, name, remaining));
            }
            return stored > 0
                ? ActionResult.Ok(Loc.F("res.resadded", stored, name))
                : ActionResult.Fail(Loc.T("res.nostorage"));
        }

        /// <summary>Creates resources directly inside free storage slots. Returns how many were stored.</summary>
        public static int AddToStorage(ResourceType type, int amount)
        {
            var storages = Module.getCategoryModules(Module.Category.Storage);
            if (storages == null || amount <= 0)
                return 0;
            var product = new ProductResource(type);
            int added = 0;
            for (int m = 0; m < storages.Count && added < amount; m++)
            {
                var storage = storages[m];
                if (storage == null || storage.isDestroyed() || !storage.isBuilt())
                    continue;
                while (added < amount)
                {
                    var slot = storage.findStorageSlot(storage.getPosition());
                    if (slot == null)
                        break;
                    var resource = Resource.create(type, product.getSubtype(type), slot.getPosition(), Location.Interior);
                    slot.addResource(resource);
                    added++;
                }
            }
            return added;
        }

        public static ActionResult AddCoins()
        {
            int amount = CoinAmounts[Cheats.AddCoinsAmount.Value];
            Resource.addInmaterialResource(new ResourceAmount(ResourceTypeList.CoinsInstance, amount));
            return ActionResult.Ok(Loc.F("res.coins", amount));
        }

        /// <summary>"Unlimited resources" tick. Returns false when storage is full while something is still missing.</summary>
        public static bool Refill()
        {
            int target = Mathf.RoundToInt(Cheats.InfiniteTarget.Value);
            int budget = RefillBudgetPerTick;
            bool missingRoom = false;
            for (int i = 0; i < ResourceCatalog.Count && budget > 0; i++)
            {
                if (!Cheats.InfiniteTypes[i].Value)
                    continue;
                var type = ResourceCatalog.Resolve(i);
                if (type == null)
                    continue;
                int missing = target - Resource.getCountOfType(type);
                if (missing <= 0)
                    continue;
                int wanted = Math.Min(missing, budget);
                int added = AddToStorage(type, wanted);
                budget -= added;
                if (added < wanted)
                    missingRoom = true;
            }
            return !missingRoom;
        }

        // ================================================================== Ships

        public static ActionResult CallMerchant()
        {
            if (LandingShipManager.findLandingModule(LandingShip.Size.Regular) == null)
                return ActionResult.Fail(Loc.T("res.nolandingpad"));
            Singleton<LandingShipManager>.getInstance().trySpawnMerchantShip();
            return ActionResult.Ok(Loc.F("res.shipcalled", Loc.T("ship.merchant")));
        }

        public static ActionResult CallColonists()
        {
            if (LandingShipManager.findLandingModule(LandingShip.Size.Regular) == null)
                return ActionResult.Fail(Loc.T("res.nolandingpad"));
            Singleton<LandingShipManager>.getInstance().trySpawnColonistShip();
            return ActionResult.Ok(Loc.F("res.shipcalled", Loc.T("ship.colonists")));
        }

        public static ActionResult CallVisitors()
        {
            var starport = TypeList<ModuleType, ModuleTypeList>.find<ModuleTypeStarport>();
            if (starport == null || !Module.isModuleTypeBuilt(starport) ||
                LandingShipManager.findLandingModule(LandingShip.Size.Regular) == null)
                return ActionResult.Fail(Loc.T("res.nostarport"));
            Singleton<LandingShipManager>.getInstance().trySpawnVisitorShip();
            return ActionResult.Ok(Loc.F("res.shipcalled", Loc.T("ship.visitors")));
        }

        // ================================================================== Threats

        public static ActionResult KillIntruders()
        {
            int killed = 0;
            var characters = G.Characters;
            if (characters != null)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    var character = characters[i];
                    if (character == null || character.isDead() || !G.IsIntruder(character))
                        continue;
                    // The game's own death logic (KO, then death message) runs on the next tick.
                    character.decayIndicator(CharacterIndicator.Health, 1f);
                    killed++;
                }
            }
            return killed > 0 ? ActionResult.Ok(Loc.F("res.intruders", killed)) : ActionResult.Fail(Loc.T("res.nointruders"));
        }

        public static ActionResult StopDisasters()
        {
            var manager = Singleton<DisasterManager>.mInstance;
            if (manager == null)
                return ActionResult.Fail(Loc.T("res.nodisaster"));
            int stopped = 0;
            if (DisasterControl.ForceEnd(manager.getSandstorm())) stopped++;
            if (DisasterControl.ForceEnd(manager.getBlizzard())) stopped++;
            if (DisasterControl.ForceEnd(manager.getSolarFlare())) stopped++;
            return stopped > 0 ? ActionResult.Ok(Loc.F("res.disasterstopped", stopped)) : ActionResult.Fail(Loc.T("res.nodisaster"));
        }

        public enum DisasterKind { Sandstorm, Blizzard, SolarFlare }

        public static ActionResult TriggerDisaster(DisasterKind kind)
        {
            var manager = Singleton<DisasterManager>.mInstance;
            var planet = PlanetManager.getCurrentPlanet();
            if (manager == null || planet == null)
                return ActionResult.Fail(Loc.T("res.nothing"));

            Disaster disaster;
            Planet.Quantity risk;
            BoolOption blockedBy;
            string nameKey;
            switch (kind)
            {
                case DisasterKind.Sandstorm:
                    disaster = manager.getSandstorm();
                    risk = planet.getSandstormRisk();
                    blockedBy = Cheats.NoSandstorms;
                    nameKey = "dis.sandstorm";
                    break;
                case DisasterKind.Blizzard:
                    disaster = manager.getBlizzard();
                    risk = planet.getBlizzardRisk();
                    blockedBy = Cheats.NoBlizzards;
                    nameKey = "dis.blizzard";
                    break;
                default:
                    disaster = manager.getSolarFlare();
                    risk = planet.getSolarFlareRisk();
                    blockedBy = Cheats.NoSolarFlares;
                    nameKey = "dis.solarflare";
                    break;
            }
            // Without risk the game never updates (and so never ends) this disaster: refuse.
            if (risk == Planet.Quantity.None)
                return ActionResult.Fail(Loc.T("res.noplanetrisk"));
            if (Cheats.NoDisasters.On && blockedBy.Value)
                return ActionResult.Fail(Loc.T("res.disasterblocked"));
            if (disaster.isInProgress())
                return ActionResult.Fail(Loc.T("res.inprogress"));
            disaster.trigger();
            return ActionResult.Ok(Loc.F("res.disasterstarted", Loc.T(nameKey)));
        }

        public static ActionResult DropMeteor()
        {
            var meteors = Singleton<MeteorManager>.mInstance;
            if (meteors == null)
                return ActionResult.Fail(Loc.T("res.nothing"));
            var target = Construction.getCount() > 0 ? Construction.getAveragePosition() : Vector3.zero;
            var constructions = G.Constructions;
            if (constructions != null && constructions.Count > 0)
            {
                var picked = constructions[UnityEngine.Random.Range(0, constructions.Count)];
                if (picked != null && !picked.isDestroyed())
                    target = picked.getPosition();
            }
            meteors.spawnMeteor(target);
            return ActionResult.Ok(Loc.T("res.meteor"));
        }

        // ================================================================== Time & progression

        public static ActionResult SetTimeOfDay()
        {
            var environment = Singleton<EnvironmentManager>.mInstance;
            if (environment == null)
                return ActionResult.Fail(Loc.T("res.nothing"));
            float value = Cheats.TimeOfDay.Value;
            environment.setTimeOfDay(value);
            TimeControl.SetFrozen(value);
            return ActionResult.Ok(Loc.F("res.time", TimeControl.Clock(value)));
        }

        public static ActionResult UnlockTechs()
        {
            var techs = Singleton<TechManager>.mInstance;
            if (techs == null)
                return ActionResult.Fail(Loc.T("res.nothing"));
            int unlocked = 0;
            List<Tech> all = TypeList<Tech, TechList>.get();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || techs.isAcquired(all[i]))
                    continue;
                techs.acquire(all[i]);
                unlocked++;
            }
            return unlocked > 0 ? ActionResult.Ok(Loc.F("res.techs", unlocked)) : ActionResult.Fail(Loc.T("res.alltechs"));
        }

        public static ActionResult CompleteMilestones()
        {
            var milestones = Singleton<MilestoneManager>.mInstance;
            if (milestones == null)
                return ActionResult.Fail(Loc.T("res.nothing"));
            int completed = 0;
            List<Milestone> all = TypeList<Milestone, MilestoneList>.get();
            // Explicit user request: bypass the optional profile-progression protection.
            MilestoneGuard.Bypass = true;
            try
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == null || milestones.isCompleted(all[i]))
                        continue;
                    milestones.onMilestoneCompleted(all[i]);
                    completed++;
                }
            }
            finally
            {
                MilestoneGuard.Bypass = false;
            }
            return completed > 0 ? ActionResult.Ok(Loc.F("res.milestones", completed)) : ActionResult.Fail(Loc.T("res.allmilestones"));
        }

        /// <summary>
        /// Unlocks every achievement Steam defines for the game. An explicit request, so the "Protect Steam achievements"
        /// setting doesn't apply. Goes through the Steam connection the game opened itself.
        /// </summary>
        public static ActionResult UnlockSteamAchievements()
        {
            // The self-test holds achievements back for the whole process, even against an explicit request.
            if (TrainerRuntime.IsTaintedForever)
                return ActionResult.Fail(Loc.T("res.nothing"));
            var locked = LockedSteamAchievements(out _, out string problem);
            if (locked == null)
                return ActionResult.Fail(Loc.T(problem));
            if (locked.Count == 0)
                return ActionResult.Fail(Loc.T("res.allachievements"));
            int unlocked = 0;
            foreach (string name in locked)
            {
                if (SteamUserStats.SetAchievement(name))
                    unlocked++;
                else
                    Log.Warn("Steam refused to unlock the achievement " + name);
            }
            // A single upload for all of them: Steam rate-limits StoreStats.
            if (unlocked == 0 || !SteamUserStats.StoreStats())
                return ActionResult.Fail(Loc.T("res.steamwait"));
            Log.Info($"{unlocked} Steam achievement(s) unlocked on request");
            return ActionResult.Ok(Loc.F("res.achievements", unlocked));
        }

        /// <summary>Read-only summary of the player's Steam achievements (self-test log).</summary>
        public static string DescribeSteamAchievements()
        {
            var locked = LockedSteamAchievements(out int total, out string problem);
            return locked == null ? "not available (" + problem + ")" : $"{total - locked.Count}/{total} unlocked";
        }

        /// <summary>API names of the Steam achievements still locked, or null (with the reason) when Steam isn't ready.</summary>
        private static List<string> LockedSteamAchievements(out int total, out string problem)
        {
            total = 0;
            problem = null;
            // Without Steam, the game falls back to an offline interface that keeps achievements in the profile.
            var server = Singleton<ServerInterfaceManager>.mInstance;
            if (server == null || server.getInterfaceName() != nameof(ServerInterfaceSteam))
            {
                problem = "res.nosteam";
                return null;
            }
            // Zero until Steam has sent the player's stats, which the game requests every time it connects to Steam.
            uint count = SteamUserStats.GetNumAchievements();
            if (count == 0)
            {
                problem = "res.steamwait";
                return null;
            }
            total = (int)count;
            var locked = new List<string>();
            for (uint i = 0; i < count; i++)
            {
                string name = SteamUserStats.GetAchievementName(i);
                if (!string.IsNullOrEmpty(name) && !(SteamUserStats.GetAchievement(name, out bool achieved) && achieved))
                    locked.Add(name);
            }
            return locked;
        }

        public static ActionResult RestoreSelection()
        {
            var selected = Selection.getSelected();
            switch (selected)
            {
                case Character character when !character.isDead():
                    G.FillAll(character);
                    if (character is Human human)
                        human.clearCondition();
                    else if (character.isKo())
                        character.setIdle();
                    break;
                case Construction construction:
                    if (!construction.isBuilt())
                    {
                        if (construction is Connection connection &&
                            (connection.getModule1() == null || !connection.getModule1().isBuilt() ||
                             connection.getModule2() == null || !connection.getModule2().isBuilt()))
                            return ActionResult.Fail(Loc.T("res.nothing"));
                        TryComplete(construction);
                    }
                    else
                    {
                        RepairConstruction(construction);
                        G.Fill(construction.getOxygenIndicator());
                        if (construction is Module module)
                        {
                            var components = module.getComponents();
                            for (int i = 0; i < components.Count; i++)
                                RepairComponent(components[i]);
                        }
                    }
                    break;
                case ConstructionComponent component:
                    if (!component.isBuilt())
                        TryComplete(component);
                    else
                        RepairComponent(component);
                    break;
                case Resource resource when G.ResourceCondition != null:
                    G.Fill(G.ResourceCondition(resource));
                    break;
                default:
                    return ActionResult.Fail(Loc.T("res.noselection"));
            }
            return ActionResult.Ok(Loc.F("res.selected", selected.getName()));
        }
    }
}
