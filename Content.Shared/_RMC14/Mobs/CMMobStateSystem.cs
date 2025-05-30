﻿using Content.Shared._RMC14.Sprite;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Mobs;

public sealed class CMMobStateSystem : EntitySystem
{
    [Dependency] private readonly IConsoleHost _host = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateActionsComponent, CMGhostActionEvent>(OnMobStateActionsGhost);

        SubscribeLocalEvent<RMCMobStateDrawDepthComponent, GetDrawDepthEvent>(OnMobStateDrawDepth);
        SubscribeLocalEvent<RMCMobStateDrawDepthComponent, MobStateChangedEvent>(OnMobStateChanged);

        Subs.BuiEvents<MobStateActionsComponent>(CMMobStateActionsUI.Key,
            subs =>
            {
                subs.Event<CMGhostActionBuiMsg>(OnGhostActionBuiMsg);
            });
    }

    private void OnMobStateActionsGhost(Entity<MobStateActionsComponent> ent, ref CMGhostActionEvent args)
    {
        if (args.Handled)
            return;

        if (_mobState.IsDead(ent))
        {
            if (_net.IsServer && TryComp(ent, out ActorComponent? actor))
                _host.ExecuteCommand(actor.PlayerSession, "ghost");

            return;
        }

        args.Handled = true;
        _ui.OpenUi(ent.Owner, CMMobStateActionsUI.Key, ent);
    }

    private void OnMobStateDrawDepth(Entity<RMCMobStateDrawDepthComponent> ent, ref GetDrawDepthEvent args)
    {
        if (!TryComp(ent, out MobStateComponent? mobState))
            return;

        if (args.DrawDepth == ent.Comp.Default &&
            ent.Comp.DrawDepths.TryGetValue(mobState.CurrentState, out var depth))
        {
            args.DrawDepth = depth;
        }
    }

    private void OnMobStateChanged(Entity<RMCMobStateDrawDepthComponent> ent, ref MobStateChangedEvent args)
    {
        _rmcSprite.UpdateDrawDepth(ent);
    }

    private void OnGhostActionBuiMsg(Entity<MobStateActionsComponent> ent, ref CMGhostActionBuiMsg args)
    {
        if (!_mobState.IsIncapacitated(ent))
            return;

        if (!TryGetEntity(args.Entity, out var entity) ||
            entity != args.Actor)
        {
            return;
        }

        _ui.CloseUi(ent.Owner, CMMobStateActionsUI.Key);

        if (_net.IsServer && TryComp(args.Actor, out ActorComponent? actor))
            _host.ExecuteCommand(actor.PlayerSession, "ghost");
    }
}
