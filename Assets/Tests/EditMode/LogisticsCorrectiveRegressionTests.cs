using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AsteroidColony;
using NUnit.Framework;
using UnityEngine;

namespace AsteroidColony.Tests
{
    [Category("Core")]
    public sealed class LogisticsCorrectiveRegressionTests
    {
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private ResourceDefinition resource;

        [TearDown]
        public void TearDown()
        {
            for (int index = sceneObjects.Count - 1; index >= 0; index--)
                if (sceneObjects[index] != null)
                    Object.DestroyImmediate(sceneObjects[index]);
            sceneObjects.Clear();
            if (resource != null)
                Object.DestroyImmediate(resource);
        }

        [Test]
        public void CancelledShuttleRequestCannotBeRevivedOrCompleted()
        {
            ShuttleTransferEndpoint origin = CreateEndpoint("Origin");
            ShuttleTransferEndpoint destination = CreateEndpoint("Destination");
            var request = new ShuttleTransportRequest("request-1", "correlation-1",
                ShuttlePayloadType.Passenger, origin, destination, 0, 0L);
            int completedEvents = 0;
            request.Completed += _ => completedEvents++;

            Assert.That(request.TryCancel(), Is.True);
            request.SetState(ShuttleTransportRequestState.WaitingForPilot);
            request.SetState(ShuttleTransportRequestState.InTransit);
            request.Complete();

            Assert.That(request.State, Is.EqualTo(ShuttleTransportRequestState.Cancelled));
            Assert.That(request.IsPhysicallyTransferred, Is.False);
            Assert.That(completedEvents, Is.Zero);

            var completed = new ShuttleTransportRequest("request-2", "correlation-2",
                ShuttlePayloadType.Passenger, origin, destination, 0, 0L);
            completed.Completed += _ => completedEvents++;
            completed.Complete();
            completed.Complete();
            Assert.That(completed.State, Is.EqualTo(ShuttleTransportRequestState.Completed));
            Assert.That(completed.IsPhysicallyTransferred, Is.True);
            Assert.That(completedEvents, Is.EqualTo(1));
        }

        [Test]
        public void BlockedIntermediateShuttlePickupCanRetryAfterCapacityReturns()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Source", 2f, out InventoryComponent sourceInventory);
            LogisticsStockComponent stage = CreateStock("Stage", 2f, out InventoryComponent stageInventory);
            LogisticsStockComponent destination = CreateStock("Destination", 2f, out _);
            resource = CreateResource("Retry freight");
            Assert.That(stageInventory.Add(resource, 1f), Is.EqualTo(1f));
            Assert.That(stageInventory.TryReserveOwned(resource, 1f,
                out InventoryReservationToken stagedClaim), Is.True);

