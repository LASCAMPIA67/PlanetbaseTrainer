using System.Collections.Generic;
using UnityEngine;

namespace PlanetbaseTrainer.Core
{
    /// <summary>French / English localization. French is used when Windows is in French (or when forced in the settings).</summary>
    internal static class Loc
    {
        private static readonly Dictionary<string, string> Fr = new Dictionary<string, string>(512);
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(512);
        private static Dictionary<string, string> _current = Fr;

        /// <summary>Incremented whenever the language changes (lets the UI invalidate cached text measurements).</summary>
        public static int Version { get; private set; }

        public static bool IsFrench => _current == Fr;

        /// <summary>Temporary language override (self-test screenshots). Never saved to the configuration.</summary>
        public static UiLanguage? Override { get; set; }

        public static void Init()
        {
            if (Fr.Count == 0)
                Fill();
            Refresh();
        }

        public static void Refresh()
        {
            var setting = Override ?? Settings.Language?.Value ?? UiLanguage.Auto;
            bool french = setting == UiLanguage.French ||
                          (setting == UiLanguage.Auto && Application.systemLanguage == SystemLanguage.French);
            _current = french ? Fr : En;
            Version++;
        }

        public static string T(string key)
        {
            if (_current.TryGetValue(key, out var value) || En.TryGetValue(key, out value))
                return value;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            try
            {
                return string.Format(T(key), args);
            }
            catch (System.FormatException)
            {
                return T(key);
            }
        }

        private static void Add(string key, string fr, string en)
        {
            Fr[key] = fr;
            En[key] = en;
        }

