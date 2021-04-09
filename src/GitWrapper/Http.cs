using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;

namespace Gitman
{
    public abstract class Http
    {
        public string ApiToken { get; set; }

        public virtual async Task<HttpResponseMessage> Get(string callUrl, Dictionary<string, string> headers = null)
        {
            return await SendWithMethod(HttpMethod.Get, callUrl, headers);
        }

        public virtual async Task<T> Get<T>(string callUrl, Dictionary<string, string> headers = null)
        {
            var response = await SendWithMethod(HttpMethod.Get, callUrl, headers);
            using var contentStream = await response.Content.ReadAsStreamAsync();
            T body = await JsonSerializer.DeserializeAsync<T>(
                contentStream,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
            return body;
        }

        private async Task<HttpResponseMessage> SendWithMethod(HttpMethod method, string callUrl, Dictionary<string, string> headers = null)
        {
            using (var httpClient = new HttpClient())
            {
                using (var httpRequest = new HttpRequestMessage(method, callUrl))
                {
                    httpRequest.Headers.Add("Accept", "application/vnd.github.v3+json");
                    httpRequest.Headers.Add("User-Agent", "TeamGitManager");
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", ApiToken);
                    if (headers != null)
                    {
                        foreach (KeyValuePair<string, string> header in headers)
                        {
                            httpRequest.Headers.Add(header.Key, header.Value);
                        }
                    }

                    var response = await httpClient.SendAsync(httpRequest);

                    if (!response.IsSuccessStatusCode)
                    {
                        var responsebody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Call to external resource was not successfull! {0}",
                            new
                            {
                                url = callUrl
                                , responseHttpStatus = response.StatusCode
                                , responseReasonPhrase = response.ReasonPhrase
                                , responseBody = responsebody
                            });
                    }

                    return response;
                }
            }
        }
    }
}