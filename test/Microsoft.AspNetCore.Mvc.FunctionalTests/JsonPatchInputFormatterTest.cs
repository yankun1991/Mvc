// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FormatterWebSite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.FunctionalTests
{
    public class JsonPatchSampleTest : IClassFixture<MvcTestFixture<Startup>>
    {
        public JsonPatchSampleTest(MvcTestFixture<Startup> fixture)
        {
            Client = fixture.Client;
        }

        public HttpClient Client { get; }

        [Theory]
        [InlineData("http://localhost/jsonpatch/PatchCustomer")]
        [InlineData("http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch")]
        public async Task AddOperation_Works(string url)
        {
            // Arrange
            var input = "[{ \"op\": \"add\", " +
                "\"path\": \"Orders/-\", " +
               "\"value\": { \"OrderName\": \"Name2\" }}]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var customer = JsonConvert.DeserializeObject<Customer>(body);
            Assert.Equal("Name2", customer.Orders[2].OrderName);
        }

        [Theory]
        [InlineData("http://localhost/jsonpatch/PatchCustomer")]
        [InlineData("http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch")]
        public async Task ReplaceOperation_Works(string url)
        {
            // Arrange
            var input = "[{ \"op\": \"replace\", " +
                "\"path\": \"Orders/0/OrderName\", " +
               "\"value\": \"ReplacedOrder\" }]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var customer = JsonConvert.DeserializeObject<Customer>(body);
            Assert.Equal("ReplacedOrder", customer.Orders[0].OrderName);
        }

        [Theory]
        [InlineData("http://localhost/jsonpatch/PatchCustomer")]
        [InlineData("http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch")]
        public async Task CopyOperation_Works(string url)
        {
            // Arrange
            var input = "[{ \"op\": \"copy\", " +
                "\"path\": \"Orders/1/OrderName\", " +
               "\"from\": \"Orders/0/OrderName\"}]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var customer = JsonConvert.DeserializeObject<Customer>(body);
            Assert.Equal("Order0", customer.Orders[1].OrderName);
        }

        [Theory]
        [InlineData("http://localhost/jsonpatch/PatchCustomer")]
        [InlineData("http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch")]
        public async Task MoveOperation_Works(string url)
        {
            // Arrange
            var input = "[{ \"op\": \"move\", " +
                "\"path\": \"Orders/1/OrderName\", " +
               "\"from\": \"Orders/0/OrderName\"}]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var customer = JsonConvert.DeserializeObject<Customer>(body);
            Assert.Equal("Order0", customer.Orders[1].OrderName);
            Assert.Null(customer.Orders[0].OrderName);
        }

        [Theory]
        [InlineData("http://localhost/jsonpatch/PatchCustomer")]
        [InlineData("http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch")]
        public async Task RemoveOperation_Works(string url)
        {
            // Arrange
            var input = "[{ \"op\": \"remove\", " +
                "\"path\": \"Orders/1/OrderName\"}]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var customer = JsonConvert.DeserializeObject<Customer>(body);
            Assert.Null(customer.Orders[1].OrderName);
        }

        [Theory]
        [InlineData("http://localhost/jsonpatch/PatchCustomer")]
        [InlineData("http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch")]
        public async Task MultipleValidOperations_Success(string url)
        {
            // Arrange
            var input = "[{ \"op\": \"add\", " +
                "\"path\": \"Orders/-\", " +
               "\"value\": { \"OrderName\": \"Name2\" }}, " +
               "{\"op\": \"copy\", " +
               "\"from\": \"Orders/2\", " +
                "\"path\": \"Orders/-\" }, " +
                "{\"op\": \"replace\", " +
                "\"path\": \"Orders/2/OrderName\", " +
                "\"value\": \"ReplacedName\" }]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var customer = JsonConvert.DeserializeObject<Customer>(body);
            Assert.Equal("ReplacedName", customer.Orders[2].OrderName);
            Assert.Equal("Name2", customer.Orders[3].OrderName);
        }

        public static IEnumerable<object[]> InvalidJsonPatchData
        {
            get
            {
                return new[]
                {
                    new object[] {
                        "http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch",
                        "[{ \"op\": \"add\", " +
                        "\"path\": \"Orders/5\", " +
                        "\"value\": { \"OrderName\": \"Name5\" }}]",
                        "{\"Patch.Customer\":[\"The index value provided by path segment '5' is out of bounds of the array size.\"]}"
                    },
                    new object[] {
                        "http://localhost/jsonpatch/PatchCustomer",
                        "[{ \"op\": \"add\", " +
                        "\"path\": \"Orders/5\", " +
                        "\"value\": { \"OrderName\": \"Name5\" }}]",
                        "{\"Customer\":[\"The index value provided by path segment '5' is out of bounds of the array size.\"]}"
                    },
                    new object[] {
                        "http://localhost/jsonpatch/PatchCustomerWithPrefix?prefix=Patch",
                        "[{ \"op\": \"add\", " +
                        "\"path\": \"Orders/-\", " +
                        "\"value\": { \"OrderName\": \"Name2\" }}, " +
                        "{\"op\": \"copy\", " +
                        "\"from\": \"Orders/4\", " +
                        "\"path\": \"Orders/3\" }, " +
                        "{\"op\": \"replace\", " +
                        "\"path\": \"Orders/2/OrderName\", " +
                        "\"value\": \"ReplacedName\" }]",
                        "{\"Patch.Customer\":[\"The index value provided by path segment '4' is out of bounds of the array size.\"]}"
                    },
                    new object[] {
                        "http://localhost/jsonpatch/PatchCustomer",
                        "[{ \"op\": \"add\", " +
                        "\"path\": \"Orders/-\", " +
                        "\"value\": { \"OrderName\": \"Name2\" }}, " +
                        "{\"op\": \"copy\", " +
                        "\"from\": \"Orders/4\", " +
                        "\"path\": \"Orders/3\" }, " +
                        "{\"op\": \"replace\", " +
                        "\"path\": \"Orders/2/OrderName\", " +
                        "\"value\": \"ReplacedName\" }]",
                        "{\"Customer\":[\"The index value provided by path segment '4' is out of bounds of the array size.\"]}"
                    }
                };
            }
        }

        [Theory, MemberData("InvalidJsonPatchData")]
        public async Task InvalidOperation_Fails(string url, string input, string errorMessage)
        {
            // Arrange
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(url)
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(errorMessage, body);
        }

        [Fact]
        public async Task InvalidData_Results()
        {
            // Arrange
            var input = "{ \"op\": \"add\", " +
                "\"path\": \"Orders/2\", " +
               "\"value\": { \"OrderName\": \"Name2\" }}";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri("http://localhost/jsonpatch/PatchCustomer")
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("{\"\":[\"The input was not valid.\"]}", body);
        }

        [Fact]
        public async Task UsesJsonConverterOnPropertyWhenPatching_Works()
        {
            // Arrange
            var input = "[{ \"op\": \"add\", " +
                "\"path\": \"Orders/-\", " +
               "\"value\": { \"OrderType\": \"Type2\" }}]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri("http://localhost/jsonpatch/PatchCustomer")
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            dynamic d = JObject.Parse(body);
            Assert.Equal("OrderTypeSetInConverter", (string)d.orders[2].orderType);
        }

        [Fact]
        public async Task JsonPatch_JsonConverterOnClass_Success()
        {
            // Arrange
            var input = "[{ \"op\": \"add\", " +
                "\"path\": \"ProductCategory\", " +
               "\"value\": { \"CategoryName\": \"Name2\" }}]";
            var request = new HttpRequestMessage
            {
                Content = new StringContent(input, Encoding.UTF8, "application/json-patch+json"),
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri("http://localhost/jsonpatch/PatchProduct")
            };

            // Act
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            dynamic d = JObject.Parse(body);
            Assert.Equal("CategorySetInConverter", (string)d.productCategory.CategoryName);
        }
    }
}