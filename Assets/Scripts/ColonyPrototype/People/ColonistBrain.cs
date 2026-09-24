using System;
using Colony.Interactions;
using System.Collections.Generic;
using UnityEngine;

namespace AsteroidColony
{
    public enum ColonistBrainState
    {
        Idle,
        SleepSeeking,
        Sleeping,
        WorkSeeking,
        Working,
        EatSeeking,
        Eating,
        OffDutySeeking,
        OffDutyActive
    }

    [DisallowMultipleComponent]
    public sealed class ColonistBrain : MonoBehaviour, ISimulationTickable, ISimulationTickPriority
    {
        private const float ObligationWakeLeadGameHours = 0.5f;
        private const string MealCompletedFullyReason = "meal_completed_fully";

        [SerializeField] private ColonistStatsComponent stats;
        [SerializeField] private ColonistIdentity identity;
        [SerializeField] private ColonistTargetResolver targetResolver;
        [SerializeField] private ColonistActivityRunner activityRunner;
        [SerializeField] private ColonistBrainState state = ColonistBrainState.Idle;

        private ActivityTarget sleepTargetInProgress;
        private ActivityTarget workTargetInProgress;
        private ActivityTarget eatTargetInProgress;
        private OffDutyOpportunity opportunityInProgress;
        private float actualActiveOffDutyGameHours;
        private OffDutyDrive? activeOffDutyDrive;
        private readonly OffDutyCompletionHistory offDutyCompletionHistory =
            new OffDutyCompletionHistory();
        private bool wakeRequested;
        private bool workStopRequested;
        private WorkAssignment mobileDutyAssignment;
        private bool workExcursionActive;
        private bool workExcursionHasCargo;
        private WorkplaceComponent workExcursionWorkplace;
        private bool eatStopRequested;
        private float activeMealGameHours;
        private bool offDutyStopRequested;
        private ActivityTarget localSleepOffer;
        private long localSleepOfferValidTick = -1L;
        private FoodMealCommitment foodMealCommitment;
        private long foodNeedAge;
        [NonSerialized] private FoodBid lastSubmittedFoodBid;
        [NonSerialized] private readonly List<OffDutyBid> lastSubmittedOffDutyBids =
            new List<OffDutyBid>();
        [NonSerialized] private string lastOfferChosen = "none";
        [NonSerialized] private string lastOfferRejectionReason = "none";
        private string lastDecision = "idle";
        private string lastDecisionReason = "initial_state";
        private string lastDecisionSignature;
        private ColonistFreeTimePlan latestFreeTimePlan;

        public ColonistBrainState State => state;
        public bool IsCriticallyHungry => stats != null && stats.IsCriticallyHungry;
        public bool WorkExcursionReady => workExcursionActive &&
            !workExcursionHasCargo && !activityRunner.HasActiveRequest &&
            HasCurrentWorkObligation() && !stats.IsCriticallyHungry;
        public bool ShouldAbortWorkExcursionBeforePickup => workExcursionActive &&
            !workExcursionHasCargo &&
            (stats.IsCriticallyHungry || !HasCurrentWorkObligation());
        public int SimulationTickPriority => 100;
        public OffDutyOpportunity OpportunityInProgress => opportunityInProgress;
        public OffDutyOpportunity OffDutyOpportunity => opportunityInProgress;
        public float ActualActiveOffDutyGameHours => actualActiveOffDutyGameHours;
        public bool OffDutyStopRequested => offDutyStopRequested;
        public string LastDecision => lastDecision;
        public string LastDecisionReason => lastDecisionReason;
        public OffDutyOpportunity OffDutyTarget => opportunityInProgress;
        public float OffDutyPlannedDuration =>
            opportunityInProgress != null ? opportunityInProgress.PlannedDurationGameHours : 0f;
        public float OffDutyActiveDuration => actualActiveOffDutyGameHours;
        public OffDutyCompletionHistory OffDutyCompletionHistory =>
            offDutyCompletionHistory;
        public OffDutyDrive? ActiveOffDutyDrive => activeOffDutyDrive;
        public FoodMealCommitment FoodMealCommitment => foodMealCommitment;
        public ActivityTarget LocalSleepOffer => localSleepOffer;
        public FoodBid LastSubmittedFoodBid => lastSubmittedFoodBid;
        public IReadOnlyList<OffDutyBid> LastSubmittedOffDutyBids => lastSubmittedOffDutyBids;
        public string LastOfferChosen => lastOfferChosen;
        public string LastOfferRejectionReason => lastOfferRejectionReason;
        public OffDutyDrive? PreferredOffDutyDrive => ResolvePreferredDrive();
        public ScheduledWorkOccurrence NextWork
        {
            get
            {
                return TryGetNextWork(out ScheduledWorkOccurrence occurrence)
                    ? occurrence
                    : null;
            }
        }
        public float TimeUntilWork
        {
            get
            {
                if (SimulationManager.Instance == null || identity == null)
                    return float.PositiveInfinity;

                float currentHour = SimulationManager.Instance.CurrentGameHour;
                if (HasCurrentWorkObligation())
                    return 0f;

                return TryGetNextWork(out ScheduledWorkOccurrence occurrence)
                    ? occurrence.TimeUntilStart(currentHour)
                    : float.PositiveInfinity;
            }
        }
        public float ProtectedSleepRequired =>
            latestFreeTimePlan != null ? latestFreeTimePlan.RequiredProtectedSleepDuration : 0f;
        public float MaximumDiscretionaryDuration =>
            latestFreeTimePlan != null ? latestFreeTimePlan.MaximumSafeDiscretionaryDuration : 0f;

