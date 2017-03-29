// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages.Internal;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Razor.Evolution;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    public class PageActionDescriptorProvider : IActionDescriptorProvider
    {
        private static readonly string IndexFileName = "Index.cshtml";
        private readonly RazorProject _project;
        private readonly MvcOptions _mvcOptions;
        private readonly RazorPagesOptions _pagesOptions;

        public PageActionDescriptorProvider(
            RazorProject project,
            IOptions<MvcOptions> mvcOptionsAccessor,
            IOptions<RazorPagesOptions> pagesOptionsAccessor)
        {
            _project = project;
            _mvcOptions = mvcOptionsAccessor.Value;
            _pagesOptions = pagesOptionsAccessor.Value;
        }

        public int Order { get; set; }

        public void OnProvidersExecuting(ActionDescriptorProviderContext context)
        {
            foreach (var item in _project.EnumerateItems(_pagesOptions.RootDirectory))
            {
                if (item.FileName.StartsWith("_"))
                {
                    // Files like _ViewImports.cshtml should not be routable.
                    continue;
                }

                if (!PageDirectiveFeature.TryGetPageDirective(item, out var directive))
                {
                    // .cshtml pages without @page are not RazorPages.
                    continue;
                }

                if (AttributeRouteModel.IsOverridePattern(directive.RouteTemplate))
                {
                    throw new InvalidOperationException(string.Format(
                        Resources.PageActionDescriptorProvider_RouteTemplateCannotBeOverrideable,
                        item.Path));
                }

                AddActionDescriptors(context.Results, item, directive);
            }
        }

        public void OnProvidersExecuted(ActionDescriptorProviderContext context)
        {
        }

        private void AddActionDescriptors(IList<ActionDescriptor> actions, RazorProjectItem item, PageDirectiveFeature directiveInfo)
        {
            var rootRelativePath = item.CombinedPath;
            var viewEnginePath = item.PathWithoutExtension;
            var name = directiveInfo.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = viewEnginePath;
            }
            var model = new PageApplicationModel(rootRelativePath, viewEnginePath)
            {
                Name = name,
            };
            var routePrefix = item.PathWithoutExtension;
            model.Selectors.Add(CreateSelectorModel(routePrefix, directiveInfo.RouteTemplate));

            if (string.Equals(IndexFileName, item.FileName, StringComparison.OrdinalIgnoreCase))
            {
                var parentDirectoryPath = item.Path;
                var index = parentDirectoryPath.LastIndexOf('/');
                if (index == -1)
                {
                    parentDirectoryPath = string.Empty;
                }
                else
                {
                    parentDirectoryPath = parentDirectoryPath.Substring(0, index);
                }
                model.Selectors.Add(CreateSelectorModel(parentDirectoryPath, directiveInfo.RouteTemplate));
            }

            for (var i = 0; i < _pagesOptions.Conventions.Count; i++)
            {
                _pagesOptions.Conventions[i].Apply(model);
            }

            var filters = new List<FilterDescriptor>(_mvcOptions.Filters.Count + model.Filters.Count);
            for (var i = 0; i < _mvcOptions.Filters.Count; i++)
            {
                filters.Add(new FilterDescriptor(_mvcOptions.Filters[i], FilterScope.Global));
            }

            for (var i = 0; i < model.Filters.Count; i++)
            {
                filters.Add(new FilterDescriptor(model.Filters[i], FilterScope.Action));
            }

            foreach (var selector in model.Selectors)
            {
                var order = selector.AttributeRouteModel.Order ?? 0;
                var attributeRouteInfo = new AttributeRouteInfo()
                {
                    Name = selector.AttributeRouteModel.Name,
                    Order = order,
                    Template = selector.AttributeRouteModel.Template,
                };
                actions.Add(CreateDescriptor(item, model, filters, attributeRouteInfo, item.PathWithoutExtension));

                if (!string.Equals(model.Name, viewEnginePath, StringComparison.Ordinal))
                {
                    // If a page has an alias, register an ActionDescriptor with a higher-ordered AttributeRoute.
                    // The ordering would ensure that the route gets used for link generation, but is never used for inbound routing.
                    attributeRouteInfo = new AttributeRouteInfo()
                    {
                        Name = selector.AttributeRouteModel.Name,
                        Order = order + 1,
                        Template = selector.AttributeRouteModel.Template,
                    };

                    actions.Add(CreateDescriptor(item, model, filters, attributeRouteInfo, model.Name));
                }
            }
        }

        private static PageActionDescriptor CreateDescriptor(
            RazorProjectItem item,
            PageApplicationModel model,
            List<FilterDescriptor> filters,
            AttributeRouteInfo attributeRouteInfo,
            string pageValue)
        {
            return new PageActionDescriptor()
            {
                AttributeRouteInfo = attributeRouteInfo,
                DisplayName = $"Page: {item.Path}",
                FilterDescriptors = filters,
                Properties = new Dictionary<object, object>(model.Properties),
                RelativePath = item.CombinedPath,
                RouteValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "page", pageValue },
                },
                ViewEnginePath = item.Path,
            };
        }

        private static SelectorModel CreateSelectorModel(string prefix, string template)
        {
            return new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = AttributeRouteModel.CombineTemplates(prefix, template),
                }
            };
        }
    }
}