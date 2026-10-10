using System;
using System.Collections.Generic;
using System.Linq;

namespace ConanServerManager
{
    public enum ModCategory
    {
        CoreFramework = 10,       // MCP, Pippi, SudoSpells, LBPR, Tot Admin/Customizer
        MapExtension = 20,        // Savage Wilds, Underworld, new maps
        OverhaulProgression = 30, // AoC, EEWA, Paragon Leveling, Professions, Magic
        NpcThralls = 40,          // Valkyrie, Spawn 98% Women, IQoL, Customizer
        ItemsCosmetics = 50,      // GrimProductions, Sacred Lust, High Heels, Barber
        BuildingStorage = 60,     // Double Storage, Pythagoras, Glass, Deco
        PhysicsTweaks = 70,       // Reliable Meteors, WindLess, Tot Walk, Stack sizes
        UiHud = 80,               // Minimap, Compass, Sikky Clan Emblems
        CompatibilityPatch = 90   // LBPR Additional Features, specific end-of-load patches
    }

    public static class ModLoadOrderHelper
    {
        // Known high-profile Steam Workshop Mod IDs mapped directly to standard categories
        private static readonly Dictionary<string, ModCategory> KnownModIds = new Dictionary<string, ModCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // Core Frameworks (Load 1st)
            ["3722388367"] = ModCategory.CoreFramework, // ModControlPanel (Enhanced)
            ["880454836"]  = ModCategory.CoreFramework, // Pippi - User & Server Management
            ["1369743238"] = ModCategory.CoreFramework, // Less Building Placement Restrictions
            ["2275543784"] = ModCategory.CoreFramework, // SudoSpells
            ["2886777977"] = ModCategory.CoreFramework, // Tot ! Admin
            ["2797441287"] = ModCategory.CoreFramework, // Tot ! Customizer

            // Maps
            ["2377569193"] = ModCategory.MapExtension,  // Savage Wilds
            ["2500473919"] = ModCategory.MapExtension,  // Underworld

            // Overhauls & Progression
            ["1113901982"] = ModCategory.OverhaulProgression, // The Age of Calamitous
            ["1734383367"] = ModCategory.OverhaulProgression, // Endgame Extended Weapon Arsenal (EEWA)
            ["1629644846"] = ModCategory.OverhaulProgression, // Kerozards Paragon Leveling

            // Thralls & NPCs
            ["3721912252"] = ModCategory.NpcThralls,    // Valkyrie
            ["3722270581"] = ModCategory.NpcThralls,    // Spawn 98% Women
            ["1657730588"] = ModCategory.NpcThralls,    // Improved Quality of Life (IQoL)

            // Items & Cosmetics
            ["3742408955"] = ModCategory.ItemsCosmetics, // GrimProductions Enhanced
            ["3721257555"] = ModCategory.ItemsCosmetics, // Sacred Lust armor set
            ["3731474693"] = ModCategory.ItemsCosmetics, // High Heels System
            ["1542371997"] = ModCategory.ItemsCosmetics, // Barbarian Barber
            ["1369802940"] = ModCategory.ItemsCosmetics, // Fashionist
            ["3721422676"] = ModCategory.ItemsCosmetics, // Accessory mod / Cosmetics
            ["3721568940"] = ModCategory.ItemsCosmetics, // Armor mod

            // Building & Storage
            ["3803465771"] = ModCategory.BuildingStorage, // double_storage_space
            ["2723987927"] = ModCategory.BuildingStorage, // Pythagoras: Support Beams
            ["1361091524"] = ModCategory.BuildingStorage, // Glass Construction and more
            ["3755091710"] = ModCategory.BuildingStorage, // Storage / Placeables

            // Physics, Environment & Tweaks
            ["3723073788"] = ModCategory.PhysicsTweaks,   // Reliable Meteor Showers
            ["3789088705"] = ModCategory.PhysicsTweaks,   // WindLess
            ["2671265327"] = ModCategory.PhysicsTweaks,   // Tot ! Walk
            ["3722359128"] = ModCategory.PhysicsTweaks,   // Tweaks
            ["3723975720"] = ModCategory.PhysicsTweaks,   // Assets_Mod

            // UI, HUD & Clan Emblems
            ["3720108366"] = ModCategory.UiHud,           // Sikky's Clan Emblems
            ["3720663670"] = ModCategory.UiHud,           // UI Tweaks
            ["3719513784"] = ModCategory.UiHud,           // HUD
            ["3720921242"] = ModCategory.UiHud,           // Minimap / Map tweaks

            // End-of-load compatibility patches (Load Last)
            ["1444947329"] = ModCategory.CompatibilityPatch, // LBPR - Additional Features
            ["2864811796"] = ModCategory.CompatibilityPatch, // Compatibility patch
            ["3786621691"] = ModCategory.CompatibilityPatch  // Server Override Patch
        };

        public static ModCategory DetectCategory(string? title, string? description, string modId)
        {
            return ClassifyMod(modId, title, description);
        }