            var order = new FreightOrder("retry-order", destination, resource, 1f, 0L);
            Assert.That(order.TryCommit(1f), Is.True);
            var plan = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, stage, 1f),
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, stage, destination, 1f)
            });
            var allocation = new FreightAllocation("retry-allocation", order, source, 1f, plan);
            InventoryComponent carrierInventory = CreateInventory("Carrier", 2f);
            var execution = new TestExecution("retry-execution", 1, carrierInventory,
                tripCapacity: 1f, completesAcrossLoads: false);
            FreightDeliveryJob job = CreateJob(manager, allocation, execution, stagedClaim);
            object authority = GetManagerAuthority(manager);
            Assert.That(job.AdvanceLeg(authority), Is.True);
            job.SetState(authority, FreightJobState.Blocked);
            job.SetRetryAtTick(authority, SimulationManager.Instance != null
                ? SimulationManager.Instance.CurrentTick : 0L);

            var correlation = new FreightExecutionCorrelation(allocation.Id, 1, execution.ExecutionId);
            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.ProviderAtSource), Is.True);
            Assert.That(stageInventory.GetOnHand(resource), Is.Zero);
            Assert.That(carrierInventory.GetOnHand(resource), Is.EqualTo(1f));
            Assert.That(job.HasPickedUp, Is.True);
            Assert.That(job.State, Is.EqualTo(FreightJobState.PickingUp));
        }

        [Test]
        public void PartialFinalTransferCreditsMovedStockAndKeepsMissingRemainderRecoverable()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Source", 5f, out _);
            LogisticsStockComponent destination = CreateStock("Destination", 5f,
                out InventoryComponent destinationInventory);
            InventoryComponent carrierInventory = CreateInventory("Carrier", 5f);
            resource = CreateResource("Short freight");
            Assert.That(carrierInventory.Add(resource, 5f), Is.EqualTo(5f));
            Assert.That(carrierInventory.TryReserveOwned(resource, 5f,
                out InventoryReservationToken cargoClaim), Is.True);
            Assert.That(carrierInventory.Remove(resource, 2f), Is.EqualTo(2f));

            var order = new FreightOrder("short-order", destination, resource, 5f, 0L);
            Assert.That(order.TryCommit(5f), Is.True);
            var plan = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.ShuttleFreight, source, destination, 1f)
            });
            var allocation = new FreightAllocation("short-allocation", order, source, 5f, plan);
            var execution = new TestExecution("short-execution", 0, carrierInventory,
                tripCapacity: 5f, completesAcrossLoads: false);
            FreightDeliveryJob job = CreateJob(manager, allocation, execution, cargoClaim);
            object authority = GetManagerAuthority(manager);
            job.SetHasPickedUp(authority, true);
            job.SetState(authority, FreightJobState.PickingUp);

            var correlation = new FreightExecutionCorrelation(allocation.Id, 0, execution.ExecutionId);
            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.LoadedLegArrived), Is.False);

            Assert.That(destinationInventory.GetOnHand(resource), Is.EqualTo(3f));
            Assert.That(job.DeliveredQuantity, Is.EqualTo(3f));
            Assert.That(job.Quantity, Is.EqualTo(2f));
            Assert.That(order.Delivered, Is.EqualTo(3f));
            Assert.That(order.Committed, Is.EqualTo(2f));
            Assert.That(job.State, Is.EqualTo(FreightJobState.Blocked));
            Assert.That(job.RetryAtTick, Is.EqualTo(long.MaxValue));
            Assert.That(job.LastFailureReason, Is.EqualTo("accepted_cargo_missing"));
            Assert.That(job.Custody.CarriedQuantity, Is.Zero);

            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.LoadedLegArrived), Is.False);
            Assert.That(destinationInventory.GetOnHand(resource), Is.EqualTo(3f));
            Assert.That(order.Delivered, Is.EqualTo(3f));
        }

        [Test]
        public void PartialAcrossLoadFinalTransferCreditsOnlyMovedLoadAndHoldsShortfall()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Walking source", 20f,
                out InventoryComponent sourceInventory);
            LogisticsStockComponent destination = CreateStock("Walking destination", 10f,
                out InventoryComponent destinationInventory);
            InventoryComponent carrierInventory = CreateInventory("Walking carrier", 5f);
            resource = CreateResource("Walking short freight");
            Assert.That(sourceInventory.Add(resource, 15f), Is.EqualTo(15f));
            Assert.That(sourceInventory.TryReserveOwned(resource, 15f,
                out InventoryReservationToken originClaim), Is.True);
            Assert.That(carrierInventory.Add(resource, 5f), Is.EqualTo(5f));
            Assert.That(carrierInventory.TryReserveOwned(resource, 5f,
                out InventoryReservationToken carriedClaim), Is.True);
            Assert.That(carrierInventory.Remove(resource, 2f), Is.EqualTo(2f));

            var order = new FreightOrder("walking-short-order", destination, resource, 20f, 0L);
            Assert.That(order.TryCommit(20f), Is.True);
            var route = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, destination, 1f)
            });
            var execution = new TestExecution("walking-short-execution", 0, carrierInventory,
                tripCapacity: 5f, completesAcrossLoads: true);
            FreightDeliveryJob job = CreateJob(manager,
                new FreightAllocation("walking-short-allocation", order, source, 20f, route),
                execution, carriedClaim);
            object authority = GetManagerAuthority(manager);
            job.SetLegProgress(authority, new FreightLegProgress(0, 20f, originClaim)
            {
                RemainingAtOrigin = 15f,
                InCarrierQuantity = 5f,
                CurrentCarrierReservation = carriedClaim
            });
            job.SetHasPickedUp(authority, true);
            job.SetState(authority, FreightJobState.PickingUp);

            var correlation = new FreightExecutionCorrelation(
                job.Allocation.Id, 0, execution.ExecutionId);
            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.LoadedLegArrived), Is.False);

            Assert.That(destinationInventory.GetOnHand(resource), Is.EqualTo(3f));
            Assert.That(job.DeliveredQuantity, Is.EqualTo(3f));
            Assert.That(job.Quantity, Is.EqualTo(17f));
            Assert.That(order.Delivered, Is.EqualTo(3f));
            Assert.That(order.Committed, Is.EqualTo(17f));
            Assert.That(job.RetryAtTick, Is.EqualTo(long.MaxValue));
            Assert.That(job.Custody.OriginReservedQuantity, Is.EqualTo(15f));
            Assert.That(job.Custody.CarriedQuantity, Is.Zero);
        }

        [Test]
        public void LiveIntermediateRoutePreventsInventoryAndAnchorRebinding()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Source", 2f, out InventoryComponent sourceInventory);
            LogisticsStockComponent stage = CreateStock("Stage", 2f, out InventoryComponent stageInventory);
            LogisticsStockComponent destination = CreateStock("Destination", 2f, out _);
            LogisticsStockComponent unrelated = CreateStock("Unrelated", 2f, out _);
            InventoryComponent replacement = CreateInventory("Replacement", 2f);
            ShuttleTransferEndpoint stageEndpoint = CreateEndpoint("Stage endpoint");
            Assert.That(stageEndpoint.TryConfigure("stage", null, stage.transform,
                stageInventory, stage, out string endpointReason), Is.True, endpointReason);
            resource = CreateResource("Binding freight");
            Assert.That(sourceInventory.Add(resource, 1f), Is.EqualTo(1f));
            Assert.That(sourceInventory.TryReserveOwned(resource, 1f,
                out InventoryReservationToken claim), Is.True);

            var order = new FreightOrder("binding-order", destination, resource, 1f, 0L);
            Assert.That(order.TryCommit(1f), Is.True);
            var plan = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, stage, 1f),
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, stage, destination, 1f)
            });
            var allocation = new FreightAllocation("binding-allocation", order, source, 1f, plan);
            var execution = new TestExecution("binding-execution", 0, null, 1f, true);
            CreateJob(manager, allocation, execution, claim);
            Transform replacementAnchor = CreateAnchor("Replacement anchor");

            Assert.That(stage.TrySetBindings(replacement, replacementAnchor, out string reason), Is.False);
            Assert.That(reason, Does.Contain("accepted freight obligations"));
            Assert.That(stage.Inventory, Is.SameAs(stageInventory));
            Assert.That(stage.FreightAnchor, Is.SameAs(stage.transform));
            Assert.That(unrelated.TrySetBindings(replacement, replacementAnchor, out reason), Is.True);
            Assert.That(unrelated.Inventory, Is.SameAs(replacement));
            Assert.That(stageEndpoint.TryConfigure("stage", null, replacementAnchor,
                stageInventory, stage, out endpointReason), Is.False);
            Assert.That(endpointReason, Does.Contain("accepted freight obligations"));
            Assert.That(stageEndpoint.TransferAnchor, Is.SameAs(stage.transform));
        }

        [Test]
        public void PolicyRetiredHistoryDoesNotBypassReorderThreshold()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent consumer = CreateStock("Consumer", 100f,
                out InventoryComponent inventory);
            resource = CreateResource("Policy freight");
            consumer.ConfigurePolicy(resource, LogisticsStockRole.Consumer,
                targetStock: 100f, targetFull: false, reorderThreshold: 20f,
                emergencyThreshold: 10f, minimumShipmentQuantity: 1f);

            manager.Publish(consumer);
            Assert.That(manager.ActiveOrders.Count, Is.EqualTo(1));
            Assert.That(inventory.Add(resource, 100f), Is.EqualTo(100f));
            manager.Publish(consumer);
            Assert.That(manager.ActiveOrders.Count, Is.Zero);
            Assert.That(manager.RecentOrders.Count, Is.EqualTo(1));

            Assert.That(inventory.Remove(resource, 1f), Is.EqualTo(1f));
            manager.Publish(consumer);
            Assert.That(manager.ActiveOrders.Count, Is.Zero);
            Assert.That(manager.RecentOrders.Count, Is.EqualTo(1));

            Assert.That(inventory.Remove(resource, 79f), Is.EqualTo(79f));
            manager.Publish(consumer);
            Assert.That(inventory.GetOnHand(resource), Is.EqualTo(20f));
            Assert.That(manager.ActiveOrders.Count, Is.EqualTo(1));
            Assert.That(manager.ActiveOrders[0].Requested, Is.EqualTo(80f));
        }

        [Test]
        public void LegalCapacitySizedChildCanBeSmallerThanBatchPreference()
        {
            var demand = new FreightDemandAccounting(40f);
            float largestServiceableChild = FreightShipmentSizing.LimitToCapacity(
                demand.Uncovered, demand.Uncovered, demand.Uncovered, 10f, false);

            Assert.That(largestServiceableChild, Is.EqualTo(10f));
            Assert.That(demand.TryCommit(largestServiceableChild), Is.True);
            Assert.That(demand.Committed, Is.EqualTo(10f));
            Assert.That(demand.Uncovered, Is.EqualTo(30f));
        }

        [Test]
        public void BusyPilotedShuttleCanQuoteFutureCapacityButUnstaffedLargerShuttleCannot()
        {
            GameObject managerObject = new GameObject("Shuttle manager");
            sceneObjects.Add(managerObject);
            ShuttleManager shuttleManager = managerObject.AddComponent<ShuttleManager>();
            ShuttleTransferEndpoint origin = CreateEndpoint("Origin");
            ShuttleTransferEndpoint destination = CreateEndpoint("Destination");
            shuttleManager.RegisterEndpoint(origin);
            shuttleManager.RegisterEndpoint(destination);
            LogisticsStockComponent source = CreateStock("Source", 10f, out _);
            LogisticsStockComponent target = CreateStock("Target", 10f, out _);

            ShuttleServiceComponent busy = CreateShuttle("Busy piloted", 10f, true);
            ShuttleServiceComponent unstaffed = CreateShuttle("Unstaffed large", 20f, false);
            shuttleManager.RegisterShuttle(busy);
            shuttleManager.RegisterShuttle(unstaffed);
            SetCurrentTrip(busy, origin, destination);

            var leg = LogisticsRouteLeg.Shuttle(source, target, origin, destination, 1f);
            Assert.That(busy.CanPlanFutureTrip, Is.True);
            Assert.That(busy.CanStartTripNow, Is.False);
            Assert.That(unstaffed.CanPlanFutureTrip, Is.False);
            Assert.That(shuttleManager.TryQuoteFreightLeg(leg, 10f,
                out ShuttleFreightQuote quote), Is.True);
            Assert.That(quote.TripCapacity, Is.EqualTo(10f));
            Assert.That(shuttleManager.TryQuoteFreightLeg(leg, 20f, out _), Is.False);
        }

        [Test]
        public void EmergencyLaborAuthorizationUsesFinalConsumerAcrossIntermediateDepot()
        {
            LogisticsStockComponent source = CreateStock("Source", 100f, out _);
            LogisticsStockComponent intermediateDepot = CreateStock(
                "Intermediate depot", 100f, out _);
            LogisticsStockComponent requestingConsumer = CreateStock(
                "Requesting consumer", 100f, out _);
            LogisticsStockComponent unrelatedConsumer = CreateStock(
                "Unrelated consumer", 100f, out _);
            resource = CreateResource("Test resource");
            requestingConsumer.ConfigurePolicy(resource, LogisticsStockRole.Consumer,
                targetStock: 20f, targetFull: false, reorderThreshold: 10f,
                emergencyThreshold: 5f, minimumShipmentQuantity: 1f);
            unrelatedConsumer.ConfigurePolicy(resource, LogisticsStockRole.Consumer,
                targetStock: 20f, targetFull: false, reorderThreshold: 10f,
                emergencyThreshold: 5f, minimumShipmentQuantity: 1f);

            WorkplaceComponent consumerWorkplace =
                requestingConsumer.gameObject.AddComponent<WorkplaceComponent>();
            WalkingFreightWorkService emergencyService =
                requestingConsumer.gameObject.AddComponent<WalkingFreightWorkService>();
            emergencyService.ConfigureEmergencyFreight(true, 5f, 0);

            Assert.That(consumerWorkplace.ExecutionMode,
                Is.EqualTo(WorkplaceExecutionMode.FacilityActivity));
            Assert.That(emergencyService.IsPolicyAvailableForLeg(
                source, intermediateDepot, requestingConsumer, resource,
                FreightWorkPurpose.ConsumerEmergencyPickup), Is.True);
            Assert.That(emergencyService.IsPolicyAvailableForLeg(
                source, intermediateDepot, unrelatedConsumer, resource,
                FreightWorkPurpose.ConsumerEmergencyPickup), Is.False);
        }

        [Test]
        public void DisabledProviderIsVisibleAsWaitWithoutDroppingItsObligation()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Source", 2f, out InventoryComponent inventory);
            LogisticsStockComponent destination = CreateStock("Destination", 2f, out _);
            resource = CreateResource("Paused freight");
            Assert.That(inventory.Add(resource, 1f), Is.EqualTo(1f));
            Assert.That(inventory.TryReserveOwned(resource, 1f,
                out InventoryReservationToken claim), Is.True);
            var order = new FreightOrder("paused-order", destination, resource, 1f, 0L);
            Assert.That(order.TryCommit(1f), Is.True);
            var route = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, destination, 1f)
            });
            var execution = new TestExecution("paused-execution", 0, null, 1f,
                completesAcrossLoads: true, providerAvailable: false);
            FreightDeliveryJob job = CreateJob(manager,
                new FreightAllocation("paused-allocation", order, source, 1f, route), execution, claim);

            Assert.That(job.IsWaitingForProvider, Is.True);
            Assert.That(job.ProviderWaitReason, Is.EqualTo("provider_unavailable"));
            Assert.That(job.Quantity, Is.EqualTo(1f));
            Assert.That(order.Committed, Is.EqualTo(1f));
            Assert.That(claim.IsActive, Is.True);
        }

        [Test]
        public void CustodySnapshotSeparatesOriginStagingAndFinalDelivery()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Source", 20f, out InventoryComponent sourceInventory);
            LogisticsStockComponent stage = CreateStock("Stage", 20f, out InventoryComponent stageInventory);
            LogisticsStockComponent destination = CreateStock("Destination", 20f,
                out InventoryComponent destinationInventory);
            InventoryComponent carrierInventory = CreateInventory("Carrier", 5f);
            resource = CreateResource("Custody freight");
            Assert.That(sourceInventory.Add(resource, 20f), Is.EqualTo(20f));
            Assert.That(sourceInventory.TryReserveOwned(resource, 20f,
                out InventoryReservationToken originClaim), Is.True);

            var order = new FreightOrder("custody-order", destination, resource, 20f, 0L);
            Assert.That(order.TryCommit(20f), Is.True);
            var plan = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, stage, 1f),
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, stage, destination, 1f)
            });
            var allocation = new FreightAllocation("custody-allocation", order, source, 20f, plan);
            var execution = new TestExecution("custody-execution", 0, carrierInventory,
                tripCapacity: 5f, completesAcrossLoads: true);
            FreightDeliveryJob job = CreateJob(manager, allocation, execution, originClaim);
            var correlation = new FreightExecutionCorrelation(allocation.Id, 0, execution.ExecutionId);

            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.ProviderAtSource), Is.True);
            FreightCustodySnapshot loaded = job.Custody;
            Assert.That(loaded.OriginReservedQuantity, Is.EqualTo(15f));
            Assert.That(loaded.CarrierInventory, Is.SameAs(carrierInventory));
            Assert.That(loaded.CarriedQuantity, Is.EqualTo(5f));
            Assert.That(loaded.StagedQuantity, Is.Zero);
            Assert.That(job.AcceptedWorkerRemainder, Is.EqualTo(20f));
            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.LoadedLegArrived), Is.True);
            FreightCustodySnapshot staged = job.Custody;
            Assert.That(staged.OriginInventory, Is.SameAs(sourceInventory));
            Assert.That(staged.OriginReservedQuantity, Is.EqualTo(15f));
            Assert.That(staged.StagingInventory, Is.SameAs(stageInventory));
            Assert.That(staged.StagedQuantity, Is.EqualTo(5f));
            Assert.That(staged.CarriedQuantity, Is.Zero);
            Assert.That(job.AcceptedWorkerRemainder, Is.EqualTo(15f));
            Assert.That(job.DeliveredQuantity, Is.Zero);

            // A direct shuttle load is read from its live cargo claim even without walking progress.
            LogisticsStockComponent shuttleSource = CreateStock("Shuttle source", 10f, out InventoryComponent shuttleSourceInventory);
            Assert.That(shuttleSourceInventory.Add(resource, 10f), Is.EqualTo(10f));
            Assert.That(shuttleSourceInventory.TryReserveOwned(resource, 10f,
                out InventoryReservationToken shuttleClaim), Is.True);
            var shuttleOrder = new FreightOrder("shuttle-custody-order", destination, resource, 10f, 0L);
            Assert.That(shuttleOrder.TryCommit(10f), Is.True);
            var shuttlePlan = new LogisticsRoutePlan(shuttleOrder, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.ShuttleFreight,
                    shuttleSource, destination, 1f)
            });
            var shuttleAllocation = new FreightAllocation("shuttle-custody-allocation",
                shuttleOrder, shuttleSource, 10f, shuttlePlan);
            InventoryComponent shuttleCargo = CreateInventory("Shuttle cargo", 10f);
            Assert.That(shuttleSourceInventory.TransferOwnedToAndReserveDestination(
                shuttleClaim, shuttleCargo, 10f, out InventoryReservationToken carriedClaim), Is.EqualTo(10f));
            var shuttleExecution = new TestExecution("shuttle-custody-execution", 0,
                shuttleCargo, 10f, completesAcrossLoads: false);
            FreightDeliveryJob shuttleJob = CreateJob(manager, shuttleAllocation,
                shuttleExecution, carriedClaim);
            object authority = GetManagerAuthority(manager);
            shuttleJob.SetHasPickedUp(authority, true);
            Assert.That(shuttleJob.Custody.CarrierInventory, Is.SameAs(shuttleCargo));
            Assert.That(shuttleJob.Custody.CarriedQuantity, Is.EqualTo(10f));
            Assert.That(shuttleJob.StagedQuantity, Is.Zero);

            Assert.That(destinationInventory.GetOnHand(resource), Is.Zero);
        }

        [Test]
        public void FinalWalkingLoadReadsAsDeliveredAndReducesWorkerRemainder()
        {
            FreightLogisticsManager manager = CreateManager();
            LogisticsStockComponent source = CreateStock("Final source", 20f,
                out InventoryComponent sourceInventory);
            LogisticsStockComponent destination = CreateStock("Final destination", 5f,
                out InventoryComponent destinationInventory);
            InventoryComponent carrierInventory = CreateInventory("Final carrier", 5f);
            resource = CreateResource("Final custody freight");
            Assert.That(sourceInventory.Add(resource, 15f), Is.EqualTo(15f));
            Assert.That(sourceInventory.TryReserveOwned(resource, 15f,
                out InventoryReservationToken originClaim), Is.True);
            Assert.That(carrierInventory.Add(resource, 5f), Is.EqualTo(5f));
            Assert.That(carrierInventory.TryReserveOwned(resource, 5f,
                out InventoryReservationToken carriedClaim), Is.True);

            var order = new FreightOrder("final-custody-order", destination, resource, 20f, 0L);
            Assert.That(order.TryCommit(20f), Is.True);
            var route = new LogisticsRoutePlan(order, new[]
            {
                new LogisticsRouteLeg(LogisticsRouteLegType.WalkingCarrier, source, destination, 1f)
            });
            var execution = new TestExecution("final-custody-execution", 0, carrierInventory,
                tripCapacity: 5f, completesAcrossLoads: true);
            FreightDeliveryJob job = CreateJob(manager,
                new FreightAllocation("final-custody-allocation", order, source, 20f, route),
                execution, carriedClaim);
            object authority = GetManagerAuthority(manager);
            var progress = new FreightLegProgress(0, 20f, originClaim)
            {
                RemainingAtOrigin = 15f,
                InCarrierQuantity = 5f,
                CurrentCarrierReservation = carriedClaim
            };
            job.SetLegProgress(authority, progress);
            job.SetHasPickedUp(authority, true);
            job.SetState(authority, FreightJobState.PickingUp);

            var correlation = new FreightExecutionCorrelation(
                job.Allocation.Id, 0, execution.ExecutionId);
            Assert.That(manager.ReportExecution(job, execution, correlation,
                FreightExecutionReport.LoadedLegArrived), Is.True);

            Assert.That(destinationInventory.GetOnHand(resource), Is.EqualTo(5f));
            Assert.That(job.DeliveredQuantity, Is.EqualTo(5f));
            Assert.That(job.Custody.DeliveredQuantity, Is.EqualTo(5f));
            Assert.That(job.StagedQuantity, Is.Zero);
            Assert.That(job.Custody.OriginReservedQuantity, Is.EqualTo(15f));
            Assert.That(job.AcceptedWorkerRemainder, Is.EqualTo(15f));
            Assert.That(job.Quantity, Is.EqualTo(15f));
        }

        private FreightLogisticsManager CreateManager()
        {
            var gameObject = new GameObject("Corrective test manager");
            sceneObjects.Add(gameObject);
            return gameObject.AddComponent<FreightLogisticsManager>();
        }

        private LogisticsStockComponent CreateStock(string objectName, float capacity,
            out InventoryComponent inventory)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            inventory = gameObject.AddComponent<InventoryComponent>();
            Assert.That(inventory.SetCapacity(capacity), Is.True);
            LogisticsStockComponent stock = gameObject.AddComponent<LogisticsStockComponent>();
            Assert.That(stock.TrySetBindings(inventory, gameObject.transform, out string reason), Is.True, reason);
            return stock;
        }

        private InventoryComponent CreateInventory(string objectName, float capacity)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            var inventory = gameObject.AddComponent<InventoryComponent>();
            Assert.That(inventory.SetCapacity(capacity), Is.True);
            return inventory;
        }

        private ShuttleTransferEndpoint CreateEndpoint(string objectName)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            return gameObject.AddComponent<ShuttleTransferEndpoint>();
        }

        private Transform CreateAnchor(string objectName)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            return gameObject.transform;
        }

        private ShuttleServiceComponent CreateShuttle(string objectName,
            float cargoCapacity, bool piloted)
        {
            var gameObject = new GameObject(objectName);
            sceneObjects.Add(gameObject);
            ShuttleVoyageComponent voyage = gameObject.AddComponent<ShuttleVoyageComponent>();
            InventoryComponent cargo = gameObject.AddComponent<InventoryComponent>();
            Assert.That(cargo.SetCapacity(cargoCapacity), Is.True);
            ShuttlePilotWorkService pilotService = piloted
                ? gameObject.AddComponent<ShuttlePilotWorkService>() : null;
            var shuttle = gameObject.AddComponent<ShuttleServiceComponent>();
            shuttle.Configure(objectName, null, voyage, cargo, null, 4, 1, pilotService);
            if (piloted)
            {
                GameObject workerObject = new GameObject(objectName + " pilot");
                sceneObjects.Add(workerObject);
                ColonistIdentity worker = workerObject.AddComponent<ColonistIdentity>();
                var workplaceObject = new GameObject(objectName + " workplace");
                sceneObjects.Add(workplaceObject);
                WorkplaceComponent workplace = workplaceObject.AddComponent<WorkplaceComponent>();
                var lease = new WorkExecutionLease(worker, workplace, new PassiveWorkOwner());
                typeof(ShuttlePilotWorkService).GetField("lease",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pilotService, lease);
                pilotService.BindVehicle(shuttle);
                AddBoardedPilot(shuttle, worker);
            }
            return shuttle;
        }

        private static void AddBoardedPilot(ShuttleServiceComponent shuttle,
            ColonistIdentity pilot)
        {
            System.Type stateType = typeof(ShuttleServiceComponent).GetNestedType(
                "BoardedActorState", BindingFlags.NonPublic);
            object state = System.Activator.CreateInstance(stateType, true);
            stateType.GetField("Actor").SetValue(state, pilot);
            stateType.GetField("IsPilot").SetValue(state, true);
            var actors = (IList)typeof(ShuttleServiceComponent).GetField("boardedActors",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shuttle);
            actors.Add(state);
        }

        private static void SetCurrentTrip(ShuttleServiceComponent shuttle,
            ShuttleTransferEndpoint origin, ShuttleTransferEndpoint destination)
        {
            typeof(ShuttleVoyageComponent).GetField("phase",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(
                    shuttle.Voyage, ShuttleVoyagePhase.CruiseAccelerating);
            var trip = new ShuttleTrip("busy-trip", shuttle, origin, destination, 0L)
            {
                State = ShuttleTripState.InTransit
            };
            typeof(ShuttleServiceComponent).GetField("currentTrip",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(shuttle, trip);
        }

        private ResourceDefinition CreateResource(string resourceName)
        {
            resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            resource.name = resourceName;
            return resource;
        }

        private static object GetManagerAuthority(FreightLogisticsManager manager) =>
            typeof(FreightLogisticsManager).GetField("jobMutationAuthority",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

        private static FreightDeliveryJob CreateJob(FreightLogisticsManager manager,
            FreightAllocation allocation, TestExecution execution, InventoryReservationToken reservation)
        {
            var job = new FreightDeliveryJob(allocation, execution, reservation,
                GetManagerAuthority(manager));
            ((List<FreightDeliveryJob>)manager.Jobs).Add(job);
            return job;
        }

        private sealed class TestExecution : IFreightLegExecution
        {
            public TestExecution(string executionId, int legIndex, InventoryComponent cargoInventory,
                float tripCapacity, bool completesAcrossLoads, bool providerAvailable = true)
            {
                ExecutionId = executionId;
                LegIndex = legIndex;
                CargoInventory = cargoInventory;
                TripCapacity = tripCapacity;
                CompletesAcrossLoads = completesAcrossLoads;
                ProviderIsAvailable = providerAvailable;
            }

            public string ExecutionId { get; }
            public int LegIndex { get; }
            public bool IsActive { get; private set; } = true;
            public bool ProviderAvailable => ProviderIsAvailable;
            public bool ExecutorAvailable => true;
            public bool IsEmergency => false;
            public bool RequiresPersonnelRouteForPickup => false;
            public bool RequiresPersonnelRouteForLoadedArrival => false;
            public bool CompletesAcceptedQuantityAcrossLoads => CompletesAcrossLoads;
            public bool DefersReleaseUntilAcceptedWorkCompletes => CompletesAcrossLoads;
            public bool HasPendingWorkerRelease => false;
            public float TripCapacity { get; }
            public float PositioningDistance => 0f;
            public InventoryComponent CargoInventory { get; }
            public Object ProviderContext => null;
            private bool CompletesAcrossLoads { get; }
            private bool ProviderIsAvailable { get; }

            public void Complete(FreightDeliveryJob job) => IsActive = false;
        }

        private sealed class PassiveWorkOwner : IWorkExecutionOwner
        {
            public WorkReleaseDisposition RequestRelease(WorkExecutionLease lease,
                WorkReleaseReason reason) => WorkReleaseDisposition.ReleasedNow;
        }
    }
}