        private void Awake()
        {
            if (stats == null)
                stats = GetComponent<ColonistStatsComponent>();
            if (identity == null)
                identity = GetComponent<ColonistIdentity>();
            if (targetResolver == null)
                targetResolver = GetComponent<ColonistTargetResolver>();
            if (activityRunner == null)
                activityRunner = GetComponent<ColonistActivityRunner>();

            if (activityRunner != null)
                activityRunner.ActivityLifecycleChanged += HandleActivityLifecycleChanged;

            if (!HasDependencies())
            {
                Debug.LogError(
                    $"{name}: ColonistBrain requires ColonistStatsComponent, " +
                    "ColonistIdentity, ColonistTargetResolver, and " +
                    "ColonistActivityRunner on the same GameObject.",
                    this);
            }
        }

        private void OnEnable()
        {
            SimulationManager.RegisterTickable(this);
        }

        private void OnDestroy()
        {
            if (activityRunner != null)
                activityRunner.ActivityLifecycleChanged -= HandleActivityLifecycleChanged;
        }

        private void OnDisable()
        {
            SimulationManager.UnregisterTickable(this);
        }

        public void SimulationTick(float deltaGameHours)
        {
            if (deltaGameHours <= 0f ||
                float.IsNaN(deltaGameHours) ||
                float.IsInfinity(deltaGameHours) ||
                !HasDependencies())
            {
                return;
            }

            switch (state)
            {
                case ColonistBrainState.Idle:
                    TickIdle();
                    break;
                case ColonistBrainState.SleepSeeking:
                    TickSleepSeeking();
                    break;
                case ColonistBrainState.Sleeping:
                    TickSleeping();
                    break;
                case ColonistBrainState.WorkSeeking:
                    TickWorkSeeking();
                    break;
                case ColonistBrainState.Working:
                    TickWorking();
                    break;
                case ColonistBrainState.EatSeeking:
                    TickEatSeeking();
                    break;
                case ColonistBrainState.Eating:
                    TickEating(deltaGameHours);
                    break;
                case ColonistBrainState.OffDutySeeking:
                    TickOffDutySeeking();
                    break;
                case ColonistBrainState.OffDutyActive:
                    TickOffDutyActive(deltaGameHours);
                    break;
            }
        }

        private void TickIdle()
        {
            if (activityRunner.HasActiveRequest)
                return;

            if (ProcessRoundOffers())
                return;

            SubmitCurrentBids();
        }

        /// <summary>
        /// The Brain is the policy owner. Managers only publish offers after every Brain has
        /// submitted its bid for the current tick; this method evaluates those offers using
        /// current facts and then attempts the physical activity request.
        /// </summary>
        private bool ProcessRoundOffers()
        {
            lastOfferChosen = "none";
            lastOfferRejectionReason = "none";
            long currentTick = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick
                : 0L;
            FoodManager foodManager = FoodManager.Instance;
            OffDutyManager offDutyManager = OffDutyManager.Instance;
            FoodOffer foodOffer = null;
            OffDutyOffer offDutyOffer = null;
            if (foodManager != null)
                foodManager.TryGetOffer(identity, currentTick, out foodOffer);
            if (offDutyManager != null)
                offDutyManager.TryGetOffer(identity, currentTick, out offDutyOffer);

            if (stats.IsCriticallyHungry)
            {
                RejectNonFoodOffers(offDutyManager, offDutyOffer, "critical_hunger");
                if (foodOffer != null && TryAcceptFoodOffer(foodManager, foodOffer, "critical_hunger"))
                    return true;
                return false;
            }

            if (HasCurrentWorkObligation())
            {
                RejectFoodOffer(foodManager, foodOffer, "current_work");
                RejectNonFoodOffers(offDutyManager, offDutyOffer, "current_work");
                RecordDecision("colonist.decision.work", "current_work");
                TryStartWork();
                return true;
            }

            bool sleepOfferAvailable = IsLocalSleepOfferAvailable(currentTick);
            if (stats.IsSleepy || stats.ShouldPreferRest)
            {
                if (!HasWorkStartingWithinPreparationWindow() && sleepOfferAvailable)
                {
                    RejectFoodOffer(foodManager, foodOffer, "sleep_preferred");
                    RejectNonFoodOffers(offDutyManager, offDutyOffer, "sleep_preferred");
                    if (TryAcceptLocalSleepOffer())
                        return true;
                }

                // Sleep is a Brain preference, not a manager decision. If the local offer is
                // unavailable, an ordinary Food offer is the explicit fallback.
                if (foodOffer != null && TryAcceptFoodOffer(foodManager, foodOffer, "sleep_unavailable"))
                    return true;
            }

            if (stats.IsHungry && foodOffer != null &&
                TryAcceptFoodOffer(foodManager, foodOffer, "hungry"))
            {
                return true;
            }

            if (offDutyOffer != null && !stats.IsHungry && !stats.IsSleepy &&
                TryAcceptOffDutyOffer(offDutyManager, offDutyOffer))
            {
                return true;
            }

            return false;
        }

