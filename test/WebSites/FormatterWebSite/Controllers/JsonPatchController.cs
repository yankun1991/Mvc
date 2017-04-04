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

        private Product CreateProduct()
        {
            return new Product
            {
                Name = "Book1",
                Reviews = new List<Review>()
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
