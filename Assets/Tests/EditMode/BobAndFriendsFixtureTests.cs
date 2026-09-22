using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    /// <summary>
    /// Validates the bobandfriends.unity multi-colonist integration fixture as authored data.
    /// The scene is inspected as text instead of being opened, so running this suite can never
    /// disturb whatever scene a human currently has open in the Editor.
    /// </summary>
    public class BobAndFriendsFixtureTests
    {
        private const string SceneAssetPath = "Assets/bobandfriends.unity";
        private const string ColonistPrefabPath =
            "Assets/Prefabs/Colonists/Colonist_Synty_Male_01.prefab";

        private static readonly Regex GuidPattern =
            new Regex("guid:\\s*([0-9a-f]{32})", RegexOptions.CultureInvariant);

        private static readonly string[] ExpectedDisplayNames =
            { "Bob", "Alice", "Charlie", "Dana" };

        private static readonly string[] ExpectedSleepActivities =
            { "Sleep01", "Sleep02", "Sleep03", "Sleep04" };

        private static string FixtureText =>
            File.ReadAllText(Path.Combine(Application.dataPath, "bobandfriends.unity"));

        [Test]
        public void FixtureContainsFourCanonicalColonistInstances()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            string prefabGuid = LocalGuid(ColonistPrefabPath);

            int instances = 0;
            for (int index = 0; index < documents.Count; index++)
            {
                SceneDocument document = documents[index];
                if (document.ClassId != "1001")
                    continue;

                string source = FieldValue(document, "m_SourcePrefab");
                if (source != null && source.Contains(prefabGuid))
                    instances++;
            }

            Assert.That(
                instances,
                Is.EqualTo(4),
                "The fixture must contain exactly four canonical colonist prefab instances.");
        }

        [Test]
        public void ColonistsHaveUniqueDisplayNames()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            string identityGuid = LocalGuid("Assets/Scripts/ColonyPrototype/People/ColonistIdentity.cs");

            List<string> displayNames = new List<string>();
            for (int index = 0; index < documents.Count; index++)
            {
                SceneDocument document = documents[index];
                if (document.ClassId != "1001")
                    continue;

                string[] lines = document.Lines.ToArray();
                for (int line = 0; line < lines.Length; line++)
                {
                    if (!lines[line].Trim().StartsWith("propertyPath: displayName"))
                        continue;

                    if (line + 1 >= lines.Length)
                        continue;

                    string value = lines[line + 1].Trim();
                    if (value.StartsWith("value:"))
                        displayNames.Add(value.Substring("value:".Length).Trim());
                }
            }

            Assert.That(displayNames.Count, Is.EqualTo(4), "Four colonists must be named.");
            for (int index = 0; index < ExpectedDisplayNames.Length; index++)
            {
                Assert.That(
                    displayNames.Contains(ExpectedDisplayNames[index]),
                    Is.True,
                    $"The fixture is missing colonist {ExpectedDisplayNames[index]}.");
            }

            Assert.That(identityGuid, Is.Not.Empty);
            Assert.That(
                new HashSet<string>(displayNames).Count,
                Is.EqualTo(displayNames.Count),
                "Colonist display names must be unique.");
        }

        [Test]
        public void ColonistsHaveFourDistinctSleepAssignments()
        {
            List<SceneDocument> documents = Parse(FixtureText);

            List<string> sleepAssignments = new List<string>();
            for (int index = 0; index < documents.Count; index++)
            {
                SceneDocument document = documents[index];
                if (document.ClassId != "1001")
                    continue;

                string[] lines = document.Lines.ToArray();
                for (int line = 0; line < lines.Length; line++)
                {
                    if (!lines[line].Trim().StartsWith("propertyPath: sleepTarget.activityId"))
                        continue;

                    if (line + 1 >= lines.Length)
                        continue;

                    string value = lines[line + 1].Trim();
                    if (value.StartsWith("value:"))
                        sleepAssignments.Add(value.Substring("value:".Length).Trim());
                }
            }

            Assert.That(sleepAssignments.Count, Is.EqualTo(4));
            for (int index = 0; index < ExpectedSleepActivities.Length; index++)
            {
                string expected = ExpectedSleepActivities[index];
                int occurrences = 0;
                for (int assignment = 0; assignment < sleepAssignments.Count; assignment++)
                {
                    if (string.Equals(sleepAssignments[assignment], expected, System.StringComparison.Ordinal))
                        occurrences++;
                }

                Assert.That(occurrences, Is.EqualTo(1), $"{expected} must be assigned exactly once.");
            }
        }

        [Test]
        public void OnlyThreeColonistsAppearOnTheWorkforceRoster()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            SceneDocument workforce = FindSingleDocumentByScript(
                documents,
                "Assets/Scripts/ColonyPrototype/Workforce/WorkforceManager.cs");
            Assert.That(workforce, Is.Not.Null);

            List<string> rosteredIdentities = new List<string>();
            for (int index = 0; index < workforce.Lines.Count; index++)
            {
                string line = workforce.Lines[index].Trim();
                if (!line.StartsWith("- colonist:"))
                    continue;

                string fileId = ExtractFileId(line);
                Assert.That(fileId, Is.Not.Empty, "A roster entry has no colonist reference.");
                rosteredIdentities.Add(fileId);
            }

            Assert.That(rosteredIdentities.Count, Is.EqualTo(3));

            List<SceneDocument> identityDocuments = FindDocumentsByScript(
                documents,
                "Assets/Scripts/ColonyPrototype/People/ColonistIdentity.cs");
            Assert.That(identityDocuments.Count, Is.EqualTo(4));

            int assignedIdentities = 0;
            for (int index = 0; index < identityDocuments.Count; index++)
            {
                string fileId = identityDocuments[index].FileId;
                for (int roster = 0; roster < rosteredIdentities.Count; roster++)
                {
                    if (string.Equals(
                            rosteredIdentities[roster],
                            fileId,
                            System.StringComparison.Ordinal))
                    {
                        assignedIdentities++;
                        break;
                    }
                }
            }

            Assert.That(
                assignedIdentities,
                Is.EqualTo(3),
                "Exactly one colonist (Dana) must remain unassigned.");
        }

        [Test]
        public void CommandPodOffersFourIndependentlyReservableBeds()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            List<FacilityActivity> activities = CollectFacilityActivities(documents);

            HashSet<string> bedGroups = new HashSet<string>();
            for (int index = 0; index < ExpectedSleepActivities.Length; index++)
            {
                FacilityActivity activity = FindActivity(activities, ExpectedSleepActivities[index]);
                Assert.That(
                    activity,
                    Is.Not.Null,
                    $"{ExpectedSleepActivities[index]} must exist as an activity.");
                Assert.That(
                    bedGroups.Add(activity.ReservationGroup),
                    Is.True,
                    $"{ExpectedSleepActivities[index]} must use its own reservation group.");
            }

            Assert.That(bedGroups.Contains("Bed04"), Is.True);
            Assert.That(
                FindActivity(activities, "Command"),
                Is.Not.Null,
                "The CommandPod must keep its Command activity.");
        }

        [Test]
        public void CafeteriaOffersBothCustomerAndWorkerActivities()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            List<FacilityActivity> activities = CollectFacilityActivities(documents);

            FacilityActivity eat = FindActivity(activities, "Eat");
            FacilityActivity serveFood = FindActivity(activities, "ServeFood");
            Assert.That(eat, Is.Not.Null);
            Assert.That(serveFood, Is.Not.Null);
            Assert.That(eat.ReservationGroup, Is.EqualTo("Eat01"));
            Assert.That(serveFood.ReservationGroup, Is.EqualTo("ServeFood01"));
            Assert.That(
                eat.ReservationGroup,
                Is.Not.EqualTo(serveFood.ReservationGroup),
                "Customers and workers must not share one reservation group.");
        }

        [Test]
        public void RecreationOffersPlayAndRelaxInSeparateGroups()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            List<FacilityActivity> activities = CollectFacilityActivities(documents);

            FacilityActivity play = FindActivity(activities, "play");
            FacilityActivity relax = FindActivity(activities, "relax");
            Assert.That(play, Is.Not.Null);
            Assert.That(relax, Is.Not.Null);
            Assert.That(play.ReservationGroup, Is.EqualTo("Play01"));
            Assert.That(relax.ReservationGroup, Is.EqualTo("Relax01"));
        }

        [Test]
        public void RecreationInterestsAreAuthoredWithCooldownsAndRecoveryRates()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            SceneDocument offDuty = FindSingleDocumentByScript(
                documents,
                "Assets/Scripts/ColonyPrototype/Core/OffDutyComponent.cs");
            Assert.That(offDuty, Is.Not.Null);

            OffDutyEntry play = FindOffDutyEntry(offDuty, "play");
            OffDutyEntry relax = FindOffDutyEntry(offDuty, "relax");
            Assert.That(play, Is.Not.Null, "The play binding must be authored.");
            Assert.That(relax, Is.Not.Null, "The relax binding must be authored.");

            Assert.That(play.CooldownKey, Is.EqualTo("play"));
            Assert.That(play.CooldownGameHours, Is.EqualTo("12"));
            Assert.That(play.StimulationRecovery, Is.EqualTo("60"));
            Assert.That(play.RelaxationRecovery, Is.EqualTo("0"));

            Assert.That(relax.CooldownKey, Is.EqualTo("relax"));
            Assert.That(relax.CooldownGameHours, Is.EqualTo("12"));
            Assert.That(relax.StimulationRecovery, Is.EqualTo("0"));
            Assert.That(relax.RelaxationRecovery, Is.EqualTo("60"));

            Assert.That(
                play.CooldownKey,
                Is.Not.EqualTo(relax.CooldownKey),
                "play and relax must not share one cooldown key.");
        }

        [Test]
        public void CafeteriaRequiresTheAssignedCafeteriaWorkerForPublicService()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            SceneDocument service = FindSingleDocumentByScript(
                documents,
                "Assets/Scripts/ColonyPrototype/Core/FoodServiceComponent.cs");
            Assert.That(service, Is.Not.Null);

            Assert.That(FieldValue(service, "eatActivityId"), Is.EqualTo("Eat"));
            Assert.That(FieldValue(service, "requiresStaff"), Is.EqualTo("1"));
            // FoodSelfServicePolicy.AssignedWorkers.
            Assert.That(FieldValue(service, "selfServicePolicy"), Is.EqualTo("1"));
            Assert.That(FieldValue(service, "minimumActiveWorkers"), Is.EqualTo("1"));

            string requiredRole = FieldValue(service, "requiredRole");
            string roleGuid = ExtractGuid(requiredRole);
            Assert.That(
                roleGuid,
                Is.EqualTo(LocalGuid("Assets/GameData/Jobs/CafeteriaWorker.asset")),
                "The Cafeteria must require the Cafeteria Worker role.");

            string requiredWorkplace = FieldValue(service, "requiredWorkplace");
            string workplaceFileId = ExtractFileId(requiredWorkplace);
            Assert.That(workplaceFileId, Is.Not.Empty);

            SceneDocument workplace = FindDocumentByFileId(documents, workplaceFileId);
            Assert.That(workplace, Is.Not.Null, "The required workplace must exist.");
            Assert.That(
                workplace.ScriptGuid,
                Is.EqualTo(LocalGuid("Assets/Scripts/ColonyPrototype/Workforce/WorkplaceComponent.cs")));

            Assert.That(
                HasRoleBinding(workplace, roleGuid, "ServeFood"),
                Is.True,
                "The required workplace must map the Cafeteria Worker role to ServeFood.");
        }

        [Test]
        public void EveryJobRoleReferencedByTheFixtureResolvesToARealAsset()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            List<string> roleGuids = new List<string>();
            for (int index = 0; index < documents.Count; index++)
            {
                SceneDocument document = documents[index];
                if (document.ScriptGuid !=
                    LocalGuid("Assets/Scripts/ColonyPrototype/Workforce/WorkplaceComponent.cs"))
                {
                    continue;
                }

                for (int line = 0; line < document.Lines.Count; line++)
                {
                    if (!document.Lines[line].Trim().StartsWith("- role:"))
                        continue;

                    string guid = ExtractGuid(document.Lines[line]);
                    if (!string.IsNullOrEmpty(guid) && !roleGuids.Contains(guid))
                        roleGuids.Add(guid);
                }
            }

            Assert.That(roleGuids.Count, Is.EqualTo(3), "Three workplaces must be authored.");

            string jobsFolder = Path.Combine(Application.dataPath, "GameData", "Jobs");
            HashSet<string> authoredJobGuids = new HashSet<string>();
            string[] metaFiles = Directory.GetFiles(jobsFolder, "*.meta", SearchOption.AllDirectories);
            for (int index = 0; index < metaFiles.Length; index++)
            {
                string guid = ReadGuidFromMeta(metaFiles[index]);
                if (!string.IsNullOrEmpty(guid))
                    authoredJobGuids.Add(guid);
            }

            for (int index = 0; index < roleGuids.Count; index++)
            {
                Assert.That(
                    authoredJobGuids.Contains(roleGuids[index]),
                    Is.True,
                    $"Job role {roleGuids[index]} does not resolve to a job asset.");
            }
        }

        [Test]
        public void EveryScriptReferencedByTheFixtureResolvesLocallyOrInTheWorkingScene()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            HashSet<string> localScriptGuids = CollectScriptGuidsUnder(Path.Combine(Application.dataPath, "Scripts"));
            HashSet<string> packageScriptGuids = CollectScriptGuidsUnder(
                Path.Combine(Application.dataPath, "..", "Packages"));

            string referenceScene = Path.Combine(Application.dataPath, "Bob.unity");
            HashSet<string> referenceSceneGuids = File.Exists(referenceScene)
                ? CollectScriptGuidsFromScene(File.ReadAllText(referenceScene))
                : new HashSet<string>();

            for (int index = 0; index < documents.Count; index++)
            {
                SceneDocument document = documents[index];
                if (document.ClassId != "114" || string.IsNullOrEmpty(document.ScriptGuid))
                    continue;

                bool resolves = localScriptGuids.Contains(document.ScriptGuid) ||
                                packageScriptGuids.Contains(document.ScriptGuid) ||
                                referenceSceneGuids.Contains(document.ScriptGuid);
                Assert.That(
                    resolves,
                    Is.True,
                    $"Fixture component {document.FileId} references an unresolved script " +
                    $"{document.ScriptGuid}.");
            }
        }

        [Test]
        public void FixtureStartsBeforeAlicesShiftBegins()
        {
            List<SceneDocument> documents = Parse(FixtureText);
            SceneDocument simulation = FindSingleDocumentByScript(
                documents,
                "Assets/Scripts/ColonyPrototype/Core/SimulationManager.cs");
            Assert.That(simulation, Is.Not.Null);
            Assert.That(FieldValue(simulation, "currentGameHour"), Is.EqualTo("5.5"));
        }

        private static bool HasRoleBinding(
            SceneDocument workplace,
            string roleGuid,
            string activityId)
        {
            for (int index = 0; index < workplace.Lines.Count; index++)
            {
                if (!workplace.Lines[index].Trim().StartsWith("- role:"))
                    continue;

                if (ExtractGuid(workplace.Lines[index]) != roleGuid)
                    continue;

                for (int lookahead = index + 1; lookahead < workplace.Lines.Count; lookahead++)
                {
                    string candidate = workplace.Lines[lookahead].Trim();
                    if (candidate.StartsWith("- role:"))
                        break;

                    if (candidate.StartsWith("activityId:"))
                    {
                        return candidate.Substring("activityId:".Length).Trim() == activityId;
                    }
                }
            }

            return false;
        }

        private static FacilityActivity FindActivity(
            List<FacilityActivity> activities,
            string activityId)
        {
            for (int index = 0; index < activities.Count; index++)
            {
                if (string.Equals(
                        activities[index].ActivityId,
                        activityId,
                        System.StringComparison.Ordinal))
                {
                    return activities[index];
                }
            }

            return null;
        }

        private static List<FacilityActivity> CollectFacilityActivities(
            List<SceneDocument> documents)
        {
            string facilityGuid =
                LocalGuid("Packages/com.asteroidcolony.interactions/Runtime/InteractableFacility.cs");
            List<FacilityActivity> result = new List<FacilityActivity>();
            for (int index = 0; index < documents.Count; index++)
            {
                SceneDocument document = documents[index];
                if (document.ScriptGuid != facilityGuid)
                    continue;

                FacilityActivity current = null;
                for (int line = 0; line < document.Lines.Count; line++)
                {
                    string trimmed = document.Lines[line].Trim();
                    if (trimmed.StartsWith("- activityId:"))
                    {
                        current = new FacilityActivity();
                        current.ActivityId =
                            trimmed.Substring("- activityId:".Length).Trim();
                        result.Add(current);
                        continue;
                    }

                    if (current == null)
                        continue;

                    if (trimmed.StartsWith("reservationGroup:"))
                    {
                        current.ReservationGroup =
                            trimmed.Substring("reservationGroup:".Length).Trim();
                    }
                }
            }

            return result;
        }

        private static OffDutyEntry FindOffDutyEntry(
            SceneDocument document,
            string activityId)
        {
            List<OffDutyEntry> entries = new List<OffDutyEntry>();
            OffDutyEntry current = null;
            for (int index = 0; index < document.Lines.Count; index++)
            {
                string trimmed = document.Lines[index].Trim();
                if (trimmed.StartsWith("- activityId:"))
                {
                    current = new OffDutyEntry();
                    current.ActivityId = trimmed.Substring("- activityId:".Length).Trim();
                    entries.Add(current);
                    continue;
                }

                if (current == null)
                    continue;

                if (trimmed.StartsWith("cooldownGameHours:"))
                    current.CooldownGameHours = trimmed.Substring("cooldownGameHours:".Length).Trim();
                else if (trimmed.StartsWith("cooldownKey:"))
                    current.CooldownKey = trimmed.Substring("cooldownKey:".Length).Trim();
                else if (trimmed.StartsWith("stimulationRecoveryPerGameHour:"))
                    current.StimulationRecovery =
                        trimmed.Substring("stimulationRecoveryPerGameHour:".Length).Trim();
                else if (trimmed.StartsWith("relaxationRecoveryPerGameHour:"))
                    current.RelaxationRecovery =
                        trimmed.Substring("relaxationRecoveryPerGameHour:".Length).Trim();
            }

            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].ActivityId, activityId, System.StringComparison.Ordinal))
                    return entries[index];
            }

            return null;
        }

        private static SceneDocument FindSingleDocumentByScript(
            List<SceneDocument> documents,
            string scriptAssetPath)
        {
            List<SceneDocument> matches = FindDocumentsByScript(documents, scriptAssetPath);
            return matches.Count == 1 ? matches[0] : null;
        }

        private static List<SceneDocument> FindDocumentsByScript(
            List<SceneDocument> documents,
            string scriptAssetPath)
        {
            string guid = LocalGuid(scriptAssetPath);
            List<SceneDocument> matches = new List<SceneDocument>();
            for (int index = 0; index < documents.Count; index++)
            {
                if (documents[index].ScriptGuid == guid)
                    matches.Add(documents[index]);
            }

            return matches;
        }

        private static SceneDocument FindDocumentByFileId(
            List<SceneDocument> documents,
            string fileId)
        {
            for (int index = 0; index < documents.Count; index++)
            {
                if (documents[index].FileId == fileId)
                    return documents[index];
            }

            return null;
        }

        private static HashSet<string> CollectScriptGuidsFromScene(string sceneText)
        {
            HashSet<string> guids = new HashSet<string>();
            List<SceneDocument> documents = Parse(sceneText);
            for (int index = 0; index < documents.Count; index++)
            {
                if (documents[index].ClassId == "114" && !string.IsNullOrEmpty(documents[index].ScriptGuid))
                    guids.Add(documents[index].ScriptGuid);
            }

            return guids;
        }

        private static HashSet<string> CollectScriptGuidsUnder(string folder)
        {
            HashSet<string> guids = new HashSet<string>();
            if (!Directory.Exists(folder))
                return guids;

            string[] metaFiles = Directory.GetFiles(folder, "*.cs.meta", SearchOption.AllDirectories);
            for (int index = 0; index < metaFiles.Length; index++)
            {
                string guid = ReadGuidFromMeta(metaFiles[index]);
                if (!string.IsNullOrEmpty(guid))
                    guids.Add(guid);
            }

            return guids;
        }

        private static string LocalGuid(string assetPath)
        {
            string root = Path.Combine(Application.dataPath, "..");
            string metaPath = Path.Combine(root, assetPath + ".meta");
            if (!File.Exists(metaPath))
                return string.Empty;

            return ReadGuidFromMeta(metaPath);
        }

        private static string ReadGuidFromMeta(string metaPath)
        {
            using (StreamReader reader = new StreamReader(metaPath))
            {
                for (int line = 0; line < 12; line++)
                {
                    string current = reader.ReadLine();
                    if (current == null)
                        break;

                    if (current.StartsWith("guid:", System.StringComparison.Ordinal))
                        return current.Substring("guid:".Length).Trim();
                }
            }

            return string.Empty;
        }

        private static string FieldValue(SceneDocument document, string name)
        {
            for (int index = 0; index < document.Lines.Count; index++)
            {
                string trimmed = document.Lines[index].Trim();
                if (trimmed.StartsWith(name + ":", System.StringComparison.Ordinal))
                    return trimmed.Substring(name.Length + 1).Trim();
            }

            return null;
        }

        private static string ExtractGuid(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            Match match = GuidPattern.Match(line);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string ExtractFileId(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            Match match = Regex.Match(line, "fileID:\\s*(-?\\d+)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static List<SceneDocument> Parse(string text)
        {
            List<SceneDocument> documents = new List<SceneDocument>();
            SceneDocument current = null;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                Match header = Regex.Match(line, "^--- !u!(\\d+) &(-?\\d+)");
                if (header.Success)
                {
                    current = new SceneDocument();
                    current.ClassId = header.Groups[1].Value;
                    current.FileId = header.Groups[2].Value;
                    documents.Add(current);
                    continue;
                }

                if (current != null)
                    current.Lines.Add(line);
            }

            for (int index = 0; index < documents.Count; index++)
                documents[index].ScriptGuid = ResolveScriptGuid(documents[index]);

            return documents;
        }

        private static string ResolveScriptGuid(SceneDocument document)
        {
            if (document.ClassId != "114")
                return string.Empty;

            string script = FieldValue(document, "m_Script");
            return ExtractGuid(script);
        }

        private sealed class SceneDocument
        {
            public string ClassId;
            public string FileId;
            public string ScriptGuid;
            public readonly List<string> Lines = new List<string>();
        }

        private sealed class FacilityActivity
        {
            public string ActivityId;
            public string ReservationGroup;
        }

        private sealed class OffDutyEntry
        {
            public string ActivityId;
            public string CooldownKey;
            public string CooldownGameHours;
            public string StimulationRecovery;
            public string RelaxationRecovery;
        }
    }
}
