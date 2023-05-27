using System.Buffers;
using System.Text;
using System.Text.Json;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using HttpCache.Controllers;
using HttpCache.Data;
using HttpCache.Extensions;
using HttpCache.Settings;

namespace HttpCache.Services;

public class PulsarRequestConsumer : IHostedService
{
    private readonly ILogger<PulsarRequestConsumer> _logger;
    private readonly PulsarSettings _settings;
    private readonly IPulsarClient _client;
    private readonly RequestHandler _handler;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task[]? _tasks;

    public PulsarRequestConsumer(
        ILogger<PulsarRequestConsumer> logger,
        PulsarSettings settings,
        RequestHandler handler
    )
    {
        _logger = logger;
        _settings = settings;
        _handler = handler;
        _client = PulsarClient.Builder()
            .ServiceUrl(new Uri(settings.Url))
            .Build();
    }

    #region IHostedService

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_tasks != null)
            throw new InvalidOperationException("Cannot start request consumer when its already running.");

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _tasks = Enumerable
                .Range(0, _settings.Concurrency)
                .Select(_ => Task.Run(
                        () => DoWork(token),
                        cancellationToken
                    )
                )
                .ToArray();

            return Task.CompletedTask;
        }
        catch (TaskCanceledException)
        {
            _cancellationTokenSource?.Cancel();
            _tasks = null;

            return Task.FromCanceled(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_tasks != null && _cancellationTokenSource != null)
        {
            await Task.WhenAny(
                Task.WhenAll(_tasks),
                cancellationToken.AsTask()
            );

            _tasks = null;
        }
    }

    #endregion

    public IConsumer<T> CreateConsumer<T>(
        string topic,
        string subscriptionName,
        SubscriptionType subscriptionType = SubscriptionType.Shared
    ) => _client
        .NewConsumer(JsonSchema<T>.Default)
        .Topic(topic)
        .SubscriptionName(subscriptionName)
        .SubscriptionType(subscriptionType)
        .Create();

    public IProducer<T> CreateProducer<T>(
        string topic
    ) => _client
        .NewProducer(JsonSchema<T>.Default)
        .Topic(topic)
        .Create();

    private async Task DoWork(CancellationToken cancellationToken)
    {
        var consumer = CreateConsumer<Request>(
            topic: _settings.InputTopic,
            subscriptionName: "httpcache",
            subscriptionType: SubscriptionType.Shared
        );

        try
        {
            var monitor = new PerformanceMonitor();

            var producer = CreateProducer<Response>(
                topic: _settings.OutputTopic
            );
            
            await foreach (var message in consumer.Messages(cancellationToken))
            {
                monitor.ResetAll();

                var request = message.Value();

                var result = await _handler.HandleMessage(
                    request,
                    monitor
                );

                var response = result.Response;
                
                _logger.LogInformation(
                    "Handled {Cached} pulsar request in {Elapsed} ms, with {ElapsedDb} ms of DB delay " +
                    "with destination: {Destination}",
                    result.IsCached
                        ? "cached"
                        : "non-cached",
                    monitor.GetElapsed(Constants.TotalTime).TotalMilliseconds,
                    monitor.GetElapsed(Constants.DatabaseTime).TotalMilliseconds,
                    request.Url ?? "<unknown>"
                );

                await producer.Send(response, cancellationToken);
            }
        }
        finally
        {
            await consumer.Unsubscribe(cancellationToken);
        }
    }

    /// <summary>
    /// A schema class handling encoding and decoding class instances to JSON
    /// format, which is supported by Pulsar 
    /// </summary>
    /// <typeparam name="T">The type of of class the schema encodes and decodes
    /// </typeparam>
    private class JsonSchema<T> : ISchema<T>
    {
        private const string EncodingKey = "__encoding";

        private readonly Encoding _encoding;

        public JsonSchema(Encoding encoding)
        {
            _encoding = encoding;
            var properties = new Dictionary<string, string>
            {
                { EncodingKey, encoding.EncodingName }
            };

            SchemaInfo = new SchemaInfo(
                "String",
                Array.Empty<byte>(),
                SchemaType.String,
                properties
            );
        }

        public static JsonSchema<T> Default => new(Encoding.UTF8);

        public SchemaInfo SchemaInfo { get; }


        /// <summary>
        /// Decodes a sequence of bytes into an instance of the class
        /// </summary>
        /// <param name="bytes">The sequence of bytes to decode</param>
        /// <param name="schemaVersion">A byte array representing a schema version</param>
        /// <returns>An instance of the type</returns>
        /// <exception cref="JsonException">Thrown if decoding fails due to an
        /// invalid object being returned by the json serializer.</exception>
        public T Decode(
            ReadOnlySequence<byte> bytes,
            byte[]? schemaVersion = null
        )
        {
            var json = _encoding.GetString(bytes);
            return JsonSerializer.Deserialize<T>(
                       json
                   ) ??
                   throw new JsonException("Could not deserialize JSON.");
        }

        /// <summary>
        /// Encode an instance of the type to a byte sequence
        /// </summary>
        /// <param name="message">The type instance to encode</param>
        /// <returns>A byte sequence representing the encoded type instance</returns>
        public ReadOnlySequence<byte> Encode(T message)
        {
            var json = JsonSerializer.Serialize(
                message,
                new JsonSerializerOptions()
            );
            return new ReadOnlySequence<byte>(_encoding.GetBytes(json));
        }
    }
}