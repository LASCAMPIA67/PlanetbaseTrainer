using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Core;

namespace PlanetbaseTrainer.Game
{
    /// <summary>
    /// Safe accessors to the game's state.
    /// Planetbase singletons are created lazily by <c>getInstance()</c>: touching them outside a game would create
    /// stray instances, so every accessor here first checks that a game session is actually running.
    /// </summary>
    internal static class G
    {
        private static readonly FieldInfo GameManagerInstance = Ref.StaticField(typeof(GameManager), "mInstance");
        private static readonly AccessTools.FieldRef<GameManager, GameManager.State> GameManagerState =
            Ref.Field<GameManager, GameManager.State>("mState");
        private static readonly FieldInfo CharactersField = Ref.StaticField(typeof(Character), "mCharacters");
        private static readonly FieldInfo ConstructionsField = Ref.StaticField(typeof(Construction), "mConstructions");

        public static readonly AccessTools.FieldRef<Character, Indicator[]> CharacterIndicators =
            Ref.Field<Character, Indicator[]>("mIndicators");
        public static readonly AccessTools.FieldRef<Construction, Indicator> ConstructionCondition =
            Ref.Field<Construction, Indicator>("mConditionIndicator");
        public static readonly AccessTools.FieldRef<ConstructionComponent, Indicator> ComponentCondition =
            Ref.Field<ConstructionComponent, Indicator>("mConditionIndicator");
        public static readonly AccessTools.FieldRef<Resource, Indicator> ResourceCondition =
            Ref.Field<Resource, Indicator>("mConditionIndicator");

        /// <summary>The running game session, or null (title screen, loading screen, credits...).</summary>
        public static GameStateGame Session
        {
            get
            {
                if (GameManagerInstance == null || GameManagerState == null)
                    return null;
                if (!(GameManagerInstance.GetValue(null) is GameManager manager))
                    return null;
                if (GameManagerState(manager) != GameManager.State.Updating)
                    return null;
                return manager.getGameState() as GameStateGame;
            }
        }

        public static bool InGame => Session != null;

        public static List<Character> Characters => CharactersField?.GetValue(null) as List<Character>;

        public static List<Construction> Constructions => ConstructionsField?.GetValue(null) as List<Construction>;

        public static bool IsPaused => Singleton<TimeManager>.mInstance != null && Singleton<TimeManager>.mInstance.isPaused();

        /// <summary>Colonists and visitors (every human that is not an intruder).</summary>
        public static bool IsFriendlyHuman(Character character)
        {
            if (!(character is Human))
                return false;
            var specialization = character.getSpecialization();
            return specialization != null && !specialization.hasFlag(Specialization.FlagAggressive);
        }

        public static bool IsIntruder(Character character)
        {
            if (!(character is Human))
                return false;
            var specialization = character.getSpecialization();
            return specialization != null && specialization.hasFlag(Specialization.FlagAggressive);
        }

        public static void Fill(Indicator indicator)
        {
            if (indicator != null && indicator.isValidValue() && indicator.getValue() < indicator.getMax())
                indicator.setValue(indicator.getMax());
        }

        public static Indicator GetIndicator(Character character, CharacterIndicator which)
        {
            if (CharacterIndicators == null || character == null)
                return null;
            var indicators = CharacterIndicators(character);
            int index = (int)which;
            return indicators != null && index < indicators.Length ? indicators[index] : null;
        }

        public static void FillAll(Character character)
        {
            if (CharacterIndicators == null || character == null)
                return;
            var indicators = CharacterIndicators(character);
            if (indicators == null)
                return;
            for (int i = 0; i < indicators.Length; i++)
                Fill(indicators[i]);
        }
    }
}
