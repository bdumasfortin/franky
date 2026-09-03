using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Franky.Runtime.Configuration;
using Franky.Runtime.Conversation;
using Franky.Runtime.Diagnostics;
using Franky.Runtime.Speech;
using Franky.Runtime.Tools;
using Microsoft.Extensions.FileProviders;

namespace Franky.Runtime.ControlBoard;

public static class ControlBoardApplication
{
    private const int MaxAudioBytes = 2 * 1024 * 1024;
    private const int MaxTranscriptCharacters = 4_000;

    public static async Task<int> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        AddInstalledCudaToProcessPath();

        var port = ReadIntArgument(arguments, "--port", 8765);
        var webRoot = ReadStringArgument(arguments, "--web-root") ??
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "franky-control-board"));
        if (!Directory.Exists(webRoot))
        {
            throw new DirectoryNotFoundException($"Franky control-board files were not found at '{webRoot}'.");
        }
        var presenceRoot = Path.GetFullPath(Path.Combine(webRoot, "..", "franky-presence"));
        if (!Directory.Exists(presenceRoot))
        {
            throw new DirectoryNotFoundException($"Franky Presence files were not found at '{presenceRoot}'.");
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var options = AssistantOptions.FromEnvironment(arguments);
        var wakeDatasetRoot = Path.GetFullPath(
            ReadStringArgument(arguments, "--wake-dataset-root") ??
            Path.Combine(webRoot, "..", "wake-word", ".cache", "recordings"));
        var events = new JsonEventSink(Console.Error);
        var wakeDatasetMutationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var commandTool = new NamedCommandTool(new ProcessCommandRunner());
        var deviceActionTool = new DeviceActionTool();
        var toolExecutor = new CompositeToolExecutor(commandTool, deviceActionTool);
        var conversationClient = ConversationClientFactory.Create(options, toolExecutor, events);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IEventSink>(events);
        builder.Services.AddSingleton<IToolExecutor>(toolExecutor);
        builder.Services.AddSingleton(conversationClient);
        builder.Services.AddSingleton<AssistantTurnCoordinator>();
        builder.Services.AddSingleton<ISpeechTranscriber, WhisperNetSpeechTranscriber>();
        builder.Services.AddSingleton(new WakeDatasetStore(wakeDatasetRoot));

        var app = builder.Build();
        var fileProvider = new PhysicalFileProvider(Path.GetFullPath(webRoot));
        var presenceFileProvider = new PhysicalFileProvider(presenceRoot);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = presenceFileProvider,
            RequestPath = "/presence",
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = presenceFileProvider,
            RequestPath = "/presence",
        });

        app.MapGet("/api/transcriptions/status", (ISpeechTranscriber transcriber) =>
            Results.Ok(transcriber.Status));

        app.MapGet("/api/assistant/status", (
            AssistantTurnCoordinator coordinator,
            AssistantOptions assistantOptions) =>
            Results.Ok(new
            {
                provider = coordinator.ProviderName,
                toolSelectionEnabled = assistantOptions.Provider != AssistantProvider.Demo,
                local = assistantOptions.IsLocal,
            }));

        app.MapGet("/api/wake-dataset", async (
            WakeDatasetStore store,
            CancellationToken requestCancellation) =>
            Results.Ok(new
            {
                status = await store.GetStatusAsync(requestCancellation),
                mutationToken = wakeDatasetMutationToken,
            }));

        app.MapPost("/api/wake-dataset/samples", async (
            HttpRequest request,
            WakeDatasetStore store,
            IEventSink events,
            CancellationToken requestCancellation) =>
        {
            if (!HasMutationToken(request, wakeDatasetMutationToken)) return Results.Unauthorized();
            var mediaType = request.ContentType?.Split(';', 2)[0].Trim();
            if (!string.Equals(mediaType, "audio/wav", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    "Franky expects a WAV wake sample.",
                    statusCode: StatusCodes.Status415UnsupportedMediaType);
            }
            if (!request.ContentLength.HasValue ||
                request.ContentLength.Value is <= 0 or > WakeDatasetStore.MaxAudioBytes)
            {
                return Results.Problem(
                    "The wake sample is empty or too large.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            try
            {
                var gainDb = int.TryParse(request.Query["gainDb"], out var parsedGain)
                    ? parsedGain
                    : 30;
                var sample = await store.SaveAsync(
                    request.Query["category"].ToString(),
                    request.Body,
                    new WakeDatasetSampleDetails(
                        request.Query["promptId"],
                        request.Query["prompt"],
                        request.Query["distance"],
                        request.Query["orientation"],
                        gainDb),
                    requestCancellation);
                events.Write("wake_dataset.sample_saved", new Dictionary<string, object?>
                {
                    ["category"] = sample.Category,
                    ["audio_bytes"] = sample.AudioBytes,
                    ["duration_ms"] = sample.DurationMilliseconds,
                });
                return Results.Created($"/api/wake-dataset/samples/{sample.Id}/audio", sample);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (InvalidDataException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status409Conflict);
            }
        });

        app.MapDelete("/api/wake-dataset/samples/{id}", async (
            string id,
            HttpRequest request,
            WakeDatasetStore store,
            CancellationToken requestCancellation) =>
        {
            if (!HasMutationToken(request, wakeDatasetMutationToken)) return Results.Unauthorized();
            return await store.DeleteAsync(id, requestCancellation)
                ? Results.NoContent()
                : Results.NotFound();
        });

        app.MapDelete("/api/wake-dataset", async (
            HttpRequest request,
            WakeDatasetStore store,
            IEventSink events,
            CancellationToken requestCancellation) =>
        {
            if (!HasMutationToken(request, wakeDatasetMutationToken)) return Results.Unauthorized();
            var deleted = await store.DeleteAllAsync(requestCancellation);
            events.Write("wake_dataset.deleted", new Dictionary<string, object?>
            {
                ["sample_count"] = deleted,
            });
            return Results.Ok(new { deleted });
        });

        app.MapPost("/api/assistant/turns", async (
            AssistantTurnRequest request,
            AssistantTurnCoordinator coordinator,
            IEventSink events,
            CancellationToken requestCancellation) =>
        {
            var text = request.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return Results.Problem(
                    "Franky expects a non-empty transcript.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            if (text.Length > MaxTranscriptCharacters)
            {
                return Results.Problem(
                    "The transcript is too large.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            if (KnownDeviceIntentRouter.TryResolve(text, out var actionName))
            {
                var action = await deviceActionTool.ExecuteAsync(
                    new ToolCall(
                        DeviceActionTool.ToolName,
                        JsonSerializer.Serialize(new { action_name = actionName })),
                    requestCancellation);
                events.Write("assistant.local_intent", new Dictionary<string, object?>
                {
                    ["action_name"] = actionName,
                    ["success"] = action.Success,
                });
                return Results.Ok(new
                {
                    Text = "SUUUPER!",
                    ToolCallsExecuted = 1,
                    Actions = new[] { new AssistantActionOutcome(actionName, action.Success) },
                    provider = "Franky local intent",
                });
            }

            try
            {
                var reply = await coordinator.SendAsync(text, requestCancellation);
                return Results.Ok(new
                {
                    reply.Text,
                    reply.ToolCallsExecuted,
                    reply.Actions,
                    provider = coordinator.ProviderName,
                });
            }
            catch (AssistantTurnBusyException exception)
            {
                return Results.Problem(
                    exception.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                events.Write("assistant.turn_failed", new Dictionary<string, object?>
                {
                    ["error_type"] = exception.GetType().Name,
                });
                return Results.Problem(
                    "Franky could not complete that request. Check the runtime output.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        app.MapPost("/api/transcriptions", async (
            HttpRequest request,
            ISpeechTranscriber transcriber,
            IEventSink events,
            CancellationToken requestCancellation) =>
        {
            if (request.ContentType?.StartsWith("audio/wav", StringComparison.OrdinalIgnoreCase) != true)
            {
                return Results.Problem(
                    "Franky expects a WAV audio request.",
                    statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            if (request.ContentLength is <= 0 or > MaxAudioBytes)
            {
                return Results.Problem(
                    "The utterance is empty or too large.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var contentLength = request.ContentLength.GetValueOrDefault();
            var started = Stopwatch.GetTimestamp();
            try
            {
                await using var audio = new MemoryStream((int)contentLength);
                await request.Body.CopyToAsync(audio, requestCancellation);
                if (audio.Length > MaxAudioBytes)
                {
                    return Results.Problem(
                        "The utterance is too large.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                audio.Position = 0;
                var transcript = await transcriber.TranscribeAsync(audio, requestCancellation);
                var elapsed = Stopwatch.GetElapsedTime(started);
                events.Write("speech.transcribed", new Dictionary<string, object?>
                {
                    ["success"] = true,
                    ["model"] = transcript.Model,
                    ["audio_bytes"] = audio.Length,
                    ["elapsed_ms"] = elapsed.TotalMilliseconds,
                    ["text_length"] = transcript.Text.Length,
                });
                return Results.Ok(new
                {
                    transcript.Text,
                    transcript.Model,
                    elapsedMs = Math.Round(elapsed.TotalMilliseconds),
                    local = true,
                });
            }
            catch (InvalidDataException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                events.Write("speech.transcribed", new Dictionary<string, object?>
                {
                    ["success"] = false,
                    ["error_type"] = exception.GetType().Name,
                    ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                });
                return Results.Problem(
                    "Local transcription failed. Check the Franky service output.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        var speechTranscriber = app.Services.GetRequiredService<ISpeechTranscriber>();
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await speechTranscriber.PrepareAsync(app.Lifetime.ApplicationStopping);
                }
                catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Franky local speech model preparation failed: {exception.Message}");
                }
            });
            _ = Task.Run(async () =>
            {
                try
                {
                    await conversationClient.PrepareAsync(app.Lifetime.ApplicationStopping);
                }
                catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Franky conversation provider preparation failed: {exception.Message}");
                }
            });
        });

        Console.WriteLine($"Franky control board: http://127.0.0.1:{port}");
        await app.RunAsync(cancellationToken);
        return 0;
    }

    private static void AddInstalledCudaToProcessPath()
    {
        if (!OperatingSystem.IsWindows()) return;

        var cudaPath = Environment.GetEnvironmentVariable(
            "CUDA_PATH",
            EnvironmentVariableTarget.Machine);
        if (string.IsNullOrWhiteSpace(cudaPath)) return;

        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var currentEntries = currentPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cudaEntries = new[]
        {
            Path.Combine(cudaPath, "bin", "x64"),
            Path.Combine(cudaPath, "bin"),
        }.Where(Directory.Exists).Where(path => !currentEntries.Contains(path));

        Environment.SetEnvironmentVariable(
            "PATH",
            string.Join(Path.PathSeparator, cudaEntries.Append(currentPath)),
            EnvironmentVariableTarget.Process);
    }

    private static string? ReadStringArgument(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }
        return null;
    }

    private static int ReadIntArgument(string[] arguments, string name, int fallback) =>
        int.TryParse(ReadStringArgument(arguments, name), out var parsed) && parsed is > 0 and <= 65535
            ? parsed
            : fallback;

    private static bool HasMutationToken(HttpRequest request, string expected) =>
        request.Headers.TryGetValue("X-Franky-Control-Token", out var supplied) &&
        supplied.Count == 1 &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied.ToString()),
            Encoding.ASCII.GetBytes(expected));
}

public sealed record AssistantTurnRequest(string? Text);