        private static void Fill()
        {
            // ---------------------------------------------------------------- Tabs & sections
            Add("tab.Favorites", "Favoris", "Favorites");
            Add("tab.Colonists", "Colons", "Colonists");
            Add("tab.Construction", "Construction", "Construction");
            Add("tab.Resources", "Ressources", "Resources");
            Add("tab.Base", "Base", "Base");
            Add("tab.Trade", "Commerce", "Trade");
            Add("tab.Threats", "Menaces", "Threats");
            Add("tab.Time", "Temps", "Time");
            Add("tab.Progression", "Progression", "Progression");
            Add("tab.Settings", "Paramètres", "Settings");

            Add("sec.survival", "Survie", "Survival");
            Add("sec.efficiency", "Efficacité", "Efficiency");
            Add("sec.population", "Population", "Population");
            Add("sec.bots", "Robots", "Bots");
            Add("sec.buildings", "Bâtiments", "Buildings");
            Add("sec.maintenance", "Entretien", "Maintenance");
            Add("sec.stock", "Stocks", "Stock");
            Add("sec.production", "Production", "Production");
            Add("sec.grids", "Réseaux", "Utilities");
            Add("sec.environment", "Environnement", "Environment");
            Add("sec.merchants", "Marchands", "Merchants");
            Add("sec.ships", "Vaisseaux", "Ships");
            Add("sec.disasters", "Catastrophes", "Disasters");
            Add("sec.security", "Sécurité", "Security");
            Add("sec.sandbox", "Bac à sable", "Sandbox");
            Add("sec.speed", "Vitesse", "Speed");
            Add("sec.daynight", "Jour et nuit", "Day & night");
            Add("sec.colony", "Colonie", "Colony");
            Add("sec.selection", "Sélection", "Selection");

            // ---------------------------------------------------------------- Colonists
            Add("c.GodMode", "Mode Dieu", "God mode");
            Add("c.GodMode.d",
                "Colons et visiteurs ne perdent jamais de santé, ne s'évanouissent pas et ne peuvent pas mourir.",
                "Colonists and visitors never lose health, never pass out and cannot die.");
            Add("c.Needs", "Besoins comblés", "Needs fulfilled");
            Add("c.Needs.d",
                "Garde les besoins cochés au maximum en permanence : plus besoin de manger, boire, respirer ou dormir.",
                "Keeps the ticked needs permanently maxed out: no more eating, drinking, breathing or sleeping.");
            Add("c.NoDiseases", "Aucune maladie", "No diseases");
            Add("c.NoDiseases.d",
                "Guérit et empêche grippe, radiations, malnutrition, traumatismes et blessures de combat.",
                "Cures and prevents flu, radiation, malnutrition, trauma and combat wounds.");
            Add("c.HealAll", "Soigner tout le monde", "Heal everyone");
            Add("c.HealAll.d",
                "Restaure instantanément toutes les jauges des colons et visiteurs, et guérit leurs maladies.",
                "Instantly refills every stat of colonists and visitors and cures their conditions.");
            Add("c.MoveSpeed", "Déplacement rapide", "Fast movement");
            Add("c.MoveSpeed.d",
                "Multiplie la vitesse de déplacement des colons et des robots.",
                "Multiplies the walking speed of colonists and bots.");
            Add("c.WorkSpeed", "Travail rapide", "Fast work");
            Add("c.WorkSpeed.d",
                "Multiplie la vitesse de construction, de production, de réparation et de minage.",
                "Multiplies building, production, repair and mining speed.");
            Add("c.SpawnColonists", "Ajouter des colons", "Add colonists");
            Add("c.SpawnColonists.d",
                "Fait apparaître des colons sur l'aire d'atterrissage (ou devant un sas).",
                "Spawns colonists on the landing pad (or in front of an airlock).");
            Add("c.InvincibleBots", "Robots indestructibles", "Indestructible bots");
            Add("c.InvincibleBots.d",
                "Les robots ne s'usent jamais et ne tombent jamais en panne.",
                "Bots never wear out and never break down.");
            Add("c.RepairBots", "Réparer tous les robots", "Repair all bots");
            Add("c.RepairBots.d",
                "Remet l'état et l'intégrité de tous les robots à 100 %.",
                "Restores the condition and integrity of every bot to 100%.");
            Add("c.SpawnBots", "Ajouter des robots", "Add bots");
            Add("c.SpawnBots.d",
                "Fait apparaître des robots transporteurs, constructeurs ou foreurs.",
                "Spawns carrier, constructor or driller bots.");

            // ---------------------------------------------------------------- Construction
            Add("c.FreeBuild", "Construction gratuite", "Free construction");
            Add("c.FreeBuild.d",
                "Bâtiments, couloirs et équipements ne demandent plus aucun matériau.",
                "Buildings, corridors and equipment no longer require any material.");
            Add("c.InstantBuild", "Construction instantanée", "Instant construction");
            Add("c.InstantBuild.d",
                "Tout ce que vous placez est terminé immédiatement, sans attendre d'ouvrier.",
                "Everything you place is completed immediately, no builder needed.");
            Add("c.FastBuild", "Construction accélérée", "Faster construction");
            Add("c.FastBuild.d",
                "Multiplie la vitesse à laquelle ouvriers et robots construisent.",
                "Multiplies how fast workers and bots build.");
            Add("c.CompleteAll", "Terminer tous les chantiers", "Finish all construction sites");
            Add("c.CompleteAll.d",
                "Achève immédiatement toutes les constructions en attente.",
                "Instantly completes every pending construction.");
            Add("c.Indestructible", "Structures indestructibles", "Indestructible structures");
            Add("c.Indestructible.d",
                "Les météorites et la foudre n'endommagent plus vos bâtiments.",
                "Meteors and lightning no longer damage your buildings.");
            Add("c.NoWear", "Aucune usure", "No wear");
            Add("c.NoWear.d",
                "Bâtiments et équipements restent à 100 % : plus aucune réparation ni pièce de rechange.",
                "Buildings and equipment stay at 100%: no more repairs or spare parts.");
            Add("c.RepairAll", "Réparer toutes les structures", "Repair all structures");
            Add("c.RepairAll.d",
                "Remet tous les bâtiments et équipements en parfait état.",
                "Restores every building and piece of equipment to perfect condition.");

            // ---------------------------------------------------------------- Resources
            Add("c.InfiniteResources", "Ressources illimitées", "Unlimited resources");
            Add("c.InfiniteResources.d",
                "Réapprovisionne automatiquement vos entrepôts pour garder le stock visé de chaque ressource cochée.",
                "Automatically restocks your storage to keep the target amount of every ticked resource.");
            Add("c.AddResources", "Ajouter des ressources", "Add resources");
            Add("c.AddResources.d",
                "Dépose des ressources directement dans vos entrepôts ; le surplus est livré sur l'aire d'atterrissage.",
                "Puts resources straight into storage; any surplus is delivered to the landing pad.");
            Add("c.AddCoins", "Ajouter des pièces", "Add coins");
            Add("c.AddCoins.d",
                "Crédite des pièces à la colonie (monnaie des visiteurs et du commerce).",
                "Credits coins to the colony (visitor and trading currency).");
            Add("c.FastProduction", "Production accélérée", "Faster production");
            Add("c.FastProduction.d",
                "Multiplie la vitesse des usines, fermes, cuisines et ateliers.",
                "Multiplies the speed of factories, farms, kitchens and workshops.");
            Add("c.NoSpoilage", "Ressources impérissables", "No spoilage");
            Add("c.NoSpoilage.d",
                "Les ressources laissées dehors ne se dégradent plus et ne disparaissent plus.",
                "Resources left outside no longer decay or disappear.");

            // ---------------------------------------------------------------- Base
            Add("c.UnlimitedPower", "Énergie illimitée", "Unlimited power");
            Add("c.UnlimitedPower.d",
                "Plus aucune consommation électrique : tous les bâtiments restent alimentés, même la nuit.",
                "Zero power consumption: every building stays powered, even at night.");
            Add("c.UnlimitedWater", "Eau illimitée", "Unlimited water");
            Add("c.UnlimitedWater.d",
                "Plus aucune consommation d'eau : tous les bâtiments restent approvisionnés.",
                "Zero water consumption: every building stays supplied.");
            Add("c.UnlimitedOxygen", "Oxygène illimité", "Unlimited oxygen");
            Add("c.UnlimitedOxygen.d",
                "Tous les modules restent remplis d'oxygène, même sans générateur.",
                "All modules stay full of oxygen, even without generators.");
            Add("c.MaxWind", "Vent maximal", "Maximum wind");
            Add("c.MaxWind.d",
                "Le vent souffle en permanence au maximum : éoliennes à pleine puissance.",
                "Wind always blows at full strength: wind turbines at full power.");

            // ---------------------------------------------------------------- Trade
            Add("c.FreeTrade", "Commerce gratuit", "Free trade");
            Add("c.FreeTrade.d",
                "Prenez ce que vous voulez aux marchands sans rien donner en échange, sans limite de soute.",
                "Take whatever you want from merchants without giving anything back, no cargo limit.");
            Add("c.UnlimitedStock", "Stock marchand illimité", "Unlimited merchant stock");
            Add("c.UnlimitedStock.d",
                "Les marchands proposent 999 unités de chaque ressource et 25 robots de chaque type proposé.",
                "Merchants offer 999 units of every resource and 25 of each bot they sell.");
            Add("c.CallMerchant", "Appeler un marchand", "Call a merchant");
            Add("c.CallMerchant.d",
                "Un vaisseau marchand vient se poser immédiatement (aire d'atterrissage libre requise).",
                "A merchant ship lands right away (free landing pad required).");
            Add("c.CallColonists", "Appeler des colons", "Call colonists");
            Add("c.CallColonists.d",
                "Un vaisseau de colons vient se poser immédiatement (aire d'atterrissage libre requise).",
                "A colonist ship lands right away (free landing pad required).");
            Add("c.CallVisitors", "Appeler des visiteurs", "Call visitors");
            Add("c.CallVisitors.d",
                "Un vaisseau de visiteurs vient se poser immédiatement (astroport requis).",
                "A visitor ship lands right away (starport required).");

            // ---------------------------------------------------------------- Threats
            Add("c.NoDisasters", "Aucune catastrophe", "No disasters");
            Add("c.NoDisasters.d",
                "Empêche les catastrophes cochées et stoppe celles déjà en cours.",
                "Prevents the ticked disasters and stops those already in progress.");
            Add("c.MeteorShield", "Bouclier anti-météorites", "Meteor shield");
            Add("c.MeteorShield.d",
                "Vos lasers anti-météorites interceptent 100 % des météorites.",
                "Your anti-meteor lasers intercept 100% of meteors.");
            Add("c.StopDisasters", "Arrêter les catastrophes", "Stop disasters");
            Add("c.StopDisasters.d",
                "Met fin immédiatement aux tempêtes, blizzards et éruptions solaires en cours.",
                "Immediately ends ongoing sandstorms, blizzards and solar flares.");
            Add("c.NoIntruders", "Aucun intrus", "No intruders");
            Add("c.NoIntruders.d",
                "Les vaisseaux n'amènent plus jamais d'intrus.",
                "Ships never bring intruders anymore.");
            Add("c.KillIntruders", "Éliminer les intrus", "Eliminate intruders");
            Add("c.KillIntruders.d",
                "Neutralise immédiatement tous les intrus présents.",
                "Instantly neutralizes every intruder on the map.");
            Add("c.TriggerSandstorm", "Déclencher une tempête de sable", "Trigger a sandstorm");
            Add("c.TriggerSandstorm.d",
                "Lance une tempête de sable maintenant (si la planète en connaît).",
                "Starts a sandstorm right now (if the planet has them).");
            Add("c.TriggerBlizzard", "Déclencher un blizzard", "Trigger a blizzard");
            Add("c.TriggerBlizzard.d",
                "Lance un blizzard maintenant (si la planète en connaît).",
                "Starts a blizzard right now (if the planet has them).");
            Add("c.TriggerSolarFlare", "Déclencher une éruption solaire", "Trigger a solar flare");
            Add("c.TriggerSolarFlare.d",
                "Lance une éruption solaire maintenant (si la planète en connaît).",
                "Starts a solar flare right now (if the planet has them).");
            Add("c.DropMeteor", "Faire tomber une météorite", "Drop a meteor");
            Add("c.DropMeteor.d",
                "Une météorite s'abat près de votre base. À vos risques et périls !",
                "A meteor strikes near your base. At your own risk!");

            // ---------------------------------------------------------------- Time
            Add("c.GameSpeed", "Vitesse de jeu boostée", "Game speed boost");
            Add("c.GameSpeed.d",
                "Multiplie la vitesse du jeu, en plus des vitesses ×1 / ×2 / ×4 du jeu.",
                "Multiplies the game speed, on top of the game's own ×1 / ×2 / ×4.");
            Add("c.FreezeTime", "Figer l'heure", "Freeze time of day");
            Add("c.FreezeTime.d",
                "Le cycle jour/nuit s'arrête à l'heure actuelle.",
                "The day/night cycle stops at the current time.");
            Add("c.AlwaysDay", "Toujours le jour", "Always daytime");
            Add("c.AlwaysDay.d",
                "Il fait toujours plein jour : panneaux solaires au maximum.",
                "Always broad daylight: solar panels at maximum.");
            Add("c.SetTime", "Régler l'heure", "Set time of day");
            Add("c.SetTime.d",
                "Change immédiatement l'heure de la journée.",
                "Instantly changes the time of day.");

            // ---------------------------------------------------------------- Progression
            Add("c.UnlockTechs", "Débloquer toutes les technologies", "Unlock all technologies");
            Add("c.UnlockTechs.d",
                "Acquiert immédiatement toutes les technologies manquantes (le jeu valide alors le jalon « Technocratie » de la planète).",
                "Instantly acquires every missing technology (the game then completes the planet's “Technocracy” milestone).");
            Add("c.MaxPrestige", "Prestige maximal", "Maximum prestige");
            Add("c.MaxPrestige.d",
                "Prestige au maximum : plus de colons et de visiteurs (mais aussi plus d'intrus), et le jalon de prestige est validé.",
                "Prestige maxed out: more colonists and visitors (but also more intruders), and the prestige milestone is reached.");
            Add("c.CompleteMilestones", "Valider tous les jalons", "Complete all milestones");
            Add("c.CompleteMilestones.d",
                "Valide les jalons de la planète actuelle. Modifie définitivement votre profil (planètes débloquées).",
                "Completes the milestones of the current planet. Permanently changes your profile (unlocks planets).");
            Add("c.RestoreSelected", "Restaurer la sélection", "Restore selection");
            Add("c.RestoreSelected.d",
                "Remet au maximum l'élément sélectionné : colon, robot, bâtiment (terminé s'il est en chantier) ou ressource.",
                "Maxes out the selected item: colonist, bot, building (completed if under construction) or resource.");

            // ---------------------------------------------------------------- Options
            Add("opt.mult", "Multiplicateur", "Multiplier");
            Add("opt.needs", "Besoins", "Needs");
            Add("opt.type", "Type", "Type");
            Add("opt.count", "Nombre", "Count");
            Add("opt.amount", "Quantité", "Amount");
            Add("opt.resource", "Ressource", "Resource");
            Add("opt.target", "Stock visé", "Target stock");
            Add("opt.resources", "Ressources", "Resources");
            Add("opt.disasters", "Catastrophes", "Disasters");
            Add("opt.time", "Heure", "Time");

            Add("need.nutrition", "Nutrition", "Nutrition");
            Add("need.hydration", "Hydratation", "Hydration");
            Add("need.oxygen", "Oxygène", "Oxygen");
            Add("need.sleep", "Sommeil", "Sleep");
            Add("need.morale", "Moral", "Morale");

            Add("dis.sandstorms", "Tempêtes de sable", "Sandstorms");
            Add("dis.blizzards", "Blizzards", "Blizzards");
            Add("dis.solarflares", "Éruptions solaires", "Solar flares");
            Add("dis.meteors", "Météorites", "Meteors");
            Add("dis.lightning", "Foudre", "Lightning");
            Add("dis.sandstorm", "Tempête de sable", "Sandstorm");
            Add("dis.blizzard", "Blizzard", "Blizzard");
            Add("dis.solarflare", "Éruption solaire", "Solar flare");

            Add("spec.Worker", "Ouvrier", "Worker");
            Add("spec.Biologist", "Biologiste", "Biologist");
            Add("spec.Engineer", "Ingénieur", "Engineer");
            Add("spec.Medic", "Médecin", "Medic");
            Add("spec.Guard", "Garde", "Guard");
            Add("spec.Carrier", "Transporteur", "Carrier");
            Add("spec.Constructor", "Constructeur", "Constructor");
            Add("spec.Driller", "Foreur", "Driller");

            Add("rn.Metal", "Métal", "Metal");
            Add("rn.Bioplastic", "Bioplastique", "Bioplastic");
            Add("rn.Meal", "Repas", "Meals");
            Add("rn.Spares", "Pièces de rechange", "Spares");
            Add("rn.Semiconductors", "Semi-conducteurs", "Semiconductors");
            Add("rn.MedicalSupplies", "Fournitures médicales", "Medical supplies");
            Add("rn.Ore", "Minerai", "Ore");
            Add("rn.Starch", "Amidon", "Starch");
            Add("rn.Vegetables", "Légumes", "Vegetables");
            Add("rn.Vitromeat", "Viande in vitro", "Vitromeat");
            Add("rn.MedicinalPlants", "Plantes médicinales", "Medicinal plants");
            Add("rn.AlcoholicDrink", "Boissons alcoolisées", "Alcoholic drinks");
            Add("rn.Gun", "Armes", "Guns");

            Add("ship.merchant", "Vaisseau marchand", "Merchant ship");
            Add("ship.colonists", "Vaisseau de colons", "Colonist ship");
            Add("ship.visitors", "Vaisseau de visiteurs", "Visitor ship");

            // ---------------------------------------------------------------- UI
            Add("ui.search", "Rechercher une triche…", "Search a cheat…");
            Add("ui.ingame", "En partie", "In game");
            Add("ui.notingame", "Hors partie", "Not in game");
            Add("ui.patches", "Patchs {0}/{1}", "Patches {0}/{1}");
            Add("ui.run", "Exécuter", "Run");
            Add("ui.confirm", "Confirmer ?", "Confirm?");
            Add("ui.needgame", "En partie", "In game only");
            Add("ui.unsupported", "Indisponible sur cette version du jeu", "Unavailable on this game version");
            Add("ui.presskey", "Appuyez…", "Press a key…");
            Add("ui.nokey", "Aucun", "None");
            Add("ui.hint", "{0} : menu   ·   Clic sur un raccourci : modifier   ·   Clic droit : effacer",
                "{0}: menu   ·   Click a hotkey: change   ·   Right-click: clear");
            Add("ui.capturehint", "Appuyez sur une touche   ·   Échap : annuler   ·   Retour arrière : effacer",
                "Press a key   ·   Esc: cancel   ·   Backspace: clear");
            Add("ui.disableall", "Tout désactiver", "Disable all");
            Add("ui.active", "{0} active(s)", "{0} active");
            Add("ui.favempty", "Aucun favori pour l'instant. Cliquez sur l'étoile d'une triche pour l'épingler ici.",
                "No favorites yet. Click the star of a cheat to pin it here.");
            Add("ui.noresults", "Aucun résultat pour « {0} »", "No results for “{0}”");
            Add("ui.close", "Fermer", "Close");
            Add("ui.danger", "RISQUÉ", "RISKY");

            Add("key.shift", "Maj", "Shift");
            Add("key.pad", "Manette", "Pad");
            Add("key.enter", "Entrée", "Enter");
            Add("key.space", "Espace", "Space");
            Add("key.escape", "Échap", "Esc");
            Add("key.backspace", "Retour arr.", "Backspace");
            Add("key.delete", "Suppr", "Delete");
            Add("key.insert", "Inser", "Insert");
            Add("key.home", "Début", "Home");
            Add("key.end", "Fin", "End");
            Add("key.pageup", "Page préc.", "Page Up");
            Add("key.pagedown", "Page suiv.", "Page Down");
            Add("key.up", "Haut", "Up");
            Add("key.down", "Bas", "Down");
            Add("key.left", "Gauche", "Left");
            Add("key.right", "Droite", "Right");
            Add("key.mouse", "Souris", "Mouse");

            // ---------------------------------------------------------------- Notifications
            Add("toast.on", "{0} : activé", "{0}: ON");
            Add("toast.off", "{0} : désactivé", "{0}: OFF");
            Add("toast.alloff", "Toutes les triches ont été désactivées", "All cheats have been disabled");
            Add("toast.keyset", "Raccourci de « {0} » : {1}", "“{0}” hotkey: {1}");
            Add("toast.keycleared", "Raccourci de « {0} » effacé", "“{0}” hotkey cleared");
            Add("toast.keyconflict", "{0} était utilisé par « {1} » : raccourci retiré", "{0} was used by “{1}”: hotkey removed there");
            Add("toast.keygame", "Attention : {0} est aussi une touche du jeu", "Warning: {0} is also a game key");
            Add("toast.notingame", "Chargez une partie pour utiliser « {0} »", "Load a game to use “{0}”");
            Add("toast.welcome", "Planetbase Trainer prêt — {0} pour ouvrir le menu", "Planetbase Trainer ready — {0} to open the menu");
            Add("toast.unsupported", "« {0} » est indisponible sur cette version du jeu", "“{0}” is unavailable on this game version");
            Add("toast.confirm", "« {0} » : appuyez à nouveau pour confirmer", "“{0}”: press again to confirm");
            Add("toast.keysreset", "Raccourcis par défaut restaurés", "Default hotkeys restored");
            Add("toast.storagefull", "Ressources illimitées : vos entrepôts sont pleins", "Unlimited resources: your storage is full");

            Add("res.healed", "{0} personne(s) soignée(s)", "{0} people healed");
            Add("res.botsrepaired", "{0} robot(s) réparé(s)", "{0} bot(s) repaired");
            Add("res.spawned", "{0} × {1} en route", "{0} × {1} on their way");
            Add("res.nospawnpoint", "Construisez d'abord une aire d'atterrissage ou un sas", "Build a landing pad or an airlock first");
            Add("res.completed", "{0} construction(s) terminée(s)", "{0} construction(s) completed");
            Add("res.nothing", "Rien à faire", "Nothing to do");
            Add("res.repaired", "{0} élément(s) réparé(s)", "{0} item(s) repaired");
            Add("res.resadded", "+{0} {1}", "+{0} {1}");
            Add("res.resoverflow", "+{0} {1} ({2} livré(s) sur l'aire d'atterrissage)", "+{0} {1} ({2} delivered to the landing pad)");
            Add("res.nostorage", "Aucune place disponible : construisez un entrepôt ou une aire d'atterrissage", "No room available: build a storage or a landing pad");
            Add("res.coins", "+{0} pièces", "+{0} coins");
            Add("res.shipcalled", "{0} en approche", "{0} incoming");
            Add("res.nolandingpad", "Aucune aire d'atterrissage opérationnelle et libre", "No free operational landing pad");
            Add("res.nostarport", "Un astroport opérationnel et libre est requis", "A free operational starport is required");
            Add("res.intruders", "{0} intrus éliminé(s)", "{0} intruder(s) eliminated");
            Add("res.nointruders", "Aucun intrus présent", "No intruders around");
            Add("res.disasterstopped", "{0} catastrophe(s) arrêtée(s)", "{0} disaster(s) stopped");
            Add("res.nodisaster", "Aucune catastrophe en cours", "No disaster in progress");
            Add("res.disasterstarted", "{0} déclenché(e) !", "{0} triggered!");
            Add("res.noplanetrisk", "Ce phénomène n'existe pas sur cette planète", "This phenomenon does not exist on this planet");
            Add("res.disasterblocked", "Décochez ce type dans « Aucune catastrophe » d'abord", "Untick this type in “No disasters” first");
            Add("res.inprogress", "Déjà en cours", "Already in progress");
            Add("res.meteor", "Météorite en approche !", "Meteor incoming!");
            Add("res.time", "Heure réglée sur {0}", "Time set to {0}");
            Add("res.techs", "{0} technologie(s) débloquée(s)", "{0} technolog(y/ies) unlocked");
            Add("res.alltechs", "Toutes les technologies sont déjà acquises", "All technologies are already acquired");
            Add("res.milestones", "{0} jalon(s) validé(s)", "{0} milestone(s) completed");
            Add("res.allmilestones", "Tous les jalons sont déjà validés", "All milestones are already completed");
            Add("res.selected", "« {0} » restauré", "“{0}” restored");
            Add("res.noselection", "Sélectionnez d'abord un élément dans le jeu", "Select something in the game first");
            Add("res.failed", "Échec : {0}", "Failed: {0}");

            // ---------------------------------------------------------------- Settings page
            Add("set.ui", "Interface", "Interface");
            Add("set.lang", "Langue", "Language");
            Add("set.lang.d", "Langue du trainer (Automatique = langue de Windows).", "Trainer language (Automatic = Windows language).");
            Add("set.scale", "Taille du menu", "Menu size");
            Add("set.scale.d", "Automatique : s'adapte à la résolution de l'écran.", "Automatic: adapts to the screen resolution.");
            Add("set.scaleauto", "Auto", "Auto");
            Add("set.accent", "Couleur d'accent", "Accent color");
            Add("set.accent.d", "Couleur principale du menu, du HUD et des notifications.", "Main color of the menu, HUD and notifications.");
            Add("set.hud", "HUD des triches actives", "Active cheats HUD");
            Add("set.hud.d", "Affiche en jeu la liste des triches activées.", "Shows the list of enabled cheats in game.");
            Add("set.hudpos", "Position du HUD", "HUD position");
            Add("set.hudpos.d", "Emplacement du HUD à l'écran.", "Where the HUD is drawn on screen.");
            Add("set.toasts", "Notifications", "Notifications");
            Add("set.toasts.d", "Confirme à l'écran chaque triche activée par raccourci.", "Confirms on screen every cheat toggled with a hotkey.");
            Add("set.pause", "Pause quand le menu est ouvert", "Pause while the menu is open");
            Add("set.pause.d", "Met la simulation en pause tant que le menu est affiché.", "Pauses the simulation while the menu is shown.");
            Add("set.keys", "Raccourcis globaux", "Global hotkeys");
            Add("set.menukey", "Ouvrir / fermer le menu", "Open / close the menu");
            Add("set.menukey.d", "Fonctionne partout, y compris dans le menu principal du jeu.", "Works everywhere, including the game's main menu.");
            Add("set.panickey", "Tout désactiver", "Disable everything");
            Add("set.panickey.d", "Coupe instantanément toutes les triches actives.", "Instantly turns off every active cheat.");
            Add("set.resetkeys", "Raccourcis par défaut", "Default hotkeys");
            Add("set.resetkeys.d", "Restaure les raccourcis d'origine de toutes les triches.", "Restores the original hotkeys of every cheat.");
            Add("set.reset", "Restaurer", "Restore");
            Add("set.behavior", "Comportement", "Behavior");
            Add("set.remember", "Mémoriser les triches actives", "Remember active cheats");
            Add("set.remember.d", "Réactive vos triches au prochain lancement du jeu.", "Re-enables your cheats next time you start the game.");
            Add("set.achievements", "Protéger les succès Steam", "Protect Steam achievements");
            Add("set.achievements.d", "Aucun succès ne se débloque tant qu'une triche est active ou a servi dans la partie en cours.",
                "No achievement unlocks while a cheat is active or has been used in the current game.");
            Add("set.milestones", "Protéger la progression du profil", "Protect profile progression");
            Add("set.milestones.d", "Les jalons atteints grâce aux triches ne sont pas enregistrés (les planètes restent verrouillées).",
                "Milestones reached thanks to cheats are not saved (planets stay locked).");
            Add("set.about", "À propos", "About");
            Add("set.about.version", "Version du trainer", "Trainer version");
            Add("set.about.game", "Jeu", "Game");
            Add("set.about.patches", "Patchs appliqués", "Applied patches");
            Add("set.about.failed", "Échecs : {0}", "Failures: {0}");
            Add("set.about.allok", "Tous les patchs sont opérationnels", "Every patch is operational");
            Add("set.about.config", "Configuration", "Configuration");

            Add("lang.Auto", "Automatique", "Automatic");
            Add("lang.French", "Français", "Français");
            Add("lang.English", "English", "English");

            Add("accent.Cyan", "Cyan", "Cyan");
            Add("accent.Violet", "Violet", "Violet");
            Add("accent.Emerald", "Émeraude", "Emerald");
            Add("accent.Amber", "Ambre", "Amber");
            Add("accent.Rose", "Rose", "Rose");
            Add("accent.Orange", "Orange", "Orange");

            Add("hudpos.TopCenter", "En haut au centre", "Top center");
            Add("hudpos.TopLeft", "En haut à gauche", "Top left");
            Add("hudpos.TopRight", "En haut à droite", "Top right");
            Add("hudpos.BottomLeft", "En bas à gauche", "Bottom left");
            Add("hudpos.BottomRight", "En bas à droite", "Bottom right");
        }
    }
}
