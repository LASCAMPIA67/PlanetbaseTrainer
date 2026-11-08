using System.Reflection;
using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using UnityEngine;

namespace PlanetbaseTrainer.Game
{
    /// <summary>Decides which character stats are protected and refills them.</summary>
    internal static class Protection
    {
        public static bool IsProtected(Character character, CharacterIndicator which)
        {
            if (character is Bot)
                return Cheats.InvincibleBots.On &&
                       (which == CharacterIndicator.Condition || which == CharacterIndicator.Integrity);
            if (!G.IsFriendlyHuman(character))
                return false;
            switch (which)
            {
                case CharacterIndicator.Health:
                    return Cheats.GodMode.On;
                case CharacterIndicator.Nutrition:
                    return Cheats.Needs.On && Cheats.NeedNutrition.Value;
                case CharacterIndicator.Hydration:
                    return Cheats.Needs.On && Cheats.NeedHydration.Value;
                case CharacterIndicator.Oxygen:
                    return Cheats.Needs.On && Cheats.NeedOxygen.Value;
                case CharacterIndicator.Sleep:
                    return Cheats.Needs.On && Cheats.NeedSleep.Value;
                case CharacterIndicator.Morale:
                    return Cheats.Needs.On && Cheats.NeedMorale.Value;
                default:
                    return false;
            }
        }

        /// <summary>Runs before and after <c>Human.tick</c>: keeps protected stats at their maximum.</summary>
        public static void OnHumanTick(Human human)
        {
            if (!(Cheats.GodMode.On || Cheats.Needs.On || Cheats.NoDiseases.On))
                return;
            if (human.isDead() || !G.IsFriendlyHuman(human) || G.CharacterIndicators == null)
                return;
            var indicators = G.CharacterIndicators(human);
            if (indicators != null)
            {
                for (int i = 0; i <= (int)CharacterIndicator.Morale && i < indicators.Length; i++)
                {
                    if (IsProtected(human, (CharacterIndicator)i))
                        G.Fill(indicators[i]);
                }
            }
            if (Cheats.NoDiseases.On && human.getCondition() != null)
                human.clearCondition();
        }

        public static void OnBotTick(Bot bot)
        {
            if (!Cheats.InvincibleBots.On || bot.isDead())
                return;
            G.Fill(G.GetIndicator(bot, CharacterIndicator.Condition));
            G.Fill(G.GetIndicator(bot, CharacterIndicator.Integrity));
        }
    }

    /// <summary>
    /// Milestones are saved to the player profile as soon as they are reached (and unlock planets). Several cheats reach
    /// them indirectly (prestige, technologies, population...). When protection is on, they are held back while the
    /// game is tainted; the explicit "complete all milestones" action bypasses the guard.
    /// </summary>
    internal static class MilestoneGuard
    {
        public static bool Bypass { get; set; }

        /// <summary>Protection forced for the whole process (self-test), regardless of the setting.</summary>
        public static bool ForceProtect { get; set; }

        public static bool ShouldBlock =>
            !Bypass && (ForceProtect || Settings.ProtectMilestones.Value) && TrainerRuntime.IsTainted;
    }

    /// <summary>Time of day (freeze / always day) and game-speed notifications.</summary>
    internal static class TimeControl
    {
        public const float Noon = 0.25f;
        private static readonly MethodInfo OnTimeScaleChangedMethod = Ref.Method(typeof(TimeManager), "onTimeScaleChanged");
        private static float _frozen = float.NaN;

        public static void ResetFrozen() => _frozen = float.NaN;

        public static void SetFrozen(float value) => _frozen = value;

        /// <summary>Called around <c>EnvironmentManager.update</c>.</summary>
        public static void Apply(EnvironmentManager environment)
        {
            var indicator = environment.getTimeIndicator();
            if (indicator == null)
                return;
            if (Cheats.AlwaysDay.On)
            {
                indicator.setValue(Noon);
                return;
            }
            if (!Cheats.FreezeTime.On)
                return;
            if (float.IsNaN(_frozen))
                _frozen = indicator.getValue();
            indicator.setValue(_frozen);
        }

        /// <summary>Lets animations, particles and sounds pick up a new speed multiplier.</summary>
        public static void NotifyTimeScaleChanged()
        {
            var manager = Singleton<TimeManager>.mInstance;
            if (manager == null || OnTimeScaleChangedMethod == null || !G.InGame)
                return;
            OnTimeScaleChangedMethod.Invoke(manager, null);
        }

        /// <summary>Planetbase's day cycle: 0 = sunrise, 0.25 = noon, 0.5 = sunset, 0.75 = midnight.</summary>
        public static string Clock(float value)
        {
            float hours = (6f + Mathf.Repeat(value, 1f) * 24f) % 24f;
            int h = (int)hours;
            int m = Mathf.RoundToInt((hours - h) * 60f);
            if (m >= 60)
            {
                m = 0;
                h = (h + 1) % 24;
            }
            return h.ToString("00") + ":" + m.ToString("00");
        }
    }

