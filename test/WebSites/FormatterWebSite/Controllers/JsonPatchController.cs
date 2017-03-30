// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace FormatterWebSite.Controllers
{
    [Route("jsonpatch/[action]")]
    public class JsonPatchController : Controller
    {
        [HttpPatch]
        public IActionResult PatchCustomer([FromBody] JsonPatchDocument<Customer> patchDoc)
        {
            // Check for model state here as the format(ex: structure) of the JSON patch request could be incorrect.
            if (patchDoc != null)
            {
                var customer = CreateCustomer();

                // Supply model state here to capture any errors which could result from invalid data.
                // For example, inserting a value at an invalid index in a list.
                patchDoc.ApplyTo(customer, ModelState);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                return Ok(customer);
            }
            else
            {
                return BadRequest(ModelState);
            }
        }

        [HttpPatch]
        public IActionResult PatchCustomerWithPrefix([FromBody] JsonPatchDocument<Customer> patchDoc, string prefix)
        {
            // Check for model state here as the format(ex: structure) of the JSON patch request could be incorrect.
            if (patchDoc != null)
            {
                var customer = CreateCustomer();

                // Supply model state here to capture any errors which could result from invalid data.
                // For example, inserting a value at an invalid index in a list.
                patchDoc.ApplyTo(customer, ModelState, prefix);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                return Ok(customer);
            }
            else
            {
                return BadRequest(ModelState);
            }
        }

        [HttpPatch]
        public IActionResult PatchProduct([FromBody] JsonPatchDocument<Product> patchDoc)
        {
            if (patchDoc != null)
            {
                var product = new Product();

                patchDoc.ApplyTo(product, ModelState);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                return Ok(product);
            }
            else
            {
                return BadRequest(ModelState);
            }
        }

        private Customer CreateCustomer()
        {
            return new Customer
            {
                CustomerName = "John",
                Orders = new List<Order>()
                {
                    new Order
                    {
                        OrderName = "Order0"
                    },
                    new Order
                    {
                        OrderName = "Order1"
                    }
                }
            };
        }
    }
}
