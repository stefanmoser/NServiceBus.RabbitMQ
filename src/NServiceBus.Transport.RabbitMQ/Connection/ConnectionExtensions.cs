﻿#nullable enable

namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    static class ConnectionExtensions
    {
        public static async Task VerifyBrokerRequirements(this IConnection connection, CancellationToken cancellationToken = default)
        {
            var minimumBrokerVersion = Version.Parse("3.10.0");
            var versionValue = connection.ServerProperties?["version"];
            var versionBytes = Array.Empty<byte>();

            if (versionValue is not null)
            {
                versionBytes = (byte[])versionValue;
            }

            var brokerVersionString = Encoding.UTF8.GetString(versionBytes);
            Version.TryParse(brokerVersionString, out var brokerVersion);

            if (brokerVersion is not null && brokerVersion < minimumBrokerVersion)
            {
                throw new Exception($"An unsupported broker version was detected: {brokerVersion}. The broker must be at least version {minimumBrokerVersion}.");
            }

            var streamFlagRequiredMinimumVersion = Version.Parse("3.12.0");
            if (brokerVersion is null || brokerVersion < streamFlagRequiredMinimumVersion)
            {
                using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                var arguments = new Dictionary<string, object?> { { "x-queue-type", "stream" } };

                try
                {
                    await channel.QueueDeclareAsync("nsb.v2.verify-stream-flag-enabled", true, false, false, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken) && ex.Message.Contains("the corresponding feature flag is disabled"))
                {
                    throw new Exception("An unsupported broker configuration was detected. The 'stream_queue' feature flag needs to be enabled.");
                }
            }
        }
    }
}