    /// <summary>Prevents / stops sandstorms, blizzards and solar flares.</summary>
    internal static class DisasterControl
    {
        private static readonly AccessTools.FieldRef<Sandstorm, float> SandstormTime = Ref.Field<Sandstorm, float>("mTime");
        private static readonly AccessTools.FieldRef<Sandstorm, float> SandstormDuration = Ref.Field<Sandstorm, float>("mSandstormTime");
        private static readonly AccessTools.FieldRef<Blizzard, float> BlizzardTime = Ref.Field<Blizzard, float>("mTime");
        private static readonly AccessTools.FieldRef<Blizzard, float> BlizzardDuration = Ref.Field<Blizzard, float>("mBlizzardTime");
        private static readonly AccessTools.FieldRef<SolarFlare, float> SolarFlareTime = Ref.Field<SolarFlare, float>("mTime");
        private static readonly AccessTools.FieldRef<SolarFlare, float> SolarFlareDuration = Ref.Field<SolarFlare, float>("mSolarFlareTime");

        public static bool IsBlocked(Disaster disaster)
        {
            if (!Cheats.NoDisasters.On)
                return false;
            switch (disaster)
            {
                case Sandstorm _:
                    return Cheats.NoSandstorms.Value;
                case Blizzard _:
                    return Cheats.NoBlizzards.Value;
                case SolarFlare _:
                    return Cheats.NoSolarFlares.Value;
                default:
                    return false;
            }
        }

        /// <summary>Prefix of <c>Disaster.update</c>: skip the countdown while blocked, and end any ongoing occurrence.</summary>
        public static bool GateUpdate(Disaster disaster)
        {
            if (!IsBlocked(disaster))
                return true;
            if (!disaster.isInProgress())
                return false;
            ForceEnd(disaster);
            return true; // let the game run its own "end of disaster" code path
        }

        /// <summary>Fast-forwards a disaster to its end; the game cleans up on its next update.</summary>
        public static bool ForceEnd(Disaster disaster)
        {
            if (disaster == null || !disaster.isInProgress())
                return false;
            switch (disaster)
            {
                case Sandstorm sandstorm when SandstormTime != null && SandstormDuration != null:
                    SandstormTime(sandstorm) = SandstormDuration(sandstorm) + 1f;
                    return true;
                case Blizzard blizzard when BlizzardTime != null && BlizzardDuration != null:
                    BlizzardTime(blizzard) = BlizzardDuration(blizzard) + 1f;
                    return true;
                case SolarFlare flare when SolarFlareTime != null && SolarFlareDuration != null:
                    SolarFlareTime(flare) = SolarFlareDuration(flare) + 1f;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>Free trade and unlimited merchant stock.</summary>
    internal static class TradeControl
    {
        private static readonly AccessTools.FieldRef<GuiWindow, GuiLabelItem> RootItem = Ref.Field<GuiWindow, GuiLabelItem>("mRootItem");
        private static readonly AccessTools.FieldRef<GuiLabelItem, GuiDefinitions.Callback> LabelCallback =
            Ref.Field<GuiLabelItem, GuiDefinitions.Callback>("mCallback");
        private static readonly AccessTools.FieldRef<GuiMerchantWindow, ProductAmounts> MerchantStock =
            Ref.Field<GuiMerchantWindow, ProductAmounts>("mMerchantStockAmounts");
        private static readonly AccessTools.FieldRef<GuiMerchantWindow, ProductAmounts> MerchantTrade =
            Ref.Field<GuiMerchantWindow, ProductAmounts>("mMerchantTradeAmounts");
        private static readonly MethodInfo UpdateUi = Ref.Method(typeof(GuiMerchantWindow), "updateUi");

        /// <summary>Enables the "Trade" button as soon as something is requested from the merchant.</summary>
        public static void EnableTradeButton(GuiMerchantWindow window)
        {
            if (RootItem == null || MerchantTrade == null || LabelCallback == null)
                return;
            var requested = MerchantTrade(window);
            var button = FindByCallback(RootItem(window), "onTrade");
            if (button != null && requested != null)
                button.setEnabled(requested.getCount() > 0);
        }

        public static void BoostStock(GuiMerchantWindow window)
        {
            if (MerchantStock == null)
                return;
            var stock = MerchantStock(window);
            if (stock == null)
                return;
            foreach (ProductAmount amount in stock)
            {
                var product = amount.getProduct();
                int target = product is ProductResource ? 999 : product is ProductBot ? 25 : 0;
                if (target > amount.getAmount())
                    amount.setAmount(target);
            }
            UpdateUi?.Invoke(window, null);
        }

        /// <summary>Cancels the payment: un-marks / refunds everything the colony was about to hand over.</summary>
        public static void WaivePayment(MerchantShip ship, ref ProductAmounts neededProducts)
        {
            if (neededProducts == null)
                return;
            int shipId = ship.getId();
            foreach (ProductAmount amount in neededProducts)
            {
                for (int i = 0; i < amount.getAmount(); i++)
                    amount.getProduct().unmarkForTrading(shipId);
            }
            neededProducts = new ProductAmounts();
        }

        private static GuiLabelItem FindByCallback(GuiWindowItem item, string methodName)
        {
            if (item == null)
                return null;
            if (item is GuiLabelItem label)
            {
                var callback = LabelCallback(label);
                if (callback != null && callback.Method.Name == methodName)
                    return label;
            }
            var children = item.getChildren();
            if (children == null)
                return null;
            for (int i = 0; i < children.Count; i++)
            {
                var found = FindByCallback(children[i], methodName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
