using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace EOgeneratelevels
{
    public class Config
    {
        public string Stringtext { get; set; } = "Bandit";
        public short Minlevel { get; set; } = 1;
        public short Maxlevel { get; set; } = 50;
    }

    public class Program
    {
        public static string getLevel(int number)
        {
            if (number < 10)
            {
                return "00" + number;
            } else if (number < 100) {
                return "0" + number;
            } else
            {
                return number.ToString();
            }
        }

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args, new RunPreferences()
                {
                    ActionsForEmptyArgs = new RunDefaultPatcher()
                    {
                        IdentifyingModKey = "YourPatcher.esp",
                        TargetRelease = GameRelease.SkyrimSE,
                    }
                });
        }

        public static NpcConfiguration.TemplateFlag templateFlags = NpcConfiguration.TemplateFlag.AIData | NpcConfiguration.TemplateFlag.AIPackages | NpcConfiguration.TemplateFlag.AttackData | NpcConfiguration.TemplateFlag.BaseData | NpcConfiguration.TemplateFlag.DefPackList | NpcConfiguration.TemplateFlag.Factions | NpcConfiguration.TemplateFlag.Inventory | NpcConfiguration.TemplateFlag.Keywords | NpcConfiguration.TemplateFlag.ModelAnimation | NpcConfiguration.TemplateFlag.Script | NpcConfiguration.TemplateFlag.Script | NpcConfiguration.TemplateFlag.SpellList | NpcConfiguration.TemplateFlag.Traits;

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            string settingsPath = Path.Combine(state.ExtraSettingsDataPath, "config.json");
            if (!File.Exists(settingsPath)) throw new ArgumentException($"Required settings missing! {settingsPath}");
            var configText = File.ReadAllText(settingsPath);
            var config = JsonConvert.DeserializeObject<Config>(configText);

            short minlevel = config.Minlevel;
            short maxlevel = config.Maxlevel;
            string stringText = config.Stringtext;

            Console.WriteLine("minlevel = " + minlevel);
            Console.WriteLine("maxlevel = " + maxlevel);
            Console.WriteLine("stringText = " + stringText);

            foreach (var SubCharGetter in state.LoadOrder.PriorityOrder.LeveledNpc().WinningOverrides()) {
                
                if (SubCharGetter.EditorID != null && SubCharGetter.EditorID.Contains("SubChar" + stringText + "_L000_") && SubCharGetter.Entries?.Count > 0) {

                    var LChar = state.PatchMod.LeveledNpcs.DuplicateInAsNewRecord(SubCharGetter);
                    var LCharEditorID = SubCharGetter.EditorID.Replace("_L000", "").Replace("SubChar", "LChar") + "%Level_";
                    LChar.EditorID = LCharEditorID;
                    Console.WriteLine("Creating LChar: " + LCharEditorID);

                    LChar.Entries = new Noggog.ExtendedList<LeveledNpcEntry>();

                    short level = minlevel;
                    do {
                        var levelString = "_L" + getLevel(level) + "_";

                        var SubCharNew = state.PatchMod.LeveledNpcs.DuplicateInAsNewRecord(SubCharGetter);
                        var newEditorID = SubCharGetter.EditorID.Replace("_L000_", levelString);
                        SubCharNew.EditorID = newEditorID;
                        Console.WriteLine("Creating SubChar: " + newEditorID);

                        SubCharNew.Entries = new Noggog.ExtendedList<LeveledNpcEntry>();

                        foreach (var entry in SubCharGetter.Entries)
                        {
                            if (entry.Data == null) throw new Exception("SubCharEntry data is null");
                            var reference = entry.Data.Reference.Resolve(state.LinkCache);
                            if (reference == null) throw new Exception("Reference is null");
                            if (reference is INpcGetter)
                            {
                                var newReference = state.PatchMod.Npcs.DuplicateInAsNewRecord(reference);
                                var newReferenceEditorID = newReference.EditorID?.Replace("_L000_", levelString);
                                newReference.EditorID = newReferenceEditorID;
                                Console.WriteLine("Creating Actor: " + newReferenceEditorID);

                                newReference.Template = reference.FormKey;
                                newReference.Configuration.TemplateFlags = templateFlags;
                                var npcLevel = new NpcLevel {
                                    Level = level
                                };
                                newReference.Configuration.Level = npcLevel;

                                SubCharNew.Entries.Add(new LeveledNpcEntry {
                                    Data = new LeveledNpcEntryData {
                                        Level = 1,
                                        Reference = newReference,
                                        Count = 1
                                    }
                                });
                            } else
                            {
                                Console.WriteLine("Reference \"" + reference.EditorID +"\" is not an NPC");
                            }
                        }

                        LChar.Entries.Add(new LeveledNpcEntry
                        {
                            Data = new LeveledNpcEntryData
                            {
                                Level = level,
                                Reference = SubCharNew,
                                Count = 1
                            }
                        });

                        level++;
                    } while (level <= maxlevel);
                }
            }
        }
    }
}
