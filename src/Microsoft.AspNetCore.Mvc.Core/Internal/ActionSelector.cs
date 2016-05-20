// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    /// <summary>
    /// A default <see cref="IActionSelector"/> implementation.
    /// </summary>
    public class ActionSelector : IActionSelector
    {
        private static readonly ActionDescriptor[] EmptyActions = new ActionDescriptor[0];

        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
        private readonly ActionConstraintCache _actionConstraintCache;
        private readonly ILogger _logger;

        private Cache _cache;

        /// <summary>
        /// Creates a new <see cref="ActionSelector"/>.
        /// </summary>
        /// <param name="actionDescriptorCollectionProvider">
        /// The <see cref="IActionDescriptorCollectionProvider"/>.
        /// </param>
        /// <param name="actionConstraintCache">The <see cref="ActionConstraintCache"/> that
        /// providers a set of <see cref="IActionConstraint"/> instances.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public ActionSelector(
            IActionDescriptorCollectionProvider actionDescriptorCollectionProvider,
            ActionConstraintCache actionConstraintCache,
            ILoggerFactory loggerFactory)
        {
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            _logger = loggerFactory.CreateLogger<ActionSelector>();
            _actionConstraintCache = actionConstraintCache;
        }

        private Cache Current
        {
            get
            {
                var actions = _actionDescriptorCollectionProvider.ActionDescriptors;
                var cache = Volatile.Read(ref _cache);

                if (cache != null && cache.Version == actions.Version)
                {
                    return cache;
                }

                cache = new Cache(actions);
                _cache = cache;
                return cache;
            }
        }

        public IReadOnlyList<ActionDescriptor> SelectCandidates(RouteContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var cache = Current;

            // The Cache works based on a string[] of the route values in a pre-calculated order. This code extracts
            // those values in the correct order.
            var keys = cache.RouteKeys;
            var values = new string[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                context.RouteData.Values.TryGetValue(keys[i], out object value);

                if (value != null)
                {
                    values[i] = value as string ?? Convert.ToString(value);
                }
            }
            
            if (!cache.OrdinalEntries.TryGetValue(values, out var matchingRouteValues))
            {
                if (!cache.OrdinalIgnoreCaseEntries.TryGetValue(values, out matchingRouteValues))
                {

                    _logger.NoActionsMatched(context.RouteData.Values);
                    return null;
                }
            }

            return (IReadOnlyList<ActionDescriptor>)matchingRouteValues ?? (IReadOnlyList<ActionDescriptor>)EmptyActions;
        }

        public ActionDescriptor SelectBestCandidate(RouteContext context, IReadOnlyList<ActionDescriptor> candidates)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var matches = EvaluateActionConstraints(context, candidates);

            var finalMatches = SelectBestActions(matches);
            if (finalMatches == null || finalMatches.Count == 0)
            {
                return null;
            }
            else if (finalMatches.Count == 1)
            {
                var selectedAction = finalMatches[0];

                return selectedAction;
            }
            else
            {
                var actionNames = string.Join(
                    Environment.NewLine,
                    finalMatches.Select(a => a.DisplayName));

                _logger.AmbiguousActions(actionNames);

                var message = Resources.FormatDefaultActionSelector_AmbiguousActions(
                    Environment.NewLine,
                    actionNames);

                throw new AmbiguousActionException(message);
            }
        }

        /// <summary>
        /// Returns the set of best matching actions.
        /// </summary>
        /// <param name="actions">The set of actions that satisfy all constraints.</param>
        /// <returns>A list of the best matching actions.</returns>
        protected virtual IReadOnlyList<ActionDescriptor> SelectBestActions(IReadOnlyList<ActionDescriptor> actions)
        {
            return actions;
        }

        private IReadOnlyList<ActionDescriptor> EvaluateActionConstraints(
            RouteContext context,
            IReadOnlyList<ActionDescriptor> actions)
        {
            var candidates = new List<ActionSelectorCandidate>();

            // Perf: Avoid allocations
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var constraints = _actionConstraintCache.GetActionConstraints(context.HttpContext, action);
                candidates.Add(new ActionSelectorCandidate(action, constraints));
            }

            var matches = EvaluateActionConstraintsCore(context, candidates, startingOrder: null);

            List<ActionDescriptor> results = null;
            if (matches != null)
            {
                results = new List<ActionDescriptor>(matches.Count);
                // Perf: Avoid allocations
                for (var i = 0; i < matches.Count; i++)
                {
                    var candidate = matches[i];
                    results.Add(candidate.Action);
                }
            }

            return results;
        }

        private IReadOnlyList<ActionSelectorCandidate> EvaluateActionConstraintsCore(
            RouteContext context,
            IReadOnlyList<ActionSelectorCandidate> candidates,
            int? startingOrder)
        {
            // Find the next group of constraints to process. This will be the lowest value of
            // order that is higher than startingOrder.
            int? order = null;

            // Perf: Avoid allocations
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Constraints != null)
                {
                    for (var j = 0; j < candidate.Constraints.Count; j++)
                    {
                        var constraint = candidate.Constraints[j];
                        if ((startingOrder == null || constraint.Order > startingOrder) &&
                            (order == null || constraint.Order < order))
                        {
                            order = constraint.Order;
                        }
                    }
                }
            }

            // If we don't find a 'next' then there's nothing left to do.
            if (order == null)
            {
                return candidates;
            }

            // Since we have a constraint to process, bisect the set of actions into those with and without a
            // constraint for the 'current order'.
            var actionsWithConstraint = new List<ActionSelectorCandidate>();
            var actionsWithoutConstraint = new List<ActionSelectorCandidate>();

            var constraintContext = new ActionConstraintContext();
            constraintContext.Candidates = candidates;
            constraintContext.RouteContext = context;

            // Perf: Avoid allocations
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var isMatch = true;
                var foundMatchingConstraint = false;

                if (candidate.Constraints != null)
                {
                    constraintContext.CurrentCandidate = candidate;
                    for (var j = 0; j < candidate.Constraints.Count; j++)
                    {
                        var constraint = candidate.Constraints[j];
                        if (constraint.Order == order)
                        {
                            foundMatchingConstraint = true;

                            if (!constraint.Accept(constraintContext))
                            {
                                isMatch = false;
                                _logger.ConstraintMismatch(
                                    candidate.Action.DisplayName,
                                    candidate.Action.Id,
                                    constraint);
                                break;
                            }
                        }
                    }
                }

                if (isMatch && foundMatchingConstraint)
                {
                    actionsWithConstraint.Add(candidate);
                }
                else if (isMatch)
                {
                    actionsWithoutConstraint.Add(candidate);
                }
            }

            // If we have matches with constraints, those are 'better' so try to keep processing those
            if (actionsWithConstraint.Count > 0)
            {
                var matches = EvaluateActionConstraintsCore(context, actionsWithConstraint, order);
                if (matches?.Count > 0)
                {
                    return matches;
                }
            }

            // If the set of matches with constraints can't work, then process the set without constraints.
            if (actionsWithoutConstraint.Count == 0)
            {
                return null;
            }
            else
            {
                return EvaluateActionConstraintsCore(context, actionsWithoutConstraint, order);
            }
        }

        // The action selector cache stores a mapping of route-values -> action descriptors for each 'known' set of
        // of route-values. We actually build two of these mappings, one for case-sensitive (fast path) and one for
        // case-insensitive (slow path).
        //
        // The difference here is because while MVC is case-insensitive, doing a case-sensitive comparison is much 
        // faster. We also expect that most of the URLs we process are canonically-cased because they were generated
        // by Url.Action or another routing api.
        //
        // This means that for a set of actions like:
        //      { controller = "Home", action = "Index" } -> HomeController::Index1()
        //      { controller = "Home", action = "index" } -> HomeController::Index2()
        //
        // There are two entries in the case-sensitive dictionary, each one maps maps to **both** of these action
        // methods.
        private class Cache
        {
            public Cache(ActionDescriptorCollection actions)
            {
                // We need to store the version so the cache can be invalidated if the actions change.
                Version = actions.Version;

                // We need to build two maps for all of the route values.
                OrdinalEntries = new Dictionary<string[], List<ActionDescriptor>>(OrdinalEqualityComparer.Instance);
                OrdinalIgnoreCaseEntries = new Dictionary<string[], List<ActionDescriptor>>(OrdinalIgnoreCaseEqualityComparer.Instance);

                // We need to first identify of the 'keys' of all the actions. We want to only consider conventionally
                // routed actions here.
                var conventionallyRoutedActions = 0;
                var routeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < actions.Items.Count; i++)
                {
                    var action = actions.Items[i];
                    if (action.AttributeRouteInfo == null)
                    {
                        // This is a conventionally routed action - so make sure we include its 'keys' in the set of
                        // known route value keys.
                        conventionallyRoutedActions++;

                        foreach (var kvp in action.RouteValues)
                        {
                            routeKeys.Add(kvp.Key);
                        }
                    }
                }

                // We need to hold on to an ordered set of 'keys' for the route values. We'll use these later to
                // extract the set of route values from an incoming request to compare against our maps of known
                // route values.
                RouteKeys = routeKeys.ToArray();

                var actionsAndKeys = new (ActionDescriptor action, string[] keys)[conventionallyRoutedActions];

                var count = 0;
                for (var i = 0; i < actions.Items.Count; i++)
                {
                    var action = actions.Items[i];
                    if (action.AttributeRouteInfo == null)
                    {
                        // This is a conventionally routed action - so we need to extract the route values associated
                        // with this action (in order) so we can store them in our maps.
                        var values = new string[RouteKeys.Length];
                        for (var j = 0; j < RouteKeys.Length; j++)
                        {
                            action.RouteValues.TryGetValue(RouteKeys[j], out values[j]);
                        }

                        actionsAndKeys[count++] = (action, values);

                        List<ActionDescriptor> entries;
                        if (!OrdinalIgnoreCaseEntries.TryGetValue(values, out entries))
                        {
                            entries = new List<ActionDescriptor>();
                            OrdinalIgnoreCaseEntries.Add(values, entries);
                        }

                        entries.Add(action);
                    }
                }

                // At this point `OrdinalIgnoreCaseEntries[key].Value` contains all of the actions in the same
                // equivalence class - meaning, all conventionally routed actions for which the route values are equal
                // ignoring case.
                //
                // Now we want to iterate the actions **again** and for each unique casing, map this back to the full
                // set for that equivalence class.
                for (var i = 0; i < actionsAndKeys.Length; i++)
                {
                    var actionAndKeys = actionsAndKeys[i];
                    if (!OrdinalEntries.ContainsKey(actionAndKeys.keys))
                    {
                        OrdinalEntries[actionAndKeys.keys] = OrdinalIgnoreCaseEntries[actionAndKeys.keys];
                    }
                }
            }

            public int Version { get; set; }

            public string[] RouteKeys { get; set; }

            public Dictionary<string[], List<ActionDescriptor>> OrdinalEntries { get; set; }

            public Dictionary<string[], List<ActionDescriptor>> OrdinalIgnoreCaseEntries { get; set; }
        }

        private class OrdinalEqualityComparer : IEqualityComparer<string[]>
        {
            public static readonly IEqualityComparer<string[]> Instance = new OrdinalEqualityComparer();

            public bool Equals(string[] x, string[] y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null ^ y == null)
                {
                    return false;
                }

                if (x.Length != y.Length)
                {
                    return false;
                }

                for (var i = 0; i < x.Length; i++)
                {
                    if (string.IsNullOrEmpty(x[i]) && string.IsNullOrEmpty(y[i]))
                    {
                        continue;
                    }

                    if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(string[] obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                var hash = new HashCodeCombiner();
                for (var i = 0; i < obj.Length; i++)
                {
                    hash.Add(obj[i], StringComparer.Ordinal);
                }

                return hash.CombinedHash;
            }
        }

        private class OrdinalIgnoreCaseEqualityComparer : IEqualityComparer<string[]>
        {
            public static readonly IEqualityComparer<string[]> Instance = new OrdinalIgnoreCaseEqualityComparer();

            public bool Equals(string[] x, string[] y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null ^ y == null)
                {
                    return false;
                }

                if (x.Length != y.Length)
                {
                    return false;
                }

                for (var i = 0; i < x.Length; i++)
                {
                    if (string.IsNullOrEmpty(x[i]) && string.IsNullOrEmpty(y[i]))
                    {
                        continue;
                    }

                    if (!string.Equals(x[i], y[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(string[] obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                var hash = new HashCodeCombiner();
                for (var i = 0; i < obj.Length; i++)
                {
                    hash.Add(obj[i], StringComparer.OrdinalIgnoreCase);
                }

                return hash.CombinedHash;
            }
        }
    }
}
