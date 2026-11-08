using Planetbase;
using PlanetbaseTrainer.Core;

namespace PlanetbaseTrainer.Game
{
    /// <summary>Material resource types exposed by the trainer (resolved by class name, so names are stable).</summary>
    internal static class ResourceCatalog
    {
        public static readonly string[] TypeNames =
        {
            "Metal", "Bioplastic", "Meal", "Spares", "Semiconductors", "MedicalSupplies", "Ore", "Starch",
            "Vegetables", "Vitromeat", "MedicinalPlants", "AlcoholicDrink", "Gun",
        };

        /// <summary>Resources kept topped-up by "Unlimited resources" by default.</summary>
        public static readonly bool[] RefilledByDefault =
        {
            true, true, true, true, true, true, false, false, false, false, false, false, false,
        };

        public static int Count => TypeNames.Length;

        public static string Name(int index) => Loc.T("rn." + TypeNames[index]);

        public static ResourceType Resolve(int index) =>
            TypeList<ResourceType, ResourceTypeList>.find(TypeNames[index]);
    }

    /// <summary>Colonist and bot specializations exposed by the trainer.</summary>
    internal static class SpecializationCatalog
    {
        public static readonly string[] Colonists = { "Worker", "Biologist", "Engineer", "Medic", "Guard" };

        public static readonly string[] Bots = { "Carrier", "Constructor", "Driller" };

        public static string Name(string typeName) => Loc.T("spec." + typeName);

        public static Specialization Resolve(string typeName) =>
            TypeList<Specialization, SpecializationList>.find(typeName);
    }
}
