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
    public static class KeyVaultMSIExample
    {
        static string keyVault_resource = "https://vault.azure.net";
        static string keyVaultApiVer = "?api-version=2016-10-01";
        static string keyVaultUrl = "https://mso-keyvault.vault.azure.net/secrets/";
        static string msiEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        static string msiSecret = Environment.GetEnvironmentVariable("IDENTITY_HEADER");
        [FunctionName("KeyVaultMSIExample")]
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
            string responseMessage = string.IsNullOrEmpty(name)
                ? "Pass a KeyVaut secret name in the query string to retrieve secrets." : $"Successfully aquired Access token for {keyVault_resource}. ";
            log.LogInformation($"Successfully aquired Access token for {keyVault_resource}. Getting Secretes from KeyVault {keyVaultUrl}");
            var secret = await GetSecrets(token, name);
            responseMessage = responseMessage + $" Secret value aquired from keyvault with Id {secret.Id}.";
            //secret.Logs = responseMessage;
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
        static async Task<Secrets> GetSecrets(MsiToken accessToken, string keyName)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", accessToken.Token_Type + " " + accessToken.Access_Token);
            var requrl = keyVaultUrl + keyName + keyVaultApiVer;
            var resMsg = await client.GetAsync(requrl);
            resMsg.EnsureSuccessStatusCode();
            var result = await resMsg.Content.ReadAsAsync<Secrets>();
            return result;
        }
    }

    class Secrets
    {
        public string Value { get; set; }
        public string Id { get; set; }
        public SecretAttributes Attributes { get; set; }
        public string Logs { get; set; }
    }
    class SecretAttributes
    {
        public bool Enable { get; set; }
        public string Created { get; set; }
        public string Updated { get; set; }
        public string RecoveryLevel { get; set; }
    }
    class MsiToken
    {
        public string Access_Token { get; set; }
        public string Expires_On { get; set; }
        public string Resource { get; set; }
        public string Token_Type { get; set; }
        public string Client_Id { get; set; }
    }
}
