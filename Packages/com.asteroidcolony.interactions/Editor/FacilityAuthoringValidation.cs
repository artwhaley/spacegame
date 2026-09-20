using System.Collections.Generic;
using Colony.Interactions;
using UnityEditor;
using UnityEngine;

public static class FacilityAuthoringValidation
{
    [MenuItem("Colony/Interactions/Validate Facilities in Open Scene")]
    private static void ValidateOpenScene()
    {
        InteractableFacility[] facilities =
            Object.FindObjectsByType<InteractableFacility>();
        int errorCount = 0;
        for (int index = 0; index < facilities.Length; index++)
        {
            errorCount += ValidateFacility(facilities[index]);
        }

        if (errorCount == 0)
        {
            Debug.Log($"Facility validation passed for {facilities.Length} facilities.");
        }
        else
        {
            Debug.LogError($"Facility validation found {errorCount} error(s).");
        }
    }

    private static int ValidateFacility(InteractableFacility facility)
    {
        int errors = 0;
        HashSet<string> ids = new HashSet<string>();
        IReadOnlyList<FacilityActivityBinding> activities = facility.Activities;
        for (int index = 0; index < activities.Count; index++)
        {
            FacilityActivityBinding activity = activities[index];
            if (activity == null || string.IsNullOrWhiteSpace(activity.ActivityId))
            {
                Debug.LogError($"{facility.name}: activity {index} has no Activity Id.", facility);
                errors++;
                continue;
            }

            if (!ids.Add(activity.ActivityId))
            {
                Debug.LogError($"{facility.name}: duplicate activity Id {activity.ActivityId}.", facility);
                errors++;
            }

            if (string.IsNullOrWhiteSpace(activity.ReservationGroup))
            {
                Debug.LogError($"{facility.name}/{activity.ActivityId}: reservation group is empty.", facility);
                errors++;
            }

            if (activity.ApproachAnchor == null || activity.ExitAnchor == null)
            {
                Debug.LogError($"{facility.name}/{activity.ActivityId}: approach and exit anchors are required.", facility);
                errors++;
            }

            errors += ValidateSegments(facility, activity.ActivityId, activity.EntrySteps);
            errors += ValidateSegment(facility, activity.ActivityId, activity.LoopSegment);
            errors += ValidateSegments(facility, activity.ActivityId, activity.ActiveSteps);
            errors += ValidateSegments(facility, activity.ActivityId, activity.ExitSteps);
        }

        return errors;
    }

    private static int ValidateSegments(
        InteractableFacility facility,
        string activityId,
        AnimationSegment[] segments)
    {
        int errors = 0;
        if (segments == null)
        {
            return errors;
        }

        for (int index = 0; index < segments.Length; index++)
        {
            errors += ValidateSegment(facility, activityId, segments[index]);
        }

        return errors;
    }

    private static int ValidateSegment(
        InteractableFacility facility,
        string activityId,
        AnimationSegment segment)
    {
        if (segment == null)
        {
            return 0;
        }

        int errors = 0;
        if (segment.Clip == null)
        {
            return 0;
        }

        if (Mathf.Abs(segment.Speed) < 0.01f || float.IsNaN(segment.Speed) || float.IsInfinity(segment.Speed))
        {
            Debug.LogError($"{facility.name}/{activityId}: {segment.Clip.name} has an invalid speed.", facility);
            errors++;
        }

        AnimationContact[] contacts = segment.Contacts;
        if (contacts == null)
        {
            return errors;
        }

        for (int index = 0; index < contacts.Length; index++)
        {
            AnimationContact contact = contacts[index];
            if (contact != null && string.IsNullOrWhiteSpace(contact.Channel))
            {
                Debug.LogError($"{facility.name}/{activityId}/{segment.Clip.name}: contact {index} has no channel.", facility);
                errors++;
            }
        }

        return errors;
    }
}
