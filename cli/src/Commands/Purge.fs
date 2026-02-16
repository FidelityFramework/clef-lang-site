namespace ClefLang.CLI.Commands

open System
open System.Net.Http
open System.Text.Json
open ClefLang.CLI

module Purge =

    type PurgeResponse = {
        success: bool
        deleted: int
        message: string
    }

    let execute
        (config: Config.CloudflareConfig)
        (useLocal: bool)
        (localPort: int)
        (verbose: bool)
        : Async<Result<int, string>> =
        async {
            // Determine worker URL and API key
            let workerUrl, apiKey =
                if useLocal then
                    $"http://localhost:{localPort}", "dev-local-key"
                else
                    let state = Config.loadState () |> Option.defaultValue Config.defaultState
                    let url = state.ContentSyncWorkerUrl |> Option.defaultValue ""
                    let key = state.ContentSyncApiKey |> Option.defaultValue ""
                    url, key

            if String.IsNullOrEmpty(workerUrl) then
                return Error "Content-sync worker not deployed. Run 'deploy' first or use --local flag."
            elif String.IsNullOrEmpty(apiKey) then
                return Error "Content-sync API key not configured. Use --local flag for local development."
            else

            printfn "Purging R2 bucket via content-sync worker..."
            printfn "  Worker URL: %s" workerUrl
            printfn ""

            use httpClient = new HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}")

            let purgeUrl = $"{workerUrl}/purge"

            try
                use content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                let! response = httpClient.PostAsync(purgeUrl, content) |> Async.AwaitTask
                let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                if response.IsSuccessStatusCode then
                    let jsonOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
                    let result = JsonSerializer.Deserialize<PurgeResponse>(responseBody, jsonOptions)

                    printfn "Purge complete: %d objects deleted" result.deleted
                    return Ok result.deleted
                else
                    return Error $"Purge failed: {response.StatusCode} - {responseBody}"

            with ex ->
                return Error $"Purge error: {ex.Message}"
        }
