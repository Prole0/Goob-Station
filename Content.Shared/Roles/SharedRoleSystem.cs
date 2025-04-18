// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2023 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 AJCM <AJCM@tutanota.com>
// SPDX-FileCopyrightText: 2024 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 Rainfall <rainfey0+git@gmail.com>
// SPDX-FileCopyrightText: 2024 Rainfey <rainfey0+github@gmail.com>
// SPDX-FileCopyrightText: 2024 Vasilis <vasilis@pikachu.systems>
// SPDX-FileCopyrightText: 2024 username <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 whateverusername0 <whateveremail>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Errant <35878406+Errant-4@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 SX_7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public abstract class SharedRoleSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private JobRequirementOverridePrototype? _requirementOverride;

    public override void Initialize()
    {
        Subs.CVar(_cfg, CCVars.GameRoleTimerOverride, SetRequirementOverride, true);

        SubscribeLocalEvent<MindRoleComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void SetRequirementOverride(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _requirementOverride = null;
            return;
        }

        if (!_prototypes.TryIndex(value, out _requirementOverride ))
            Log.Error($"Unknown JobRequirementOverridePrototype: {value}");
    }

    /// <summary>
    ///     Adds multiple mind roles to a mind
    /// </summary>
    /// <param name="mindId">The mind entity to add the role to</param>
    /// <param name="roles">The list of mind roles to add</param>
    /// <param name="mind">If the mind component is provided, it will be checked if it belongs to the mind entity</param>
    /// <param name="silent">If true, no briefing will be generated upon receiving the mind role</param>
    public void MindAddRoles(EntityUid mindId,
        List<EntProtoId>? roles,
        MindComponent? mind = null,
        bool silent = false)
    {
        if (roles is null || roles.Count == 0)
            return;

        foreach (var proto in roles)
        {
            MindAddRole(mindId, proto, mind, silent);
        }
    }

    /// <summary>
    ///     Adds a mind role to a mind
    /// </summary>
    /// <param name="mindId">The mind entity to add the role to</param>
    /// <param name="protoId">The mind role to add</param>
    /// <param name="mind">If the mind component is provided, it will be checked if it belongs to the mind entity</param>
    /// <param name="silent">If true, no briefing will be generated upon receiving the mind role</param>
    public void MindAddRole(EntityUid mindId,
        EntProtoId protoId,
        MindComponent? mind = null,
        bool silent = false)
    {
        if (protoId == "MindRoleJob")
            MindAddJobRole(mindId, mind, silent, "");
        else
            MindAddRoleDo(mindId, protoId, mind, silent);
    }

    /// <summary>
    /// Adds a Job mind role with the specified job prototype
    /// </summary>
    /// /// <param name="mindId">The mind entity to add the job role to</param>
    /// <param name="mind">If the mind component is provided, it will be checked if it belongs to the mind entity</param>
    /// <param name="silent">If true, no briefing will be generated upon receiving the mind role</param>
    /// <param name="jobPrototype">The Job prototype for the new role</param>
    public void MindAddJobRole(EntityUid mindId,
        MindComponent? mind = null,
        bool silent = false,
        string? jobPrototype = null)
    {
        // Can't have someone get paid for two jobs now, can we
        if (MindHasRole<JobRoleComponent>(mindId, out var jobRole)
            && jobRole.Value.Comp.JobPrototype != jobPrototype)
        {
            Resolve(mindId, ref mind);
            if (mind is not null)
            {
                _adminLogger.Add(LogType.Mind,
                    LogImpact.Low,
                    $"Job Role of {ToPrettyString(mind.OwnedEntity)} changed from '{jobRole.Value.Comp.JobPrototype}' to '{jobPrototype}'");
            }

            jobRole.Value.Comp.JobPrototype = jobPrototype;
        }
        else
            MindAddRoleDo(mindId, "MindRoleJob", mind, silent, jobPrototype);
    }

    /// <summary>
    ///     Creates a Mind Role
    /// </summary>
    private void MindAddRoleDo(EntityUid mindId,
        EntProtoId protoId,
        MindComponent? mind = null,
        bool silent = false,
        string? jobPrototype = null)
    {
        if (!Resolve(mindId, ref mind))
        {
            Log.Error($"Failed to add role {protoId} to mind {mindId} : Mind does not match provided mind component");
            return;
        }

        var antagonist = false;

        if (!_prototypes.TryIndex(protoId, out var protoEnt))
        {
            Log.Error($"Failed to add role {protoId} to mind {mindId} : Role prototype does not exist");
            return;
        }

        //TODO don't let a prototype being added a second time
        //If that was somehow to occur, a second mindrole for that comp would be created
        //Meaning any mind role checks could return wrong results, since they just return the first match they find

        var mindRoleId = Spawn(protoId, MapCoordinates.Nullspace);
        EnsureComp<MindRoleComponent>(mindRoleId);
        var mindRoleComp = Comp<MindRoleComponent>(mindRoleId);

        mindRoleComp.Mind = (mindId,mind);
        if (jobPrototype is not null)
        {
            mindRoleComp.JobPrototype = jobPrototype;
            EnsureComp<JobRoleComponent>(mindRoleId);
        }

        if (mindRoleComp.Antag || mindRoleComp.ExclusiveAntag)
            antagonist = true;

        mind.MindRoles.Add(mindRoleId);

        var mindEv = new MindRoleAddedEvent(silent);
        RaiseLocalEvent(mindId, ref mindEv);

        // RoleType refresh, Role time tracking, Update Admin playerlist

        var message = new RoleAddedEvent(mindId, mind, antagonist, silent);
        if (mind.OwnedEntity != null)
            RaiseLocalEvent(mind.OwnedEntity.Value, message, true);
        // RaiseLocalEvent(mindId, message, true); // Upstream, if you see errors remove two above lines above and uncomments this

        var name = Loc.GetString(protoEnt.Name);
        if (mind.OwnedEntity is not null)
        {
            _adminLogger.Add(LogType.Mind,
                LogImpact.Low,
                $"{name} added to mind of {ToPrettyString(mind.OwnedEntity)}");
        }
        else
        {
            //TODO: This is not tied to the player on the Admin Log filters.
            //Probably only happens when Job Role is added on initial spawn, before the mind entity is put in a mob
            _adminLogger.Add(LogType.Mind,
                LogImpact.Low,
                $"{name} added to {ToPrettyString(mindId)}");
        }
    }

    /// <summary>
    ///     Removes all instances of a specific role from this mind.
    /// </summary>
    /// <param name="mindId">The mind to remove the role from.</param>
    /// <typeparam name="T">The type of the role to remove.</typeparam>
    /// <exception cref="ArgumentException">Thrown if the mind does not exist or does not have this role.</exception>
    /// <returns>Returns False if there was something wrong with the mind or the removal. True if successful</returns>>
    public bool MindRemoveRole<T>(EntityUid mindId) where T : IComponent
    {
        if (!TryComp<MindComponent>(mindId, out var mind) )
            throw new ArgumentException($"{mindId} does not exist or does not have mind component");

        var found = false;
        var antagonist = false;
        var delete = new List<EntityUid>();
        foreach (var role in mind.MindRoles)
        {
            if (!HasComp<T>(role))
                continue;

            if (!TryComp(role, out MindRoleComponent? roleComp))
            {
                Log.Error($"Encountered mind role entity {ToPrettyString(role)} without a {nameof(MindRoleComponent)}");
                continue;
            }

            antagonist |= roleComp.Antag | roleComp.ExclusiveAntag;
            delete.Add(role);
            found = true;

        }

        foreach (var role in delete)
        {
            _entityManager.DeleteEntity(role);
        }

        var message = new RoleRemovedEvent(mindId, mind, antagonist);

        if (mind.OwnedEntity != null)
            RaiseLocalEvent(mind.OwnedEntity.Value, message, true);
        // RaiseLocalEvent(mind, message, true); // Also here, remove lines above and use this if you see errors
        _adminLogger.Add(LogType.Mind,
            LogImpact.Low,
            $"'Role {typeof(T).Name}' removed from mind of {ToPrettyString(mind.OwnedEntity)}");
        return true;
    }

    // Removing the mind role's reference on component shutdown
    // to make sure the reference gets removed even if the mind role entity was deleted by outside code
    private void OnComponentShutdown(Entity<MindRoleComponent> ent, ref ComponentShutdown args)
    {
        //TODO: Just ensure that the tests don't spawn unassociated mind role entities
        if (ent.Comp.Mind.Comp is null)
            return;

        ent.Comp.Mind.Comp.MindRoles.Remove(ent.Owner);
    }

    /// <summary>
    /// Finds and removes all mind roles of a specific type
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <typeparam name="T">The type of the role to remove.</typeparam>
    /// <returns>True if the role existed and was removed</returns>
    public bool MindTryRemoveRole<T>(EntityUid mindId) where T : IComponent
    {
        if (!MindHasRole<T>(mindId))
        {
            Log.Warning($"Failed to remove role {typeof(T)} from {mindId} : mind does not have role ");
            return false;
        }

        if (typeof(T) == typeof(MindRoleComponent))
            return false;

        return MindRemoveRole<T>(mindId);
    }

    /// <summary>
    /// Finds the first mind role of a specific T type on a mind entity.
    /// Outputs entity components for the mind role's MindRoleComponent and for T
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <typeparam name="T">The type of the role to find.</typeparam>
    /// <param name="role">The Mind Role entity component</param>
    /// <param name="roleT">The Mind Role's entity component for T</param>
    /// <returns>True if the role is found</returns>
    public bool MindHasRole<T>(EntityUid mindId,
        [NotNullWhen(true)] out Entity<MindRoleComponent>? role,
        [NotNullWhen(true)] out Entity<T>? roleT) where T : IComponent
    {
        role = null;
        roleT = null;

        if (!TryComp<MindComponent>(mindId, out var mind))
            return false;

        var found = false;

        foreach (var roleEnt in mind.MindRoles)
        {
            if (!HasComp<T>(roleEnt))
                continue;

            role = (roleEnt,Comp<MindRoleComponent>(roleEnt));
            roleT = (roleEnt,Comp<T>(roleEnt));
            found = true;
            break;
        }

        return found;
    }

    /// <summary>
    /// Finds the first mind role of a specific type on a mind entity.
    /// Outputs an entity component for the mind role's MindRoleComponent
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <param name="type">The Type to look for</param>
    /// <param name="role">The output role</param>
    /// <returns>True if the role is found</returns>
    public bool MindHasRole(EntityUid mindId,
        Type type,
        [NotNullWhen(true)] out Entity<MindRoleComponent>? role)
    {
        role = null;
        // All MindRoles have this component, it would just return the first one.
        // Order might not be what is expected.
        // Better to report null
        if (type == Type.GetType("MindRoleComponent"))
        {
            Log.Error($"Something attempted to query mind role 'MindRoleComponent' on mind {mindId}. This component is present on every single mind role.");
            return false;
        }

        if (!TryComp<MindComponent>(mindId, out var mind))
            return false;

        var found = false;

        foreach (var roleEnt in mind.MindRoles)
        {
            if (!HasComp(roleEnt, type))
                continue;

            role = (roleEnt,Comp<MindRoleComponent>(roleEnt));
            found = true;
            break;
        }

        return found;
    }

    /// <summary>
    /// Finds the first mind role of a specific type on a mind entity.
    /// Outputs an entity component for the mind role's MindRoleComponent
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <param name="role">The Mind Role entity component</param>
    /// <typeparam name="T">The type of the role to find.</typeparam>
    /// <returns>True if the role is found</returns>
    public bool MindHasRole<T>(EntityUid mindId,
        [NotNullWhen(true)] out Entity<MindRoleComponent>? role) where T : IComponent
    {
        return MindHasRole<T>(mindId, out role, out _);
    }

    /// <summary>
    /// Finds the first mind role of a specific type on a mind entity.
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <typeparam name="T">The type of the role to find.</typeparam>
    /// <returns>True if the role is found</returns>
    public bool MindHasRole<T>(EntityUid mindId) where T : IComponent
    {
        return MindHasRole<T>(mindId, out _, out _);
    }

    //TODO: Delete this later
    /// <summary>
    /// Returns the first mind role of a specific type
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <returns>Entity Component of the mind role</returns>
    [Obsolete("Use MindHasRole's output value")]
    public Entity<MindRoleComponent>? MindGetRole<T>(EntityUid mindId) where T : IComponent
    {
        Entity<MindRoleComponent>? result = null;

        var mind = Comp<MindComponent>(mindId);

        foreach (var uid in mind.MindRoles)
        {
            if (HasComp<T>(uid) && TryComp<MindRoleComponent>(uid, out var comp))
                result = (uid,comp);
        }
        return result;
    }

    /// <summary>
    /// Reads all Roles of a mind Entity and returns their data as RoleInfo
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <returns>RoleInfo list</returns>
    public List<RoleInfo> MindGetAllRoleInfo(EntityUid mindId)
    {
        var roleInfo = new List<RoleInfo>();

        if (!TryComp<MindComponent>(mindId, out var mind))
            return roleInfo;

        foreach (var role in mind.MindRoles)
        {
            var valid = false;
            var name = "game-ticker-unknown-role";
            var prototype = "";
           string? playTimeTracker = null;

            var comp = Comp<MindRoleComponent>(role);
            if (comp.AntagPrototype is not null)
            {
                prototype = comp.AntagPrototype;
            }

            if (comp.JobPrototype is not null && comp.AntagPrototype is null)
            {
                prototype = comp.JobPrototype;
                if (_prototypes.TryIndex(comp.JobPrototype, out var job))
                {
                    playTimeTracker = job.PlayTimeTracker;
                    name = job.Name;
                    valid = true;
                }
                else
                {
                    Log.Error($" Mind Role Prototype '{role.Id}' contains invalid Job prototype: '{comp.JobPrototype}'");
                }
            }
            else if (comp.AntagPrototype is not null && comp.JobPrototype is null)
            {
                prototype = comp.AntagPrototype;
                if (_prototypes.TryIndex(comp.AntagPrototype, out var antag))
                {
                    name = antag.Name;
                    valid = true;
                }
                else
                {
                    Log.Error($" Mind Role Prototype '{role.Id}' contains invalid Antagonist prototype: '{comp.AntagPrototype}'");
                }
            }
            else if (comp.JobPrototype is not null && comp.AntagPrototype is not null)
            {
                Log.Error($" Mind Role Prototype '{role.Id}' contains both Job and Antagonist prototypes");
            }

            if (valid)
                roleInfo.Add(new RoleInfo(name, comp.Antag || comp.ExclusiveAntag , playTimeTracker, prototype));
        }
        return roleInfo;
    }

    /// <summary>
    /// Does this mind possess an antagonist role
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <returns>True if the mind possesses any antag roles</returns>
    public bool MindIsAntagonist(EntityUid? mindId)
    {
        if (mindId is null)
        {
            Log.Warning($"Antagonist status of mind entity {mindId} could not be determined - mind entity not found");
            return false;
        }

        return CheckAntagonistStatus(mindId.Value).Item1;
    }

    /// <summary>
    /// Does this mind possess an exclusive antagonist role
    /// </summary>
    /// <param name="mindId">The mind entity</param>
    /// <returns>True if the mind possesses any exclusive antag roles</returns>
    public bool MindIsExclusiveAntagonist(EntityUid? mindId)
    {
        if (mindId is null)
        {
            Log.Warning($"Antagonist status of mind entity {mindId} could not be determined - mind entity not found");
            return false;
        }

        return CheckAntagonistStatus(mindId.Value).Item2;
    }

   private (bool, bool) CheckAntagonistStatus(EntityUid mindId)
   {
       if (!TryComp<MindComponent>(mindId, out var mind))
       {
           Log.Warning($"Antagonist status of mind entity {mindId} could not be determined - mind component not found");
           return (false, false);
       }

        var antagonist = false;
        var exclusiveAntag = false;
        foreach (var role in mind.MindRoles)
        {
            var roleComp = Comp<MindRoleComponent>(role);
            if (roleComp.Antag || exclusiveAntag)
                antagonist = true;
            if (roleComp.ExclusiveAntag)
                exclusiveAntag = true;
        }

        return (antagonist, exclusiveAntag);
    }

    /// <summary>
    /// Play a sound for the mind, if it has a session attached.
    /// Use this for role greeting sounds.
    /// </summary>
    public void MindPlaySound(EntityUid mindId, SoundSpecifier? sound, MindComponent? mind = null)
    {
        if (Resolve(mindId, ref mind) && mind.Session != null)
            _audio.PlayGlobal(sound, mind.Session);
    }

    // TODO ROLES Change to readonly.
    // Passing around a reference to a prototype's hashset makes me uncomfortable because it might be accidentally
    // mutated.
    public HashSet<JobRequirement>? GetJobRequirement(JobPrototype job)
    {
        if (_requirementOverride != null && _requirementOverride.Jobs.TryGetValue(job.ID, out var req))
            return req;

        return job.Requirements;
    }

    // TODO ROLES Change to readonly.
    public HashSet<JobRequirement>? GetJobRequirement(ProtoId<JobPrototype> job)
    {
        if (_requirementOverride != null && _requirementOverride.Jobs.TryGetValue(job, out var req))
            return req;

        return _prototypes.Index(job).Requirements;
    }

    // TODO ROLES Change to readonly.
    public HashSet<JobRequirement>? GetAntagRequirement(ProtoId<AntagPrototype> antag)
    {
        if (_requirementOverride != null && _requirementOverride.Antags.TryGetValue(antag, out var req))
            return req;

        return _prototypes.Index(antag).Requirements;
    }

    // TODO ROLES Change to readonly.
    public HashSet<JobRequirement>? GetAntagRequirement(AntagPrototype antag)
    {
        if (_requirementOverride != null && _requirementOverride.Antags.TryGetValue(antag.ID, out var req))
            return req;

        return antag.Requirements;
    }
}