        public static ModCategory ClassifyMod(string modId, string? title = null, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(modId)) return ModCategory.ItemsCosmetics;

            string cleanId = modId.Trim();
            if (KnownModIds.TryGetValue(cleanId, out var knownCat))
            {
                return knownCat;
            }

            string combined = $"{(title ?? "")} {(description ?? "")}".ToLowerInvariant();

            // 1. Compatibility Patches (Check first so "LBPR Additional Features" isn't misclassified as Core Framework)
            if (combined.Contains("additional features") || combined.Contains("compatibility patch") || 
                combined.Contains("override patch") || combined.Contains("bridge patch"))
            {
                return ModCategory.CompatibilityPatch;
            }

            // 2. Core Frameworks (Load 1st)
            if (combined.Contains("modcontrolpanel") || combined.Contains("mod control panel") || combined.Contains("mcp") ||
                combined.Contains("pippi") || combined.Contains("sudospells") || combined.Contains("tot ! admin") ||
                combined.Contains("tot ! customizer") || combined.Contains("less building placement restrictions") ||
                combined.Contains("lbpr") || combined.Contains("framework") || combined.Contains("core library"))
            {
                return ModCategory.CoreFramework;
            }

            // 3. Maps & World Expansions
            if (combined.Contains("map extension") || combined.Contains("new map") || combined.Contains("savage wilds") ||
                combined.Contains("underworld") || combined.Contains("the old tunnels") || combined.Contains("dungeon map"))
            {
                return ModCategory.MapExtension;
            }

            // 4. Overhauls & Progression
            if (combined.Contains("age of calamitous") || combined.Contains("calamitous") || combined.Contains("eewa") ||
                combined.Contains("endgame extended") || combined.Contains("paragon leveling") || combined.Contains("kerozards") ||
                combined.Contains("level cap") || combined.Contains("magic system") || combined.Contains("professions"))
            {
                return ModCategory.OverhaulProgression;
            }

            // 5. NPCs & Thralls
            if (combined.Contains("thrall") || combined.Contains("valkyrie") || combined.Contains("spawn") ||
                combined.Contains("women") || combined.Contains("npc") || combined.Contains("follower") ||
                combined.Contains("improved quality of life") || combined.Contains("iqol"))
            {
                return ModCategory.NpcThralls;
            }

            // 6. UI & HUD
            if (combined.Contains("minimap") || combined.Contains("compass") || combined.Contains("hud") ||
                combined.Contains("clan emblem") || combined.Contains("clan emblems") || combined.Contains("emblem") ||
                combined.Contains("ui tweak") || combined.Contains("user interface"))
            {
                return ModCategory.UiHud;
            }

            // 7. Building & Storage
            if (combined.Contains("storage") || combined.Contains("double storage") || combined.Contains("building") ||
                combined.Contains("pythagoras") || combined.Contains("glass construction") || combined.Contains("support beam") ||
                combined.Contains("placeable") || combined.Contains("deco") || combined.Contains("chest"))
            {
                return ModCategory.BuildingStorage;
            }

            // 8. Physics & Tweaks
            if (combined.Contains("meteor") || combined.Contains("windless") || combined.Contains("wind") ||
                combined.Contains("tot ! walk") || combined.Contains("walk speed") || combined.Contains("weather") ||
                combined.Contains("lighting") || combined.Contains("physics") || combined.Contains("stack size"))
            {
                return ModCategory.PhysicsTweaks;
            }

            // 9. Items, Weapons, Armor & Cosmetics (Default fallback)
            return ModCategory.ItemsCosmetics;
        }

        public static string GetCategoryDisplayName(ModCategory category)
        {
            return category switch
            {
                ModCategory.CoreFramework => "Core Frameworks & UI",
                ModCategory.MapExtension => "Map Expansions",
                ModCategory.OverhaulProgression => "Overhauls & Progression",
                ModCategory.NpcThralls => "Thralls & NPCs",
                ModCategory.ItemsCosmetics => "Weapons, Armor & Cosmetics",
                ModCategory.BuildingStorage => "Building & Storage",
                ModCategory.PhysicsTweaks => "Physics & Tweaks",
                ModCategory.UiHud => "HUD & Emblems",
                ModCategory.CompatibilityPatch => "Compatibility Patches (End)",
                _ => "General Content"
            };
        }

        public static List<string> AutoSortModList(List<string> currentMods, Func<string, WorkshopModItem?> detailsProvider)
        {
            if (currentMods == null || currentMods.Count <= 1) return currentMods?.ToList() ?? new List<string>();

            // Stable sort using category priority, preserving relative order of mods within the same category
            return currentMods
                .Select((modId, originalIndex) =>
                {
                    var details = detailsProvider(modId);
                    var category = ClassifyMod(modId, details?.Title, details?.ShortDescription);
                    return new
                    {
                        ModId = modId,
                        Category = category,
                        Priority = (int)category,
                        OriginalIndex = originalIndex
                    };
                })
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.OriginalIndex)
                .Select(x => x.ModId)
                .ToList();
        }
    }
}
