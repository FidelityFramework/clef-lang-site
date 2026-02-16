namespace ClefLang.CLI.Core

open System.Net.Http

module HttpHelpers =

    /// Create an HttpClient configured with Cloudflare API authentication
    let createAuthenticatedClient (apiToken: string) : HttpClient =
        let client = new HttpClient()
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}")
        client.BaseAddress <- System.Uri("https://api.cloudflare.com/client/v4")
        client
