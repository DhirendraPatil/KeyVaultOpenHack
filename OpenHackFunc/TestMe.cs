using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
namespace OpenHackFunc
{
    public static class TestMe
    {
        static string keyVault_resource = "https://vault.azure.net";
        static string msiEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        static string msiSecret = Environment.GetEnvironmentVariable("IDENTITY_HEADER");
        static string keyVaultUrl = "https://mso-keyvault.vault.azure.net/secrets/";
        [FunctionName("TestMe")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            var token = await GetToken(keyVault_resource);
            //TODO Use this token to connect SecureVault. Dont return this token to caller.

            string responseMessage = string.IsNullOrEmpty(name)
                ? "Pass a KeyVaut secret name in the query string to retrieve secrets." : $"Successfully aquired Access token for {keyVault_resource}. ";
            log.LogInformation($"Successfully aquired Access token for {keyVault_resource}. Getting Secretes from KeyVault {keyVaultUrl}");
            return new OkObjectResult(responseMessage);
        }
        static async Task<MsiToken> GetToken(string resource)
        {
            if (string.IsNullOrEmpty(msiEndpoint)) throw new ArgumentNullException("MSI Endpoint is not setup. Make sure application identity is set up Managed Identity.");
            if (string.IsNullOrEmpty(msiSecret)) throw new ArgumentNullException("MSI Secret is not setup. Make sure application identity is set up Managed Identity.");

            var _client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, string.Format("{0}/?resource={1}&api-version=2019-08-01", msiEndpoint, resource));
            request.Headers.Add("X-IDENTITY-HEADER", msiSecret);
             var resMsg = await _client.SendAsync(request);
            var result = await resMsg.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MsiToken>(result);

        }
    }

}
