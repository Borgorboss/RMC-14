using Content.Shared._RMC14.Water;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Slippery;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Movement.Systems;

public sealed class SpeedModifierContactsSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifierSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly RMCWaterSystem _rmcWater = default!;

    // TODO full-game-save
    // Either these need to be processed before a map is saved, or slowed/slowing entities need to update on init.
    private HashSet<EntityUid> _toUpdate = new();
    private HashSet<EntityUid> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpeedModifierContactsComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<SpeedModifierContactsComponent, EndCollideEvent>(OnEntityExit);
        SubscribeLocalEvent<SpeedModifiedByContactComponent, RefreshMovementSpeedModifiersEvent>(MovementSpeedCheck);
        SubscribeLocalEvent<SpeedModifierContactsComponent, ComponentShutdown>(OnShutdown);

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _toRemove.Clear();

        foreach (var ent in _toUpdate)
        {
            _speedModifierSystem.RefreshMovementSpeedModifiers(ent);
        }

        foreach (var ent in _toRemove)
        {
            RemComp<SpeedModifiedByContactComponent>(ent);
        }

        _toUpdate.Clear();
    }

    public void ChangeModifiers(EntityUid uid, float speed, SpeedModifierContactsComponent? component = null)
    {
        ChangeModifiers(uid, speed, speed, component);
    }

    public void ChangeModifiers(EntityUid uid, float walkSpeed, float sprintSpeed, SpeedModifierContactsComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }
        component.WalkSpeedModifier = walkSpeed;
        component.SprintSpeedModifier = sprintSpeed;
        Dirty(uid, component);
        _toUpdate.UnionWith(_physics.GetContactingEntities(uid));
    }

    private void OnShutdown(EntityUid uid, SpeedModifierContactsComponent component, ComponentShutdown args)
    {
        if (!TryComp(uid, out PhysicsComponent? phys))
            return;

        // Note that the entity may not be getting deleted here. E.g., glue puddles.
        _toUpdate.UnionWith(_physics.GetContactingEntities(uid, phys));
    }

    private void MovementSpeedCheck(EntityUid uid, SpeedModifiedByContactComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!EntityManager.TryGetComponent<PhysicsComponent>(uid, out var physicsComponent))
            return;

        var walkSpeed = 0.0f;
        var sprintSpeed = 0.0f;

        bool remove = true;
        var entries = 0;
        foreach (var ent in _physics.GetContactingEntities(uid, physicsComponent))
        {
            bool speedModified = false;

            if (TryComp<SpeedModifierContactsComponent>(ent, out var slowContactsComponent))
            {
                if (_whitelistSystem.IsWhitelistPass(slowContactsComponent.IgnoreWhitelist, uid))
                    continue;

                walkSpeed += slowContactsComponent.WalkSpeedModifier;
                sprintSpeed += slowContactsComponent.SprintSpeedModifier;
                speedModified = true;
            }

            // SpeedModifierContactsComponent takes priority over SlowedOverSlipperyComponent, effectively overriding the slippery slow.
            if (TryComp<SlipperyComponent>(ent, out var slipperyComponent) && speedModified == false)
            {
                var evSlippery = new GetSlowedOverSlipperyModifierEvent();
                RaiseLocalEvent(uid, ref evSlippery);

                if (evSlippery.SlowdownModifier != 1)
                {
                    walkSpeed += evSlippery.SlowdownModifier;
                    sprintSpeed += evSlippery.SlowdownModifier;
                    speedModified = true;
                }
            }

            if (speedModified)
            {
                remove = false;
                entries++;
            }
        }

        if (entries > 0)
        {
            walkSpeed /= entries;
            sprintSpeed /= entries;

            var evMax = new GetSpeedModifierContactCapEvent();
            RaiseLocalEvent(uid, ref evMax);

            walkSpeed = MathF.Max(walkSpeed, evMax.MaxWalkSlowdown);
            sprintSpeed = MathF.Max(sprintSpeed, evMax.MaxSprintSlowdown);

            args.ModifySpeed(walkSpeed, sprintSpeed);
        }

        // no longer colliding with anything
        if (remove)
            _toRemove.Add(uid);
    }

    private void OnEntityExit(EntityUid uid, SpeedModifierContactsComponent component, ref EndCollideEvent args)
    {
        var otherUid = args.OtherEntity;
        _toUpdate.Add(otherUid);
    }

    private void OnEntityEnter(EntityUid uid, SpeedModifierContactsComponent component, ref StartCollideEvent args)
    {
        if (!_rmcWater.CanCollide(uid, args.OtherEntity))
            return;

        AddModifiedEntity(args.OtherEntity);
    }

    /// <summary>
    /// Add an entity to be checked for speed modification from contact with another entity.
    /// </summary>
    /// <param name="uid">The entity to be added.</param>
    public void AddModifiedEntity(EntityUid uid)
    {
        if (!HasComp<MovementSpeedModifierComponent>(uid))
            return;

        EnsureComp<SpeedModifiedByContactComponent>(uid);
        _toUpdate.Add(uid);
    }
}
