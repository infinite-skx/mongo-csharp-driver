/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using MongoDB.Driver.Core.Authentication;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver
{
    /// <summary>
    /// Represents a registry of already created clusters.
    /// </summary>
    public class ClusterRegistry
    {
        #region static
        // static fields
        private static readonly ClusterRegistry __instance = new ClusterRegistry();

        // static properties
        /// <summary>
        /// Gets the default cluster registry.
        /// </summary>
        /// <value>
        /// The default cluster registry.
        /// </value>
        public static ClusterRegistry Instance
        {
            get { return __instance; }
        }
        #endregion

        // fields
        private readonly object _lock = new object();
        private readonly Dictionary<ClusterKey, ICluster> _registry = new Dictionary<ClusterKey, ICluster>();

        // methods
        private ICluster CreateCluster(ClusterKey clusterKey)
        {
            var builder = new ClusterBuilder()
                .ConfigureCluster(settings => ConfigureCluster(settings, clusterKey))
                .ConfigureServer(settings => ConfigureServer(settings, clusterKey))
                .ConfigureConnectionPool(settings => ConfigureConnectionPool(settings, clusterKey))
                .ConfigureConnection(settings => ConfigureConnection(settings, clusterKey))
                .ConfigureTcp(settings => ConfigureTcp(settings, clusterKey))
                .ConfigureSdamLogging(settings => ConfigureSdamLogging(settings, clusterKey));

            if (clusterKey.UseTls)
            {
                builder.ConfigureSsl(settings => ConfigureSsl(settings, clusterKey));
            }

            if (clusterKey.ClusterConfigurator != null)
            {
                clusterKey.ClusterConfigurator(builder);
            }

            var cluster = builder.BuildCluster();
            cluster.Initialize();

            return cluster;
        }

        private ClusterSettings ConfigureCluster(ClusterSettings settings, ClusterKey clusterKey)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var endPoints = clusterKey.Servers.Select(s => EndPointHelper.Parse(s.ToString()));
            var connectionModeSwitch = clusterKey.ConnectionModeSwitch;
            Optional<ClusterConnectionMode> connectionMode = connectionModeSwitch == ConnectionModeSwitch.UseConnectionMode ? clusterKey.ConnectionMode.ToCore() : default;
            Optional<bool?> directConnection = connectionModeSwitch == ConnectionModeSwitch.UseDirectConnection ? clusterKey.DirectConnection : default;
            return settings.With(
                connectionMode: connectionMode,
                connectionModeSwitch: connectionModeSwitch,
                directConnection: directConnection,
                endPoints: Optional.Enumerable(endPoints),
                kmsProviders: Optional.Create(clusterKey.KmsProviders),
                loadBalanced: clusterKey.LoadBalanced,
                localThreshold: clusterKey.LocalThreshold,
                replicaSetName: clusterKey.ReplicaSetName,
                maxServerSelectionWaitQueueSize: clusterKey.WaitQueueSize,
                serverApi: clusterKey.ServerApi,
                serverSelectionTimeout: clusterKey.ServerSelectionTimeout,
                schemaMap: Optional.Create(clusterKey.SchemaMap),
                scheme: clusterKey.Scheme);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private ConnectionPoolSettings ConfigureConnectionPool(ConnectionPoolSettings settings, ClusterKey clusterKey)
        {
            return settings.With(
                // maintenanceInterval: TODO: should this be configurable?
                maxConnecting: clusterKey.MaxConnecting,
                maxConnections: clusterKey.MaxConnectionPoolSize,
                minConnections: clusterKey.MinConnectionPoolSize,
                waitQueueSize: clusterKey.WaitQueueSize,
                waitQueueTimeout: clusterKey.WaitQueueTimeout);
        }

        private ConnectionSettings ConfigureConnection(ConnectionSettings settings, ClusterKey clusterKey)
        {
            var authenticatorFactories = clusterKey.Credentials.Select(c => new AuthenticatorFactory(() => c.ToAuthenticator(clusterKey.ServerApi)));
            return settings.With(
                authenticatorFactories: Optional.Enumerable<IAuthenticatorFactory>(authenticatorFactories),
                compressors: Optional.Enumerable(clusterKey.Compressors),
                loadBalanced: clusterKey.LoadBalanced,
                maxIdleTime: clusterKey.MaxConnectionIdleTime,
                maxLifeTime: clusterKey.MaxConnectionLifeTime,
                applicationName: clusterKey.ApplicationName);
        }

        private SdamLoggingSettings ConfigureSdamLogging(SdamLoggingSettings settings, ClusterKey clusterKey)
        {
            return settings.With(logFilename: clusterKey.SdamLogFilename);
        }

        private ServerSettings ConfigureServer(ServerSettings settings, ClusterKey clusterKey)
        {
            return settings.With(
                heartbeatInterval: clusterKey.HeartbeatInterval,
                heartbeatTimeout: clusterKey.HeartbeatTimeout);
        }

        private SslStreamSettings ConfigureSsl(SslStreamSettings settings, ClusterKey clusterKey)
        {
            if (clusterKey.UseTls)
            {
                var sslSettings = clusterKey.SslSettings ?? new SslSettings();

                var validationCallback = sslSettings.ServerCertificateValidationCallback;
                if (validationCallback == null && clusterKey.AllowInsecureTls)
                {
                    validationCallback = AcceptAnySslCertificate;
                }

                return settings.With(
                    clientCertificates: Optional.Enumerable(sslSettings.ClientCertificates ?? Enumerable.Empty<X509Certificate>()),
                    checkCertificateRevocation: sslSettings.CheckCertificateRevocation,
                    clientCertificateSelectionCallback: sslSettings.ClientCertificateSelectionCallback,
                    enabledProtocols: sslSettings.EnabledSslProtocols,
                    serverCertificateValidationCallback: validationCallback);
            }

            return settings;
        }

        private TcpStreamSettings ConfigureTcp(TcpStreamSettings settings, ClusterKey clusterKey)
        {
            if (clusterKey.IPv6)
            {
                settings = settings.With(addressFamily: AddressFamily.InterNetworkV6);
            }

            return settings.With(
                connectTimeout: clusterKey.ConnectTimeout,
                readTimeout: clusterKey.SocketTimeout,
                receiveBufferSize: clusterKey.ReceiveBufferSize,
                sendBufferSize: clusterKey.SendBufferSize,
                writeTimeout: clusterKey.SocketTimeout);
        }

        internal ICluster GetOrCreateCluster(ClusterKey clusterKey)
        {
            lock (_lock)
            {
                ICluster cluster;
                if (!_registry.TryGetValue(clusterKey, out cluster))
                {
                    cluster = CreateCluster(clusterKey);
                    _registry.Add(clusterKey, cluster);
                }
                return cluster;
            }
        }

        private static bool AcceptAnySslCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        )
        {
            return true;
        }

        /// <summary>
        /// Unregisters and disposes the cluster.
        /// </summary>
        /// <param name="cluster">The cluster.</param>
        public void UnregisterAndDisposeCluster(ICluster cluster)
        {
            Ensure.IsNotNull(cluster, nameof(cluster));

            lock (_lock)
            {
                ClusterKey clusterKey = null;
                foreach (var keyValuePair in _registry)
                {
                    if (object.ReferenceEquals(keyValuePair.Value, cluster))
                    {
                        clusterKey = keyValuePair.Key;
                        break;
                    }
                }

                if (clusterKey != null)
                {
                    _registry.Remove(clusterKey);
                }
            }

            cluster.Dispose();
        }
    }
}
