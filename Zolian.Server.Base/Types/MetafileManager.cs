using Newtonsoft.Json;
using Darkages.Compression;
using ServiceStack;
using Darkages.Network.Security;
using Darkages.Enums;
using Darkages.Models;

namespace Darkages.Types
{
    public class Node
    {
        public List<string> Atoms { get; set; }
        public string Name { get; set; }
    }

    public class MetafileManager
    {
        private static readonly MetafileCollection Metafiles;

        static MetafileManager()
        {
            var filePath = Path.Combine(ServerSetup.StoragePath, "metafile");

            if (!Directory.Exists(filePath)) return;

            var files = Directory.GetFiles(filePath);

            Metafiles = new MetafileCollection(short.MaxValue);

            foreach (var file in files)
            {
                var metaFile = CompressableObject.Load<Metafile>(file);

                if (metaFile.Name.StartsWith("SEvent")) continue;

                if (metaFile.Name.StartsWith("SClass")) continue;

                if (metaFile.Name.StartsWith("ItemInfo")) continue;

                Metafiles.Add(metaFile);
            }

            CreateFromTemplates();
            LoadQuestDescriptions();
        }

        private static void LoadQuestDescriptions()
        {
            var metaFile1 = new Metafile { Name = "SEvent1", Nodes = new List<MetafileNode>() };
            var metaFileLocation1 = ServerSetup.StoragePath + "\\Static\\Quests\\Circle1";
            var metaFile2 = new Metafile { Name = "SEvent2", Nodes = new List<MetafileNode>() };
            var metaFileLocation2 = ServerSetup.StoragePath + "\\Static\\Quests\\Circle2";
            var metaFile3 = new Metafile { Name = "SEvent3", Nodes = new List<MetafileNode>() };
            var metaFileLocation3 = ServerSetup.StoragePath + "\\Static\\Quests\\Circle3";
            var metaFile4 = new Metafile { Name = "SEvent4", Nodes = new List<MetafileNode>() };
            var metaFileLocation4 = ServerSetup.StoragePath + "\\Static\\Quests\\Circle4";
            var metaFile5 = new Metafile { Name = "SEvent5", Nodes = new List<MetafileNode>() };
            var metaFileLocation5 = ServerSetup.StoragePath + "\\Static\\Quests\\Circle5";
            var metaFile6 = new Metafile { Name = "SEvent6", Nodes = new List<MetafileNode>() };
            var metaFileLocation6 = ServerSetup.StoragePath + "\\Static\\Quests\\Advanced";
            var metaFile7 = new Metafile { Name = "SEvent7", Nodes = new List<MetafileNode>() };
            var metaFileLocation7 = ServerSetup.StoragePath + "\\Static\\Quests\\Forsaken";

            LoadCircleQuestDescriptions(metaFileLocation1, metaFile1);
            LoadCircleQuestDescriptions(metaFileLocation2, metaFile2);
            LoadCircleQuestDescriptions(metaFileLocation3, metaFile3);
            LoadCircleQuestDescriptions(metaFileLocation4, metaFile4);
            LoadCircleQuestDescriptions(metaFileLocation5, metaFile5);
            LoadCircleQuestDescriptions(metaFileLocation6, metaFile6);
            LoadCircleQuestDescriptions(metaFileLocation7, metaFile7);
        }

        private static void LoadCircleQuestDescriptions(string dir, Metafile metaFile)
        {
            if (!Directory.Exists(dir)) return;
            var loadedNodes = new List<Node>();

            foreach (var file in Directory.GetFiles(dir, "*.txt"))
            {
                var contents = File.ReadAllText(file);
                if (string.IsNullOrEmpty(contents)) continue;
                var nodes = JsonConvert.DeserializeObject<List<Node>>(contents);
                if (nodes == null) continue;
                if (nodes.Count > 0)
                    loadedNodes.AddRange(nodes);
            }

            foreach (var batch in loadedNodes.BatchesOf(712))
            {
                metaFile.Nodes.Add(new MetafileNode("", ""));
                foreach (var node in batch)
                {
                    var metafileNode = new MetafileNode(node.Name, node.Atoms.ToArray());
                    metaFile.Nodes.Add(metafileNode);
                }

                CompileTemplate(metaFile);
                Metafiles.Add(metaFile);
            }
        }