        private void SubmitCurrentBids()
        {
            long currentTick = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick
                : 0L;
            float currentHour = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentGameHour
                : 0f;

            if (stats.IsHungry)
                foodNeedAge++;
            else
                foodNeedAge = 0L;

            lastSubmittedFoodBid = null;
            lastSubmittedOffDutyBids.Clear();

            if (stats.IsCriticallyHungry || stats.IsHungry || stats.IsSleepy)
            {
                if (stats.IsCriticallyHungry || stats.IsHungry)
                {
                    lastSubmittedFoodBid = new FoodBid(
                        identity,
                        transform.position,
                        currentHour,
                        stats.Hunger,
                        stats.IsCriticallyHungry,
                        stats.IsSleepy ? 1 : 0,
                        currentTick,
                        foodNeedAge);
                    FoodManager.Instance?.SubmitBid(lastSubmittedFoodBid);
                }

                if (stats.IsSleepy && !HasWorkStartingWithinPreparationWindow())
                    PrepareLocalSleepOffer(currentTick);
                return;
            }

            if (HasWorkStartingWithinPreparationWindow())
            {
                RecordDecision("colonist.decision.blocked", "work_preparation_window");
                return;
            }

            SubmitOffDutyBids(currentTick, currentHour);
        }

        private void PrepareLocalSleepOffer(long currentTick)
        {
            localSleepOffer = null;
            localSleepOfferValidTick = -1L;
            if (targetResolver.TryResolveTarget(ActivityPurpose.Sleep, out ActivityTarget target) &&
                IsTargetValid(target) &&
                !target.Facility.IsReserved(GetReservationGroup(target)))
            {
                localSleepOffer = target;
                localSleepOfferValidTick = currentTick + 1L;
            }
        }

        private bool IsLocalSleepOfferAvailable(long currentTick)
        {
            return localSleepOffer != null &&
                   localSleepOfferValidTick == currentTick &&
                   IsTargetValid(localSleepOffer) &&
                   !localSleepOffer.Facility.IsReserved(GetReservationGroup(localSleepOffer));
        }

        private bool TryAcceptLocalSleepOffer()
        {
            if (!IsLocalSleepOfferAvailable(localSleepOfferValidTick))
                return false;
            ActivityTarget target = localSleepOffer;
            localSleepOffer = null;
            localSleepOfferValidTick = -1L;
            if (!activityRunner.RequestActivity(target.Facility, target.ActivityId))
                return false;
            sleepTargetInProgress = target;
            wakeRequested = false;
            state = ColonistBrainState.SleepSeeking;
            lastOfferChosen = "sleep";
            RecordDecision("colonist.decision.sleep", "sleep_offer_accepted", target.Facility);
            return true;
        }

        private void SubmitOffDutyBids(long currentTick, float currentHour)
        {
            if (OffDutyManager.Instance == null || SimulationManager.Instance == null)
                return;
            latestFreeTimePlan = ColonistFreeTimePlanner.Calculate(
                stats,
                identity,
                GetComponent<ColonistAssignments>(),
                currentHour);
            if (!latestFreeTimePlan.HasBudget)
                return;

            OffDutyDrive? primary = ResolvePreferredDrive();
            if (!primary.HasValue)
            {
                RecordDecision("colonist.decision.blocked", "discretionary_needs_satisfied");
                return;
            }

            SubmitOffDutyBid(primary.Value, 0, currentTick, currentHour);
            OffDutyDrive? secondary = ResolveSecondaryDrive(primary.Value);
            if (secondary.HasValue)
                SubmitOffDutyBid(secondary.Value, 1, currentTick, currentHour);
        }

        private void SubmitOffDutyBid(
            OffDutyDrive drive,
            int preferenceRank,
            long currentTick,
            float currentHour)
        {
            OffDutyBid bid = new OffDutyBid(
                identity,
                transform.position,
                drive,
                latestFreeTimePlan.MaximumSafeDiscretionaryDuration,
                offDutyCompletionHistory,
                currentHour,
                preferenceRank,
                currentTick);
            lastSubmittedOffDutyBids.Add(bid);
            OffDutyManager.Instance.SubmitBid(bid);
        }

        private bool TryAcceptFoodOffer(
            FoodManager manager,
            FoodOffer offer,
            string reason)
        {
            if (manager == null || offer == null || offer.Opportunity == null ||
                !IsTargetValid(offer.Opportunity.Target) ||
                !activityRunner.RequestActivity(
                    offer.Opportunity.Target.Facility,
                    offer.Opportunity.Target.ActivityId))
            {
                manager?.RemoveOffer(offer, false, "stale_or_request_failed");
                return false;
            }

            if (!manager.TryReserveMeal(offer, activityRunner, out foodMealCommitment))
            {
                activityRunner.Stop();
                manager.RemoveOffer(offer, false, "inventory_reservation_failed");
                return false;
            }

            manager.RemoveOffer(offer, true, reason);
            lastOfferChosen = "food";
            eatTargetInProgress = offer.Opportunity.Target;
            eatStopRequested = false;
            activeMealGameHours = 0f;
            state = ColonistBrainState.EatSeeking;
            RecordDecision(
                "colonist.decision.eat",
                reason,
                offer.Opportunity.Service != null ? offer.Opportunity.Service.Facility : null,
                new SimulationLogField("hunger", stats.Hunger));
            return true;
        }

        private bool TryAcceptOffDutyOffer(OffDutyManager manager, OffDutyOffer offer)
        {
            if (manager == null || offer == null || offer.Opportunity == null ||
                !IsTargetValid(offer.Opportunity.Target) ||
                !activityRunner.RequestActivity(
                    offer.Opportunity.Target.Facility,
                    offer.Opportunity.Target.ActivityId))
            {
                manager?.RemoveOffer(offer, false, "stale_or_request_failed");
                return false;
            }

            manager.RemoveOffer(offer, true, "brain_choice");
            lastOfferChosen = "offduty";
            opportunityInProgress = offer.Opportunity;
            activeOffDutyDrive = offer.Bid.DesiredDrive;
            actualActiveOffDutyGameHours = 0f;
            offDutyStopRequested = false;
            state = ColonistBrainState.OffDutySeeking;
            RecordDecision("colonist.decision.offduty", "offer_accepted", offer.Opportunity.Provider.Facility);
            return true;
        }

