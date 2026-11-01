using System;
using System.Collections.Generic;
using Cave.Gambit;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerEquipmentId
    {
        None = 0,
        OblivionDisk = 1,
        Distraction = 2,
        TacticalProbe = 100,
        TacticalGuard = 101,
        TacticalEstablishLink = 102,
        TacticalSurge = 103,
        TacticalSever = 104,
        TacticalReposition = 105
    }

    public enum PlayerEquipmentCategory
    {
        Equipment,
        TacticalCommand
    }

    public enum EquipmentActivationKind
    {
        Primary,
        Secondary
    }

    public enum EquipmentActivationDecision
    {
        None,
        Primary,
        Secondary
    }

    public static class EquipmentInputPolicy
    {
        public static EquipmentActivationDecision Resolve(
            bool supportsSecondary,
            bool thresholdReached,
            bool released,
            bool secondaryAlreadyAttempted)
        {
            if (supportsSecondary && thresholdReached && !secondaryAlreadyAttempted)
            {
                return EquipmentActivationDecision.Secondary;
            }

            if (released && !secondaryAlreadyAttempted)
            {
                return EquipmentActivationDecision.Primary;
            }

            return EquipmentActivationDecision.None;
        }

        public static PlayerEquipmentId ResolveSelection(
            PlayerEquipmentId previous,
            PlayerEquipmentId highlighted,
            bool highlightedAvailable,
            bool canceled)
        {
            return !canceled
                && highlighted != PlayerEquipmentId.None
                && highlightedAvailable
                ? highlighted
                : previous;
        }
    }

    public readonly struct EquipmentActivationResult
    {
        public EquipmentActivationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }

    public readonly struct PlayerEquipmentPresentation
    {
        public PlayerEquipmentPresentation(
            PlayerEquipmentId id,
            PlayerEquipmentCategory category,
            string name,
            string symbol,
            string description,
            bool available,
            bool hasSecondary,
            string status)
        {
            Id = id;
            Category = category;
            Name = name;
            Symbol = symbol;
            Description = description;
            Available = available;
            HasSecondary = hasSecondary;
            Status = status;
        }

        public PlayerEquipmentId Id { get; }
        public PlayerEquipmentCategory Category { get; }
        public string Name { get; }
        public string Symbol { get; }
        public string Description { get; }
        public bool Available { get; }
        public bool HasSecondary { get; }
        public string Status { get; }
    }

    internal interface IPlayerEquipmentHandler
    {
        PlayerEquipmentId Id { get; }
        PlayerEquipmentCategory Category { get; }
        string Name { get; }
        string Symbol { get; }
        string Description { get; }
        bool IsAvailable { get; }
        bool HasSecondary { get; }
        float HoldThreshold { get; }
        string Status { get; }
        EquipmentActivationResult Activate(EquipmentActivationKind kind);
    }

    [DisallowMultipleComponent]
    public sealed class PlayerEquipmentController : MonoBehaviour
    {
        private const string SelectedEquipmentKey = "Cave.Equipment.Selected.v1";
        private const float DefaultHoldThreshold = 0.45f;
        private const float PointerDeadZonePixels = 60f;

        private readonly List<IPlayerEquipmentHandler> handlers =
            new List<IPlayerEquipmentHandler>(8);
        private PlayerLandmineInventory inventory;
        private PlayerCurseController curse;
        private PlayerHealth health;
        private SovereignGambitTacticalCommandController tactical;
        private PlayerEquipmentId ordinarySelection = PlayerEquipmentId.OblivionDisk;
        private bool wheelOpen;
        private PlayerEquipmentId selected;
        private PlayerEquipmentId highlighted;
        private bool trackingActivation;
        private bool holdAttempted;
        private float activationStartedAt;
        private int lastStateSignature;

        public static PlayerEquipmentController Active { get; private set; }
        public static bool AnyWheelOpen => Active != null && Active.wheelOpen;

        public event Action SelectionChanged;
        public event Action StateChanged;
        public event Action<bool> WheelVisibilityChanged;
        public event Action HighlightChanged;
        public event Action<EquipmentActivationResult> ActivationResolved;

        public PlayerEquipmentId SelectedEquipment => selected;
        public PlayerEquipmentId HighlightedEquipment => highlighted;
        public bool IsWheelOpen => wheelOpen;
        public int EquipmentCount => handlers.Count;
        public const int EquipmentSlotCapacity = 8;

        public static PlayerEquipmentController EnsureOn(GameObject player)
        {
            if (player == null)
            {
                return null;
            }

            if (player.GetComponent<PlayerLandmineInventory>() == null)
            {
                player.AddComponent<PlayerLandmineInventory>();
            }

            if (player.GetComponent<PlayerCurseController>() == null)
            {
                player.AddComponent<PlayerCurseController>();
            }

            if (player.GetComponent<SovereignGambitTacticalCommandController>() == null)
            {
                player.AddComponent<SovereignGambitTacticalCommandController>();
            }

            PlayerEquipmentController controller = player.GetComponent<PlayerEquipmentController>();
            return controller != null ? controller : player.AddComponent<PlayerEquipmentController>();
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerLandmineInventory>();
            curse = GetComponent<PlayerCurseController>();
            health = GetComponent<PlayerHealth>();
            tactical = GetComponent<SovereignGambitTacticalCommandController>();
            RebuildHandlers();
            selected = LoadSelectedEquipment();
            ordinarySelection = selected;
            highlighted = PlayerEquipmentId.None;
            lastStateSignature = ComputeStateSignature();
        }

        private void OnEnable()
        {
            Active = this;
            BindAuthorities(true);
        }

        private void OnDisable()
        {
            BindAuthorities(false);
            if (Active == this)
            {
                Active = null;
            }

            wheelOpen = false;
            trackingActivation = false;
        }

        private void Update()
        {
            RefreshAuthorityState();

            if (wheelOpen)
            {
                UpdateWheel();
                return;
            }

            if (GameInput.SelectEquipmentPressed)
            {
                OpenWheel();
                return;
            }

            UpdateActivationInput();
        }

        public PlayerEquipmentPresentation GetPresentation(PlayerEquipmentId id)
        {
            IPlayerEquipmentHandler handler = FindHandler(id);
            return handler == null
                ? new PlayerEquipmentPresentation(
                    PlayerEquipmentId.None,
                    PlayerEquipmentCategory.Equipment,
                    "NO EQUIPMENT",
                    "—",
                    "No supported equipment is available.",
                    false,
                    false,
                    "UNAVAILABLE")
                : new PlayerEquipmentPresentation(
                    handler.Id,
                    handler.Category,
                    handler.Name,
                    handler.Symbol,
                    handler.Description,
                    handler.IsAvailable,
                    handler.HasSecondary,
                    handler.Status);
        }

        public PlayerEquipmentPresentation GetPresentationAt(int index)
        {
            return index >= 0 && index < handlers.Count
                ? GetPresentation(handlers[index].Id)
                : GetPresentation(PlayerEquipmentId.None);
        }

        public bool TryEquip(PlayerEquipmentId id)
        {
            IPlayerEquipmentHandler handler = FindHandler(id);
            if (handler == null || !handler.IsAvailable)
            {
                return false;
            }

            if (selected == id)
            {
                return true;
            }

            selected = id;
            TacticalCommandHandler tacticalHandler = handler as TacticalCommandHandler;
            if (tacticalHandler != null)
            {
                tactical?.TrySelectCommand(tacticalHandler.Command);
            }
            else
            {
                ordinarySelection = selected;
                PlayerPrefs.SetInt(SelectedEquipmentKey, (int)selected);
                PlayerPrefs.Save();
            }
            SelectionChanged?.Invoke();
            StateChanged?.Invoke();
            return true;
        }

        public EquipmentActivationResult TryActivate(EquipmentActivationKind kind)
        {
            IPlayerEquipmentHandler handler = FindHandler(selected);
            EquipmentActivationResult result;
            if (handler == null || !handler.IsAvailable)
            {
                result = new EquipmentActivationResult(false, "Equipment unavailable.");
            }
            else if (kind == EquipmentActivationKind.Secondary && !handler.HasSecondary)
            {
                result = new EquipmentActivationResult(false, "No secondary action.");
            }
            else
            {
                result = handler.Activate(kind);
            }

            RefreshAuthorityState(true);
            ActivationResolved?.Invoke(result);
            return result;
        }

        public void OpenWheel()
        {
            if (wheelOpen || !GameInput.GameplayInputEnabled)
            {
                return;
            }

            wheelOpen = true;
            highlighted = PlayerEquipmentId.None;
            trackingActivation = false;
            GameInput.SetGameplayInputEnabled(false);
            WheelVisibilityChanged?.Invoke(true);
            HighlightChanged?.Invoke();
        }

        public void CancelWheel()
        {
            CloseWheel(false);
        }

        public static void CancelAnyOpenWheel()
        {
            Active?.CancelWheel();
        }

        private void UpdateWheel()
        {
            if (GameInput.MenuCancelPressed)
            {
                GameInput.ConsumeMenuInputForCurrentFrame();
                CloseWheel(false);
                return;
            }

            PlayerEquipmentId next = ResolveDirectionalHighlight();
            if (next != highlighted)
            {
                highlighted = next;
                HighlightChanged?.Invoke();
            }

            if (GameInput.SelectEquipmentReleased || !GameInput.SelectEquipmentHeld)
            {
                CloseWheel(true);
            }
        }

        private void CloseWheel(bool confirm)
        {
            if (!wheelOpen)
            {
                return;
            }

            IPlayerEquipmentHandler highlightedHandler = FindHandler(highlighted);
            PlayerEquipmentId resolved = EquipmentInputPolicy.ResolveSelection(
                selected,
                highlighted,
                highlightedHandler != null && highlightedHandler.IsAvailable,
                !confirm);
            if (resolved != selected)
            {
                TryEquip(resolved);
            }

            wheelOpen = false;
            highlighted = PlayerEquipmentId.None;
            WheelVisibilityChanged?.Invoke(false);
            HighlightChanged?.Invoke();
            GameInput.EnableGameplayAfterInputRelease();
        }

        private void UpdateActivationInput()
        {
            if (GameInput.ActivateEquipmentPressed)
            {
                trackingActivation = true;
                holdAttempted = false;
                activationStartedAt = Time.unscaledTime;
            }

            if (!trackingActivation)
            {
                return;
            }

            IPlayerEquipmentHandler handler = FindHandler(selected);
            float threshold = handler != null
                ? Mathf.Max(0.05f, handler.HoldThreshold)
                : DefaultHoldThreshold;
            bool thresholdReached = handler != null
                && GameInput.ActivateEquipmentHeld
                && Time.unscaledTime >= activationStartedAt + threshold;
            EquipmentActivationDecision decision = EquipmentInputPolicy.Resolve(
                handler != null && handler.HasSecondary,
                thresholdReached,
                GameInput.ActivateEquipmentReleased,
                holdAttempted);
            if (decision == EquipmentActivationDecision.Secondary)
            {
                holdAttempted = true;
                TryActivate(EquipmentActivationKind.Secondary);
            }
            else if (decision == EquipmentActivationDecision.Primary)
            {
                TryActivate(EquipmentActivationKind.Primary);
                trackingActivation = false;
            }
            else if (GameInput.ActivateEquipmentReleased)
            {
                trackingActivation = false;
            }
            else if (!GameInput.GameplayInputEnabled)
            {
                trackingActivation = false;
            }
        }

        private PlayerEquipmentId ResolveDirectionalHighlight()
        {
            Vector2 direction = GameInput.ReadRawEquipmentSelectionDirection();
            Vector2 pointerOffset = (Vector2)UnityEngine.Input.mousePosition
                - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (direction.sqrMagnitude < 0.25f
                && pointerOffset.sqrMagnitude >= PointerDeadZonePixels * PointerDeadZonePixels)
            {
                direction = pointerOffset.normalized;
            }

            if (direction.sqrMagnitude < 0.25f || handlers.Count == 0)
            {
                return PlayerEquipmentId.None;
            }

            float angle = Mathf.Atan2(direction.y, direction.x);
            if (angle < 0f)
            {
                angle += Mathf.PI * 2f;
            }

            float slice = Mathf.PI * 2f / handlers.Count;
            int index = Mathf.FloorToInt((angle + slice * 0.5f) / slice) % handlers.Count;
            IPlayerEquipmentHandler candidate = handlers[index];
            return candidate.IsAvailable ? candidate.Id : PlayerEquipmentId.None;
        }

        private void RebuildHandlers()
        {
            handlers.Clear();
            if (inventory != null)
            {
                handlers.Add(new OblivionDiskHandler(inventory));
            }

            if (curse != null)
            {
                handlers.Add(new DistractionHandler(curse));
            }

            if (tactical != null && tactical.IsMatchActive)
            {
                handlers.Add(new TacticalCommandHandler(tactical,SovereignTacticalCommandId.Probe));
                handlers.Add(new TacticalCommandHandler(tactical,SovereignTacticalCommandId.Guard));
                handlers.Add(new TacticalCommandHandler(tactical,SovereignTacticalCommandId.EstablishLink));
                handlers.Add(new TacticalCommandHandler(tactical,SovereignTacticalCommandId.Surge));
                handlers.Add(new TacticalCommandHandler(tactical,SovereignTacticalCommandId.Sever));
                handlers.Add(new TacticalCommandHandler(tactical,SovereignTacticalCommandId.Reposition));
            }
        }

        private PlayerEquipmentId LoadSelectedEquipment()
        {
            PlayerEquipmentId saved = (PlayerEquipmentId)PlayerPrefs.GetInt(
                SelectedEquipmentKey,
                (int)PlayerEquipmentId.OblivionDisk);
            IPlayerEquipmentHandler savedHandler = FindHandler(saved);
            if (savedHandler != null && savedHandler.IsAvailable)
            {
                return saved;
            }

            for (int index = 0; index < handlers.Count; index++)
            {
                if (handlers[index].IsAvailable)
                {
                    return handlers[index].Id;
                }
            }

            return savedHandler != null ? saved : PlayerEquipmentId.OblivionDisk;
        }

        private IPlayerEquipmentHandler FindHandler(PlayerEquipmentId id)
        {
            for (int index = 0; index < handlers.Count; index++)
            {
                if (handlers[index].Id == id)
                {
                    return handlers[index];
                }
            }

            return null;
        }

        private void BindAuthorities(bool bind)
        {
            if (inventory != null)
            {
                if (bind)
                {
                    inventory.DiskChargeStateChanged += HandleAuthorityChanged;
                }
                else
                {
                    inventory.DiskChargeStateChanged -= HandleAuthorityChanged;
                }
            }

            if (curse != null)
            {
                if (bind)
                {
                    curse.CurseStateChanged += HandleAuthorityChanged;
                }
                else
                {
                    curse.CurseStateChanged -= HandleAuthorityChanged;
                }
            }

            if (health != null)
            {
                if (bind)
                {
                    health.Died += HandlePlayerUnavailable;
                    health.Respawned += HandlePlayerUnavailable;
                }
                else
                {
                    health.Died -= HandlePlayerUnavailable;
                    health.Respawned -= HandlePlayerUnavailable;
                }
            }

            if (tactical != null)
            {
                if (bind)
                {
                    tactical.MatchAvailabilityChanged += HandleTacticalAvailabilityChanged;
                    tactical.StateChanged += HandleTacticalStateChanged;
                }
                else
                {
                    tactical.MatchAvailabilityChanged -= HandleTacticalAvailabilityChanged;
                    tactical.StateChanged -= HandleTacticalStateChanged;
                }
            }
        }

        private void HandleTacticalAvailabilityChanged(bool available)
        {
            RebuildHandlers();
            if (!available && IsTacticalEquipment(selected))
            {
                selected = FindHandler(ordinarySelection) != null
                    ? ordinarySelection
                    : PlayerEquipmentId.OblivionDisk;
                SelectionChanged?.Invoke();
            }
            StateChanged?.Invoke();
        }

        private void HandleTacticalStateChanged()
        {
            if (tactical != null && tactical.IsMatchActive && IsTacticalEquipment(selected))
            {
                PlayerEquipmentId next = ToEquipmentId(tactical.SelectedCommand);
                if (next != selected && FindHandler(next)?.IsAvailable == true)
                {
                    selected = next;
                    SelectionChanged?.Invoke();
                }
            }
            RefreshAuthorityState(true);
        }

        private static bool IsTacticalEquipment(PlayerEquipmentId id)
        {
            return (int)id >= (int)PlayerEquipmentId.TacticalProbe;
        }

        private static PlayerEquipmentId ToEquipmentId(SovereignTacticalCommandId id)
        {
            switch (id)
            {
                case SovereignTacticalCommandId.Probe:return PlayerEquipmentId.TacticalProbe;
                case SovereignTacticalCommandId.Guard:return PlayerEquipmentId.TacticalGuard;
                case SovereignTacticalCommandId.EstablishLink:return PlayerEquipmentId.TacticalEstablishLink;
                case SovereignTacticalCommandId.Surge:return PlayerEquipmentId.TacticalSurge;
                case SovereignTacticalCommandId.Sever:return PlayerEquipmentId.TacticalSever;
                case SovereignTacticalCommandId.Reposition:return PlayerEquipmentId.TacticalReposition;
                default:return PlayerEquipmentId.None;
            }
        }

        private void HandleAuthorityChanged()
        {
            RefreshAuthorityState(true);
        }

        private void HandlePlayerUnavailable()
        {
            if (wheelOpen)
            {
                CloseWheel(false);
            }

            trackingActivation = false;
        }

        private void RefreshAuthorityState(bool force = false)
        {
            int signature = ComputeStateSignature();
            if (!force && signature == lastStateSignature)
            {
                return;
            }

            lastStateSignature = signature;
            StateChanged?.Invoke();
        }

        private int ComputeStateSignature()
        {
            unchecked
            {
                int signature = inventory != null
                    ? inventory.StoredDiskCharges * 397 ^ inventory.DiskCapacity
                    : 0;
                if (curse != null)
                {
                    signature = signature * 397 ^ (curse.CurseOfInsanityActive ? 1 : 0);
                    signature = signature * 397
                        ^ Mathf.CeilToInt(curse.DistractionCooldownRemaining * 10f);
                }

                return signature;
            }
        }

        private sealed class TacticalCommandHandler : IPlayerEquipmentHandler
        {
            private readonly SovereignGambitTacticalCommandController controller;
            public TacticalCommandHandler(SovereignGambitTacticalCommandController value,SovereignTacticalCommandId command)
            { controller=value;Command=command; }
            public SovereignTacticalCommandId Command { get; }
            private TacticalCommandDefinition Definition => controller.GetDefinition(Command);
            public PlayerEquipmentId Id => ToEquipmentId(Command);
            public PlayerEquipmentCategory Category => PlayerEquipmentCategory.TacticalCommand;
            public string Name => Definition.Name;
            public string Symbol => Definition.Symbol;
            public string Description => Definition.Description;
            public bool IsAvailable => controller.IsCommandAvailable(Command);
            public bool HasSecondary => false;
            public float HoldThreshold => DefaultHoldThreshold;
            public string Status => controller.IsCommandQueued(Command)
                ? "QUEUED"
                : Definition.EnergyCost.ToString("0.0")+" E";
            public EquipmentActivationResult Activate(EquipmentActivationKind kind)
            {
                TacticalCommandExecutionResult result=kind==EquipmentActivationKind.Primary
                    ? controller.TryExecute(Command)
                    : new TacticalCommandExecutionResult(TacticalCommandExecutionStatus.TargetInvalid,"No secondary tactical action.");
                return new EquipmentActivationResult(result.Succeeded,result.Message);
            }
        }

        private sealed class OblivionDiskHandler : IPlayerEquipmentHandler
        {
            private readonly PlayerLandmineInventory inventory;

            public OblivionDiskHandler(PlayerLandmineInventory inventory)
            {
                this.inventory = inventory;
            }

            public PlayerEquipmentId Id => PlayerEquipmentId.OblivionDisk;
            public PlayerEquipmentCategory Category => PlayerEquipmentCategory.Equipment;
            public string Name => "OBLIVION DISK";
            public string Symbol => "◎";
            public string Description => "Tap to place. Hold to sacrifice the weakest active disk.";
            public bool IsAvailable => inventory != null && inventory.IsDiskUnlocked;
            public bool HasSecondary => true;
            public float HoldThreshold => inventory != null
                ? inventory.SacrificeHoldThreshold
                : DefaultHoldThreshold;
            public string Status
            {
                get
                {
                    if (!IsAvailable)
                    {
                        return "LOCKED";
                    }

                    string pips = string.Empty;
                    for (int index = 0; index < inventory.DiskCapacity; index++)
                    {
                        pips += index < inventory.StoredDiskCharges ? "◆" : "◇";
                    }

                    return pips;
                }
            }

            public EquipmentActivationResult Activate(EquipmentActivationKind kind)
            {
                bool succeeded = kind == EquipmentActivationKind.Secondary
                    ? inventory.TrySacrificeWeakestMine()
                    : inventory.TryPlaceLandmine();
                return new EquipmentActivationResult(
                    succeeded,
                    succeeded
                        ? kind == EquipmentActivationKind.Secondary
                            ? "Disk sacrificed."
                            : "Disk placed."
                        : kind == EquipmentActivationKind.Secondary
                            ? "No eligible disk to sacrifice."
                            : "Disk placement unavailable.");
            }
        }

        private sealed class DistractionHandler : IPlayerEquipmentHandler
        {
            private readonly PlayerCurseController curse;

            public DistractionHandler(PlayerCurseController curse)
            {
                this.curse = curse;
            }

            public PlayerEquipmentId Id => PlayerEquipmentId.Distraction;
            public PlayerEquipmentCategory Category => PlayerEquipmentCategory.Equipment;
            public string Name => "DISTRACTION";
            public string Symbol => "✦";
            public string Description => "Deploy an Insanity decoy toward the current aim direction.";
            public bool IsAvailable => curse != null && curse.CurseOfInsanityActive;
            public bool HasSecondary => false;
            public float HoldThreshold => DefaultHoldThreshold;
            public string Status => !IsAvailable
                ? "CURSE REQUIRED"
                : curse.DistractionCooldownRemaining > 0f
                    ? curse.DistractionCooldownRemaining.ToString("0.0") + "s"
                    : "READY";

            public EquipmentActivationResult Activate(EquipmentActivationKind kind)
            {
                bool succeeded = kind == EquipmentActivationKind.Primary
                    && curse.TryDeployDistraction();
                return new EquipmentActivationResult(
                    succeeded,
                    succeeded ? "Distraction deployed." : "Distraction unavailable.");
            }
        }
    }
}