        public static Metafile GetMetaFile(string name) => Metafiles.Find(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public static MetafileCollection GetMetaFiles() => Metafiles;

        private static void CompileTemplate(Metafile metaFile)
        {
            using (var stream = new MemoryStream())
            {
                metaFile.Save(stream);
                metaFile.InflatedData = stream.ToArray();
            }

            metaFile.Hash = Crc32Provider.Generate32(metaFile.InflatedData);
            metaFile.Compress();
        }

        private static void CreateFromTemplates()
        {
            GenerateItemInfoMeta();
            GenerateHumanClassMeta();
            GenerateHalfElfClassMeta();
            GenerateHighElfClassMeta();
            GenerateDarkElfClassMeta();
            GenerateWoodElfClassMeta();
            GenerateOrcClassMeta();
            GenerateDwarfClassMeta();
            GenerateHalflingClassMeta();
            GenerateDragonkinClassMeta();
            GenerateHalfBeastClassMeta();
            GenerateFishClassMeta();
        }

        private static void GenerateHumanClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass1", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass2", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass3", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass4", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass5", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass6", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Human } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateHalfElfClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass7", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass8", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass9", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass10", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass11", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass12", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateHighElfClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass13", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass14", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass15", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass16", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass17", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass18", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HighElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateDarkElfClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass19", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass20", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass21", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass22", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass23", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass24", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.DarkElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateWoodElfClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass25", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass26", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass27", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass28", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass29", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass30", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.WoodElf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateOrcClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass31", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass32", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass33", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass34", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass35", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass36", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Orc } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateDwarfClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass37", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass38", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass39", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass40", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass41", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass42", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dwarf } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateHalflingClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass43", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass44", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass45", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass46", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass47", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass48", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Halfling } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateDragonkinClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass49", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass50", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass51", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass52", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass53", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass54", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Dragonkin } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateHalfBeastClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass55", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass56", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass57", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass58", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass59", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass60", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.HalfBeast } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateFishClassMeta()
        {
            var sClass1 = new Metafile { Name = "SClass61", Nodes = new List<MetafileNode>() };
            var sClass2 = new Metafile { Name = "SClass62", Nodes = new List<MetafileNode>() };
            var sClass3 = new Metafile { Name = "SClass63", Nodes = new List<MetafileNode>() };
            var sClass4 = new Metafile { Name = "SClass64", Nodes = new List<MetafileNode>() };
            var sClass5 = new Metafile { Name = "SClass65", Nodes = new List<MetafileNode>() };
            var sClass6 = new Metafile { Name = "SClass66", Nodes = new List<MetafileNode>() };

            sClass1.Nodes.Add(new MetafileNode("Skill", ""));
            sClass2.Nodes.Add(new MetafileNode("Skill", ""));
            sClass3.Nodes.Add(new MetafileNode("Skill", ""));
            sClass4.Nodes.Add(new MetafileNode("Skill", ""));
            sClass5.Nodes.Add(new MetafileNode("Skill", ""));
            sClass6.Nodes.Add(new MetafileNode("Skill", ""));

            foreach (var template in from v in ServerSetup.GlobalSkillTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass1.Nodes.Add(new MetafileNode("", ""));
            sClass1.Nodes.Add(new MetafileNode("Spell", ""));

            sClass2.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass2.Nodes.Add(new MetafileNode("", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell", ""));

            sClass3.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass3.Nodes.Add(new MetafileNode("", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell", ""));

            sClass4.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass4.Nodes.Add(new MetafileNode("", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell", ""));

            sClass5.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass5.Nodes.Add(new MetafileNode("", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell", ""));

            sClass6.Nodes.Add(new MetafileNode("Skill_End", ""));
            sClass6.Nodes.Add(new MetafileNode("", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell", ""));

            foreach (var template in from v in ServerSetup.GlobalSpellTemplateCache
                                     let prerequisites = v.Value.Prerequisites
                                     where prerequisites != null
                                     orderby prerequisites.ExpLevelRequired
                                     select v.Value)
            {
                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Berserker or Class.Peasant } or { SecondaryClassRequired: Class.Berserker or Class.DualBash })
                    sClass1.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Defender or Class.Peasant } or { SecondaryClassRequired: Class.Defender or Class.DualBash })
                    sClass2.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Assassin or Class.Peasant } or { SecondaryClassRequired: Class.Assassin })
                    sClass3.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Cleric or Class.Peasant } or { SecondaryClassRequired: Class.Cleric or Class.DualCast })
                    sClass4.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Arcanus or Class.Peasant } or { SecondaryClassRequired: Class.Arcanus or Class.DualCast })
                    sClass5.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));

                if (template.Prerequisites is { RaceRequired: Race.Fish } or { ClassRequired: Class.Monk or Class.Peasant } or { SecondaryClassRequired: Class.Monk })
                    sClass6.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }

            sClass1.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass2.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass3.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass4.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass5.Nodes.Add(new MetafileNode("Spell_End", ""));
            sClass6.Nodes.Add(new MetafileNode("Spell_End", ""));


            CompileTemplate(sClass1);
            CompileTemplate(sClass2);
            CompileTemplate(sClass3);
            CompileTemplate(sClass4);
            CompileTemplate(sClass5);
            CompileTemplate(sClass6);

            Metafiles.Add(sClass1);
            Metafiles.Add(sClass2);
            Metafiles.Add(sClass3);
            Metafiles.Add(sClass4);
            Metafiles.Add(sClass5);
            Metafiles.Add(sClass6);
        }

        private static void GenerateItemInfoMeta()
        {
            var i = 0;
            foreach (var batch in ServerSetup.GlobalItemTemplateCache.OrderBy(v => v.Value.LevelRequired)
                .BatchesOf(712))
            {
                var metaFile = new Metafile { Name = $"ItemInfo{i}", Nodes = new List<MetafileNode>() };

                foreach (var template in from v in batch select v.Value)
                {
                    //if (template.Gender == 0)
                    //    continue;

                    var meta = template.GetMetaData();
                    metaFile.Nodes.Add(new MetafileNode(template.Name, meta));
                }

                CompileTemplate(metaFile);
                Metafiles.Add(metaFile);
                i++;
            }
        }
    }
}