        private void RejectFoodOffer(FoodManager manager, FoodOffer offer, string reason)
        {
            if (manager != null && offer != null)
            {
                manager.RemoveOffer(offer, false, reason);
                lastOfferRejectionReason = reason;
            }
        }

        private void RejectNonFoodOffers(OffDutyManager manager, OffDutyOffer offer, string reason)
        {
            if (manager != null && offer != null)
            {
                manager.RemoveOffer(offer, false, reason);
                lastOfferRejectionReason = reason;
            }
        }

        private static string GetReservationGroup(ActivityTarget target)
        {
            return target != null && target.Facility != null &&
                   target.Facility.TryGetBinding(target.ActivityId, out FacilityActivityBinding binding)
                ? binding.ReservationGroup
                : string.Empty;
        }

        private void TickSleepSeeking()
        {
            if (!IsTargetValid(sleepTargetInProgress))
            {
                FinishSleepLifecycle();
                return;
            }

            if (!wakeRequested &&
                (HasCurrentWorkObligation() ||
                 HasWorkStartingWithinPreparationWindow() ||
                 stats.IsCriticallyHungry))
            {
                RequestWake();
            }

            if (activityRunner.IsActivityActive &&
                string.Equals(
                    activityRunner.ActiveActivityId,
                    sleepTargetInProgress.ActivityId,
                    StringComparison.Ordinal))
            {
                state = ColonistBrainState.Sleeping;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishSleepLifecycle();
        }

        private void TickSleeping()
        {
            if (!IsTargetValid(sleepTargetInProgress))
            {
                FinishSleepLifecycle();
                return;
            }

            if (!wakeRequested &&
                (stats.Fatigue <= 0f ||
                 HasCurrentWorkObligation() ||
                 HasWorkStartingWithinPreparationWindow() ||
                 stats.IsCriticallyHungry))
            {
                RequestWake();
            }

            if (wakeRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishSleepLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishSleepLifecycle();
        }

        private void RequestWake()
        {
            if (wakeRequested)
                return;

            wakeRequested = true;
            RecordDecision(
                "colonist.decision.interrupted",
                stats.IsCriticallyHungry ? "sleep_interrupted_for_critical_hunger" :
                HasCurrentWorkObligation() ? "current_work" : "work_preparation");
            activityRunner.Stop();
        }

        private void FinishSleepLifecycle()
        {
            wakeRequested = false;
            sleepTargetInProgress = null;
            state = ColonistBrainState.Idle;
        }

        private bool TryStartSleep()
        {
            if (stats.IsCriticallyHungry ||
                HasWorkStartingWithinPreparationWindow() ||
                !targetResolver.TryResolveTarget(
                    ActivityPurpose.Sleep,
                    out ActivityTarget target) ||
                !IsTargetValid(target) ||
                !activityRunner.RequestActivity(target.Facility, target.ActivityId))
            {
                return false;
            }

            sleepTargetInProgress = target;
            wakeRequested = false;
            state = ColonistBrainState.SleepSeeking;
            return true;
        }

        private void TickWorkSeeking()
        {
            if (mobileDutyAssignment != null)
            {
                if (stats.IsCriticallyHungry || !HasCurrentWorkObligation())
                {
                    RequestWorkStop();
                    FinishWorkLifecycle();
                    return;
                }

                PersonnelRouteRunner routeRunner = GetComponent<PersonnelRouteRunner>();
                if (routeRunner == null || routeRunner.State == PersonnelRouteExecutionState.Failed ||
                    routeRunner.State == PersonnelRouteExecutionState.Cancelled)
                {
                    FinishWorkLifecycle();
                    return;
                }
                if (routeRunner.State == PersonnelRouteExecutionState.Completed)
                    state = ColonistBrainState.Working;
                return;
            }

            if (stats.IsCriticallyHungry)
            {
                RequestWorkStop();
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (workTargetInProgress == null || !workTargetInProgress.IsConfigured ||
                !HasCurrentWorkObligation())
            {
                RequestWorkStop();
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (activityRunner.IsActivityActive &&
                string.Equals(
                    activityRunner.ActiveActivityId,
                    workTargetInProgress.ActivityId,
                    StringComparison.Ordinal))
            {
                state = ColonistBrainState.Working;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishWorkLifecycle();
        }

        private void TickWorking()
        {
            if (workExcursionActive)
            {
                // Once loaded, the worker owns the run through delivery even if a
                // shift boundary or critical need occurs during the trip.
                if (workExcursionHasCargo)
                    return;
                return;
            }

            if (mobileDutyAssignment != null)
            {
                WalkingFreightCarrierComponent carrier =
                    GetComponent<WalkingFreightCarrierComponent>();
                if (carrier != null && carrier.HasCargo)
                    return;
                if (stats.IsCriticallyHungry || !HasCurrentWorkObligation())
                {
                    RecordDecision(
                        stats.IsCriticallyHungry
                            ? "colonist.decision.interrupted"
                            : "colonist.decision.reconsidered",
                        stats.IsCriticallyHungry
                            ? "work_interrupted_for_critical_hunger"
                            : "work_ended");
                    FinishWorkLifecycle();
                }
                return;
            }

            if (workTargetInProgress == null || !workTargetInProgress.IsConfigured)
            {
                RequestWorkStop();
            }
            else if (!workStopRequested &&
                     (stats.IsCriticallyHungry ||
                      !HasCurrentWorkObligation() ||
                      !IsCurrentWorkActivity()))
            {
                RequestWorkStop();
            }

            if (workStopRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishWorkLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishWorkLifecycle();
        }

        private bool TryStartWork()
        {
            if (WorkforceManager.Instance != null && identity != null &&
                SimulationManager.Instance != null &&
                WorkforceManager.Instance.TryGetCurrentDuty(
                    identity,
                    SimulationManager.Instance.CurrentGameHour,
                    out WorkAssignment assignment) &&
                assignment.Workplace.ExecutionMode == WorkplaceExecutionMode.MobileDuty)
            {
                PersonnelRouteRunner routeRunner = GetComponent<PersonnelRouteRunner>();
                string routeFailure = "personnel_route_runner_missing";
                if (routeRunner == null || assignment.Workplace.DutyAnchor == null ||
                    !routeRunner.TryStartRoute(assignment.Workplace.DutyAnchor, out routeFailure))
                {
                    RecordDecision("colonist.decision.blocked",
                        "mobile_duty_route_unavailable",
                        assignment.Workplace,
                        new SimulationLogField("routeFailure", routeFailure));
                    return false;
                }

                mobileDutyAssignment = assignment;
                workStopRequested = false;
                state = ColonistBrainState.WorkSeeking;
                RecordDecision("colonist.decision.work", "mobile_duty_route_started",
                    assignment.Workplace);
                return true;
            }

            if (!targetResolver.TryResolveTarget(
                    ActivityPurpose.Work,
                    out ActivityTarget target) ||
                !IsTargetValid(target) ||
                !activityRunner.RequestActivity(target.Facility, target.ActivityId))
            {
                RecordDecision("colonist.decision.blocked", "work_target_unavailable");
                return false;
            }

            workTargetInProgress = target;
            workStopRequested = false;
            state = ColonistBrainState.WorkSeeking;
            return true;
        }

        public bool CanBeginWorkExcursion(WorkplaceComponent workplace)
        {
            if (workplace == null || state != ColonistBrainState.Working ||
                workTargetInProgress == null || workExcursionActive ||
                stats.IsCriticallyHungry || !HasCurrentWorkObligation() ||
                WorkforceManager.Instance == null || identity == null ||
                !WorkforceManager.Instance.TryGetAssignment(identity, out WorkAssignment assignment) ||
                assignment.Workplace != workplace ||
                workplace.ExecutionMode != WorkplaceExecutionMode.FacilityActivity ||
                activityRunner == null || !activityRunner.IsActivityActive)
            {
                return false;
            }

            return true;
        }

        public bool TryBeginWorkExcursion(WorkplaceComponent workplace)
        {
            if (!CanBeginWorkExcursion(workplace))
                return false;

            workExcursionActive = true;
            workExcursionHasCargo = false;
            workExcursionWorkplace = workplace;
            activityRunner.Stop();
            RecordDecision("colonist.decision.work", "authorized_work_excursion", workplace);
            return true;
        }

        public void SetWorkExcursionCargo(bool hasCargo)
        {
            if (workExcursionActive)
                workExcursionHasCargo = hasCargo;
        }

        public void CompleteWorkExcursion()
        {
            if (!workExcursionActive)
                return;

            workExcursionActive = false;
            workExcursionHasCargo = false;
            workExcursionWorkplace = null;
            FinishWorkLifecycle();
        }

        private void RequestWorkStop()
        {
            if (workStopRequested)
                return;

            workStopRequested = true;
            RecordDecision(
                stats.IsCriticallyHungry
                    ? "colonist.decision.interrupted"
                    : "colonist.decision.reconsidered",
                stats.IsCriticallyHungry
                    ? "work_interrupted_for_critical_hunger"
                    : "work_ended");
            if (stats.IsCriticallyHungry)
            {
                SimulationLogManager.RecordEvent(
                    "work.left_for_critical_need",
                    "Work",
                    "Warning",
                    LogSubject,
                    workTargetInProgress != null ? workTargetInProgress.Facility : null,
                    new SimulationLogField("reason", "work_interrupted_for_critical_hunger"),
                    new SimulationLogField("hunger", stats.Hunger));
            }
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
            GetComponent<PersonnelRouteRunner>()?.StopRoute();
        }

        private bool IsCurrentWorkActivity()
        {
            return activityRunner.HasActiveRequest &&
                   workTargetInProgress != null &&
                   string.Equals(
                       activityRunner.CurrentActivityId,
                       workTargetInProgress.ActivityId,
                       StringComparison.Ordinal);
        }

        private void FinishWorkLifecycle()
        {
            workTargetInProgress = null;
            mobileDutyAssignment = null;
            workStopRequested = false;
            state = ColonistBrainState.Idle;
        }

        private void TickEatSeeking()
        {
            bool targetValid = IsFoodTargetValid(eatTargetInProgress);
            bool accessValid = targetValid && CanRequesterStillAccessFood(eatTargetInProgress);
            if (!targetValid || !accessValid)
            {
                RequestEatStop(!targetValid ? "food_target_invalid" : "food_access_lost");
                if (!activityRunner.HasActiveRequest)
                    FinishEatLifecycle();
                return;
            }

            if (activityRunner.IsActivityActive &&
                string.Equals(activityRunner.ActiveActivityId,
                    eatTargetInProgress.ActivityId, StringComparison.Ordinal))
            {
                state = ColonistBrainState.Eating;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishEatLifecycle();
        }

        private void TickEating(float deltaGameHours)
        {
            // A served meal is a committed interaction. Work, sleep, staffing and
            // changing need levels may all wait until its full duration has elapsed.
            if (foodMealCommitment == null || !foodMealCommitment.Consumed)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishEatLifecycle();
                return;
            }

            if (!foodMealCommitment.Completed)
            {
                activeMealGameHours += deltaGameHours;
                if (activeMealGameHours + 0.000001f >= foodMealCommitment.DurationGameHours)
                {
                    foodMealCommitment.Completed = true;
                    activityRunner.SetActiveActivityLock(false);
                    stats.AdjustHunger(-foodMealCommitment.HungerRecovery);
                    SimulationLogManager.RecordEvent(
                        "food.meal_completed", "Food", "Info", LogSubject,
                        foodMealCommitment.Service != null
                            ? foodMealCommitment.Service.Facility : null,
                        new SimulationLogField("hungerRecovery", foodMealCommitment.HungerRecovery),
                        new SimulationLogField("durationGameHours", foodMealCommitment.DurationGameHours),
                        new SimulationLogField("hunger", stats.Hunger));
                    RequestEatStop(MealCompletedFullyReason);
                }
            }

            if (!activityRunner.HasActiveRequest)
                FinishEatLifecycle();
        }

        private void RequestEatStop(string reason)
        {
            if (eatStopRequested)
                return;

            eatStopRequested = true;
            RecordDecision(
                reason == MealCompletedFullyReason
                    ? "colonist.decision.eat_completed"
                    : "colonist.decision.interrupted",
                reason);
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private bool CanRequesterStillAccessFood(ActivityTarget target)
        {
            if (FoodManager.Instance == null || target == null)
                return false;

            float currentGameHour = SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentGameHour
                : 0f;
            return FoodManager.Instance.CanRequesterStillAccess(
                target,
                identity,
                currentGameHour);
        }

        private bool IsCurrentEatActivity()
        {
            return activityRunner.HasActiveRequest &&
                   eatTargetInProgress != null &&
                   string.Equals(
                       activityRunner.CurrentActivityId,
                       eatTargetInProgress.ActivityId,
                       StringComparison.Ordinal);
        }

        private void FinishEatLifecycle()
        {
            if (foodMealCommitment != null && !foodMealCommitment.Consumed &&
                FoodManager.Instance != null)
            {
                FoodManager.Instance.ReleaseMeal(foodMealCommitment);
            }
            foodMealCommitment = null;
            eatTargetInProgress = null;
            eatStopRequested = false;
            activeMealGameHours = 0f;
            state = ColonistBrainState.Idle;
        }

        private OffDutyDrive? ResolvePreferredDrive()
        {
            if (stats == null)
                return null;

            bool wantsStimulation = stats.NeedsStimulation;
            bool wantsRelaxation = stats.NeedsRelaxation;
            if (!wantsStimulation && !wantsRelaxation)
                return null;

            if (wantsStimulation && !wantsRelaxation)
                return OffDutyDrive.Stimulation;

            if (wantsRelaxation && !wantsStimulation)
                return OffDutyDrive.Relaxation;

            float stimulationPressure =
                NormalizedPressure(stats.StimulationNeed, stats.StimulationNeedThreshold);
            float relaxationPressure =
                NormalizedPressure(stats.RelaxationNeed, stats.RelaxationNeedThreshold);

            // Deterministic tie break: stimulation wins an exact tie. No randomness yet.
            return relaxationPressure > stimulationPressure
                ? OffDutyDrive.Relaxation
                : OffDutyDrive.Stimulation;
        }

        private OffDutyDrive? ResolveSecondaryDrive(OffDutyDrive primary)
        {
            OffDutyDrive other = primary == OffDutyDrive.Stimulation
                ? OffDutyDrive.Relaxation
                : OffDutyDrive.Stimulation;
            bool active = other == OffDutyDrive.Stimulation
                ? stats.NeedsStimulation
                : stats.NeedsRelaxation;
            return active ? other : (OffDutyDrive?)null;
        }

        private static float NormalizedPressure(float need, float threshold)
        {
            if (threshold <= 0f || float.IsNaN(threshold))
                return need > 0f ? float.PositiveInfinity : 0f;

            return need / threshold;
        }

        private static string DescribeDrive(OffDutyDrive drive)
        {
            return drive == OffDutyDrive.Stimulation ? "stimulation" : "relaxation";
        }

        private void TickOffDutySeeking()
        {
            if (!IsOffDutyOpportunityLive() ||
                HasCurrentWorkObligation() ||
                HasWorkStartingWithinPreparationWindow() ||
                stats.IsHungry ||
                stats.IsSleepy ||
                stats.IsCriticallyHungry)
            {
                RequestOffDutyStop(
                    stats.IsCriticallyHungry ? "offduty_interrupted_for_critical_hunger" :
                    stats.IsSleepy ? "sleepy" :
                    stats.IsHungry ? "hungry" :
                    HasCurrentWorkObligation() ? "current_work" :
                    HasWorkStartingWithinPreparationWindow() ? "work_preparation" :
                    "provider_unavailable");
                if (!activityRunner.HasActiveRequest)
                    FinishOffDutyLifecycle();
                return;
            }

            if (activityRunner.IsActivityActive && IsCurrentOffDutyActivity())
            {
                state = ColonistBrainState.OffDutyActive;
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishOffDutyLifecycle();
        }

        private void TickOffDutyActive(float deltaGameHours)
        {
            if (!IsOffDutyOpportunityLive() ||
                stats.IsCriticallyHungry ||
                stats.IsHungry ||
                stats.IsSleepy ||
                HasCurrentWorkObligation() ||
                HasWorkStartingWithinPreparationWindow())
            {
                RequestOffDutyStop(
                    stats.IsCriticallyHungry ? "offduty_interrupted_for_critical_hunger" :
                    stats.IsSleepy ? "sleepy" :
                    stats.IsHungry ? "hungry" :
                    HasCurrentWorkObligation() ? "current_work" :
                    HasWorkStartingWithinPreparationWindow() ? "work_preparation" :
                    "required_staff_unavailable");
            }
            else
            {
                actualActiveOffDutyGameHours += deltaGameHours;
                if (actualActiveOffDutyGameHours >= opportunityInProgress.PlannedDurationGameHours)
                {
                    RecordDecision(
                        "colonist.decision.reconsidered",
                        "planned_duration_complete",
                        opportunityInProgress.Target.Facility);
                    RequestOffDutyStop("planned_duration_complete");
                }
            }

            if (offDutyStopRequested)
            {
                if (!activityRunner.HasActiveRequest)
                    FinishOffDutyLifecycle();
                return;
            }

            if (!activityRunner.HasActiveRequest)
                FinishOffDutyLifecycle();
        }

        private void RequestOffDutyStop(string reason)
        {
            if (offDutyStopRequested)
                return;

            offDutyStopRequested = true;
            bool completed = string.Equals(
                reason,
                "planned_duration_complete",
                StringComparison.Ordinal);

            // Cooldown starts only on a genuine full-duration completion. Discovery,
            // reservation, walking, entry, becoming active and being interrupted early all
            // leave the activity immediately eligible again.
            string cooldownKey = string.Empty;
            float cooldownUntil = 0f;
            if (completed &&
                opportunityInProgress != null &&
                opportunityInProgress.Activity != null)
            {
                OffDutyActivityBinding completedActivity = opportunityInProgress.Activity;
                cooldownKey = completedActivity.CooldownKey;
                float currentGameHour = SimulationManager.Instance != null
                    ? SimulationManager.Instance.CurrentGameHour
                    : 0f;
                offDutyCompletionHistory.RecordCompletion(cooldownKey, currentGameHour);
                if (offDutyCompletionHistory.TryGetCooldownUntil(
                        cooldownKey,
                        completedActivity.CooldownGameHours,
                        out float recordedUntil))
                {
                    cooldownUntil = recordedUntil;
                }
            }

            RecordDecision(
                completed
                    ? "colonist.decision.reconsidered"
                    : "colonist.decision.interrupted",
                reason,
                opportunityInProgress != null ? opportunityInProgress.Target.Facility : null);
            SimulationLogManager.RecordEvent(
                completed ? "offduty.completed" : "offduty.interrupted",
                "OffDuty",
                "Info",
                LogSubject,
                opportunityInProgress != null ? opportunityInProgress.Target.Facility : null,
                new SimulationLogField("reason", reason),
                new SimulationLogField("activeDuration", actualActiveOffDutyGameHours),
                new SimulationLogField(
                    "drive",
                    activeOffDutyDrive.HasValue
                        ? DescribeDrive(activeOffDutyDrive.Value)
                        : "unknown"),
                new SimulationLogField("cooldownKey", cooldownKey),
                new SimulationLogField("cooldownUntil", cooldownUntil));
            if (activityRunner.HasActiveRequest)
                activityRunner.Stop();
        }

        private void FinishOffDutyLifecycle()
        {
            opportunityInProgress = null;
            actualActiveOffDutyGameHours = 0f;
            offDutyStopRequested = false;
            activeOffDutyDrive = null;
            state = ColonistBrainState.Idle;
        }

        private bool IsCurrentOffDutyActivity()
        {
            return opportunityInProgress != null &&
                   activityRunner.HasActiveRequest &&
                   string.Equals(
                       activityRunner.CurrentActivityId,
                       opportunityInProgress.Target.ActivityId,
                       StringComparison.Ordinal);
        }

        private bool IsOffDutyOpportunityLive()
        {
            if (opportunityInProgress == null ||
                opportunityInProgress.Provider == null ||
                !opportunityInProgress.Provider.IsLive(opportunityInProgress.Activity))
            {
                return false;
            }

            OffDutyActivityBinding activity = opportunityInProgress.Activity;
            if (!activity.RequiresStaff)
                return true;

            return WorkforceManager.Instance != null &&
                   WorkforceManager.Instance.HasEnoughActiveWorkers(
                       activity.RequiredWorkplace,
                       activity.RequiredRole,
                       activity.MinimumActiveWorkers,
                       0f,
                       SimulationManager.Instance != null
                           ? SimulationManager.Instance.CurrentGameHour
                           : 0f);
        }

        private bool HasUpcomingObligationWithin(float gameHours)
        {
            return HasCurrentWorkObligation() || HasWorkStartingWithin(gameHours);
        }

        private bool HasWorkStartingWithinPreparationWindow()
        {
            return HasWorkStartingWithin(ObligationWakeLeadGameHours);
        }

        private bool HasWorkStartingWithin(float gameHours)
        {
            if (gameHours < 0f ||
                float.IsNaN(gameHours) ||
                float.IsInfinity(gameHours) ||
                identity == null ||
                WorkforceManager.Instance == null ||
                SimulationManager.Instance == null ||
                WorkforceManager.Instance.TryGetCurrentShift(
                    identity,
                    SimulationManager.Instance.CurrentGameHour,
                    out _))
            {
                return false;
            }

            if (!WorkforceManager.Instance.TryGetNextShift(
                    identity,
                    SimulationManager.Instance.CurrentGameHour,
                    out ScheduledWorkOccurrence nextShift))
            {
                return false;
            }

            return nextShift.TimeUntilStart(SimulationManager.Instance.CurrentGameHour) <= gameHours;
        }

        private bool HasCurrentWorkObligation()
        {
            return identity != null &&
                   WorkforceManager.Instance != null &&
                   SimulationManager.Instance != null &&
                   WorkforceManager.Instance.TryGetCurrentShift(
                       identity,
                       SimulationManager.Instance.CurrentGameHour,
                       out _);
        }

        private bool TryGetNextWork(out ScheduledWorkOccurrence occurrence)
        {
            occurrence = null;
            return identity != null &&
                   WorkforceManager.Instance != null &&
                   SimulationManager.Instance != null &&
                   WorkforceManager.Instance.TryGetNextShift(
                       identity,
                       SimulationManager.Instance.CurrentGameHour,
                       out occurrence);
        }

        /// <summary>
        /// Canonical structured-log subject for this colonist. Game-side colonist events are
        /// attributed to the ColonistIdentity instead of to whichever sibling component emitted
        /// them, so querying "this colonist's history" cannot silently miss needs, decisions or
        /// activity lifecycle entries recorded by another of the colonist's components.
        /// </summary>
        private UnityEngine.Object LogSubject
        {
            get
            {
                if (identity == null)
                    identity = GetComponent<ColonistIdentity>();

                return identity != null ? (UnityEngine.Object)identity : (UnityEngine.Object)this;
            }
        }

        private void RecordDecision(
            string eventKey,
            string reason,
            UnityEngine.Object target = null,
            params SimulationLogField[] fields)
        {
            string targetName = target != null ? target.name : string.Empty;
            string signature = eventKey + "|" + reason + "|" + targetName;
            if (string.Equals(signature, lastDecisionSignature, StringComparison.Ordinal))
                return;

            lastDecisionSignature = signature;
            lastDecision = eventKey;
            lastDecisionReason = reason ?? string.Empty;
            SimulationLogManager.RecordEvent(
                eventKey,
                "Decision",
                "Info",
                LogSubject,
                target,
                MergeFields(new SimulationLogField("reason", reason), fields));
        }

        private void HandleActivityLifecycleChanged(ActivityLifecycleEvent activityEvent)
        {
            if (activityEvent == null)
                return;

            if (foodMealCommitment != null &&
                foodMealCommitment.Service != null &&
                activityEvent.Facility == foodMealCommitment.Service.Facility &&
                string.Equals(
                    activityEvent.ActivityId,
                    foodMealCommitment.Service.EatActivityId,
                    StringComparison.Ordinal))
            {
                if (activityEvent.Kind == ActivityLifecycleEventKind.ActiveStarted)
                {
                    if (FoodManager.Instance == null ||
                        !FoodManager.Instance.CommitMeal(foodMealCommitment))
                    {
                        activityRunner.Stop();
                    }
                    else
                    {
                        activeMealGameHours = 0f;
                        activityRunner.SetActiveActivityLock(true);
                        state = ColonistBrainState.Eating;
                    }
                }
                else if (activityEvent.Kind == ActivityLifecycleEventKind.Failed ||
                         activityEvent.Kind == ActivityLifecycleEventKind.Released)
                {
                    activityRunner.SetActiveActivityLock(false);
                    if (!foodMealCommitment.Consumed)
                        FoodManager.Instance?.ReleaseMeal(foodMealCommitment);
                }
            }

            string eventKey;
            switch (activityEvent.Kind)
            {
                case ActivityLifecycleEventKind.Requested:
                    eventKey = "activity.requested";
                    break;
                case ActivityLifecycleEventKind.Reserved:
                    eventKey = "activity.reserved";
                    break;
                case ActivityLifecycleEventKind.NavigationStarted:
                    eventKey = "activity.navigation_started";
                    break;
                case ActivityLifecycleEventKind.ActiveStarted:
                    eventKey = "activity.active_started";
                    break;
                case ActivityLifecycleEventKind.ExitStarted:
                    eventKey = "activity.exit_started";
                    break;
                case ActivityLifecycleEventKind.Released:
                    eventKey = "activity.released";
                    break;
                case ActivityLifecycleEventKind.Failed:
                    eventKey = "activity.failed";
                    break;
                default:
                    return;
            }

            SimulationLogManager.RecordEvent(
                eventKey,
                "Activity",
                activityEvent.Kind == ActivityLifecycleEventKind.Failed ? "Warning" : "Info",
                LogSubject,
                activityEvent.Facility,
                new SimulationLogField("activityId", activityEvent.ActivityId),
                new SimulationLogField("reservationGroup", activityEvent.ReservationGroup),
                new SimulationLogField("reason", activityEvent.Reason));
        }

        private static SimulationLogField[] MergeFields(
            SimulationLogField first,
            SimulationLogField[] remaining)
        {
            int count = remaining != null ? remaining.Length : 0;
            SimulationLogField[] fields = new SimulationLogField[count + 1];
            fields[0] = first;
            if (remaining != null)
                Array.Copy(remaining, 0, fields, 1, remaining.Length);
            return fields;
        }

        private static bool IsTargetValid(ActivityTarget target)
        {
            return target != null && target.IsConfigured;
        }

        private bool IsFoodTargetValid(ActivityTarget target)
        {
            return IsTargetValid(target) &&
                   FoodManager.Instance != null &&
                   FoodManager.Instance.IsFoodTargetLive(target);
        }

        private bool HasDependencies()
        {
            return stats != null &&
                   identity != null &&
                   targetResolver != null &&
                   activityRunner != null;
        }
    }
}
