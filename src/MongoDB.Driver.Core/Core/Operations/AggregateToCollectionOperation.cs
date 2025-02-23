/* Copyright 2013-present MongoDB Inc.
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.Servers;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;
using MongoDB.Shared;

namespace MongoDB.Driver.Core.Operations
{
    /// <summary>
    /// Represents an aggregate operation that writes the results to an output collection.
    /// </summary>
    public class AggregateToCollectionOperation : IWriteOperation<BsonDocument>
    {
        // fields
        private bool? _allowDiskUse;
        private bool? _bypassDocumentValidation;
        private Collation _collation;
        private readonly CollectionNamespace _collectionNamespace;
        private string _comment;
        private readonly DatabaseNamespace _databaseNamespace;
        private BsonValue _hint;
        private BsonDocument _let;
        private TimeSpan? _maxTime;
        private readonly MessageEncoderSettings _messageEncoderSettings;
        private readonly IReadOnlyList<BsonDocument> _pipeline;
        private ReadConcern _readConcern;
        private ReadPreference _readPreference;
        private WriteConcern _writeConcern;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateToCollectionOperation"/> class.
        /// </summary>
        /// <param name="databaseNamespace">The database namespace.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="messageEncoderSettings">The message encoder settings.</param>
        public AggregateToCollectionOperation(DatabaseNamespace databaseNamespace, IEnumerable<BsonDocument> pipeline, MessageEncoderSettings messageEncoderSettings)
        {
            _databaseNamespace = Ensure.IsNotNull(databaseNamespace, nameof(databaseNamespace));
            _pipeline = Ensure.IsNotNull(pipeline, nameof(pipeline)).ToList();
            _messageEncoderSettings = Ensure.IsNotNull(messageEncoderSettings, nameof(messageEncoderSettings));

            EnsureIsOutputToCollectionPipeline();
            _pipeline = SimplifyOutStageIfOutputDatabaseIsSameAsInputDatabase(_pipeline);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateToCollectionOperation"/> class.
        /// </summary>
        /// <param name="collectionNamespace">The collection namespace.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="messageEncoderSettings">The message encoder settings.</param>
        public AggregateToCollectionOperation(CollectionNamespace collectionNamespace, IEnumerable<BsonDocument> pipeline, MessageEncoderSettings messageEncoderSettings)
            : this(Ensure.IsNotNull(collectionNamespace, nameof(collectionNamespace)).DatabaseNamespace, pipeline, messageEncoderSettings)
        {
            _collectionNamespace = collectionNamespace;
        }

        // properties
        /// <summary>
        /// Gets or sets a value indicating whether the server is allowed to use the disk.
        /// </summary>
        /// <value>
        /// A value indicating whether the server is allowed to use the disk.
        /// </value>
        public bool? AllowDiskUse
        {
            get { return _allowDiskUse; }
            set { _allowDiskUse = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to bypass document validation.
        /// </summary>
        /// <value>
        /// A value indicating whether to bypass document validation.
        /// </value>
        public bool? BypassDocumentValidation
        {
            get { return _bypassDocumentValidation; }
            set { _bypassDocumentValidation = value; }
        }

        /// <summary>
        /// Gets or sets the collation.
        /// </summary>
        /// <value>
        /// The collation.
        /// </value>
        public Collation Collation
        {
            get { return _collation; }
            set { _collation = value; }
        }

        /// <summary>
        /// Gets the collection namespace.
        /// </summary>
        /// <value>
        /// The collection namespace.
        /// </value>
        public CollectionNamespace CollectionNamespace
        {
            get { return _collectionNamespace; }
        }

        /// <summary>
        /// Gets or sets the comment.
        /// </summary>
        /// <value>
        /// The comment.
        /// </value>
        public string Comment
        {
            get { return _comment; }
            set { _comment = value; }
        }

        /// <summary>
        /// Gets the database namespace.
        /// </summary>
        /// <value>
        /// The database namespace.
        /// </value>
        public DatabaseNamespace DatabaseNamespace
        {
            get { return _databaseNamespace; }
        }

        /// <summary>
        /// Gets or sets the hint. This must either be a BsonString representing the index name or a BsonDocument representing the key pattern of the index.
        /// </summary>
        /// <value>
        /// The hint.
        /// </value>
        public BsonValue Hint
        {
            get { return _hint; }
            set { _hint = value; }
        }

        /// <summary>
        /// Gets or sets the "let" definition.
        /// </summary>
        /// <value>
        /// The "let" definition.
        /// </value>
        public BsonDocument Let
        {
            get { return _let; }
            set { _let = value; }
        }

        /// <summary>
        /// Gets or sets the maximum time the server should spend on this operation.
        /// </summary>
        /// <value>
        /// The maximum time the server should spend on this operation.
        /// </value>
        public TimeSpan? MaxTime
        {
            get { return _maxTime; }
            set { _maxTime = Ensure.IsNullOrInfiniteOrGreaterThanOrEqualToZero(value, nameof(value)); }
        }

        /// <summary>
        /// Gets the message encoder settings.
        /// </summary>
        /// <value>
        /// The message encoder settings.
        /// </value>
        public MessageEncoderSettings MessageEncoderSettings
        {
            get { return _messageEncoderSettings; }
        }

        /// <summary>
        /// Gets the pipeline.
        /// </summary>
        /// <value>
        /// The pipeline.
        /// </value>
        public IReadOnlyList<BsonDocument> Pipeline
        {
            get { return _pipeline; }
        }

        /// <summary>
        /// Gets or sets the read concern.
        /// </summary>
        /// <value>
        /// The read concern.
        /// </value>
        public ReadConcern ReadConcern
        {
            get { return _readConcern; }
            set
            {
                _readConcern = value;
            }
        }

        /// <summary>
        /// Gets or sets the read preference.
        /// </summary>
        public ReadPreference ReadPreference
        {
            get { return _readPreference; }
            set
            {
                _readPreference = value;
            }
        }

        /// <summary>
        /// Gets or sets the write concern.
        /// </summary>
        /// <value>
        /// The write concern.
        /// </value>
        public WriteConcern WriteConcern
        {
            get { return _writeConcern; }
            set { _writeConcern = value; }
        }

        // methods
        /// <inheritdoc/>
        public BsonDocument Execute(IWriteBinding binding, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(binding, nameof(binding));

            var mayUseSecondary = new MayUseSecondary(_readPreference);
            using (var channelSource = binding.GetWriteChannelSource(mayUseSecondary, cancellationToken))
            using (var channel = channelSource.GetChannel(cancellationToken))
            using (var channelBinding = new ChannelReadWriteBinding(channelSource.Server, channel, binding.Session.Fork()))
            {
                var operation = CreateOperation(channelBinding.Session, channel.ConnectionDescription, mayUseSecondary.EffectiveReadPreference);
                return operation.Execute(channelBinding, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task<BsonDocument> ExecuteAsync(IWriteBinding binding, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(binding, nameof(binding));

            var mayUseSecondary = new MayUseSecondary(_readPreference);
            using (var channelSource = await binding.GetWriteChannelSourceAsync(mayUseSecondary, cancellationToken).ConfigureAwait(false))
            using (var channel = await channelSource.GetChannelAsync(cancellationToken).ConfigureAwait(false))
            using (var channelBinding = new ChannelReadWriteBinding(channelSource.Server, channel, binding.Session.Fork()))
            {
                var operation = CreateOperation(channelBinding.Session, channel.ConnectionDescription, mayUseSecondary.EffectiveReadPreference);
                return await operation.ExecuteAsync(channelBinding, cancellationToken).ConfigureAwait(false);
            }
        }

        internal BsonDocument CreateCommand(ICoreSessionHandle session, ConnectionDescription connectionDescription)
        {
            var readConcern = _readConcern != null
                ? ReadConcernHelper.GetReadConcernForCommand(session, connectionDescription, _readConcern)
                : null;
            var writeConcern = WriteConcernHelper.GetEffectiveWriteConcern(session, _writeConcern);
            return new BsonDocument
            {
                { "aggregate", _collectionNamespace == null ? (BsonValue)1 : _collectionNamespace.CollectionName },
                { "pipeline", new BsonArray(_pipeline) },
                { "allowDiskUse", () => _allowDiskUse.Value, _allowDiskUse.HasValue },
                { "bypassDocumentValidation", () => _bypassDocumentValidation.Value, _bypassDocumentValidation.HasValue },
                { "maxTimeMS", () => MaxTimeHelper.ToMaxTimeMS(_maxTime.Value), _maxTime.HasValue },
                { "collation", () => _collation.ToBsonDocument(), _collation != null },
                { "readConcern", readConcern, readConcern != null },
                { "writeConcern", writeConcern, writeConcern != null },
                { "cursor", new BsonDocument() },
                { "hint", () => _hint, _hint != null },
                { "let", () => _let, _let != null },
                { "comment", () => _comment, _comment != null }
            };
        }

        private WriteCommandOperation<BsonDocument> CreateOperation(ICoreSessionHandle session, ConnectionDescription connectionDescription, ReadPreference effectiveReadPreference)
        {
            var command = CreateCommand(session, connectionDescription);
            var operation = new WriteCommandOperation<BsonDocument>(_databaseNamespace, command, BsonDocumentSerializer.Instance, MessageEncoderSettings);
            if (effectiveReadPreference != null)
            {
                operation.ReadPreference = effectiveReadPreference;
            }
            return operation;
        }

        private void EnsureIsOutputToCollectionPipeline()
        {
            var lastStage = _pipeline.LastOrDefault();
            var lastStageName = lastStage?.GetElement(0).Name;
            if (lastStage == null || (lastStageName != "$out" && lastStageName != "$merge"))
            {
                throw new ArgumentException("The last stage of the pipeline for an AggregateOutputToCollectionOperation must have a $out or $merge operator.", "pipeline");
            }
        }

        private IReadOnlyList<BsonDocument> SimplifyOutStageIfOutputDatabaseIsSameAsInputDatabase(IReadOnlyList<BsonDocument> pipeline)
        {
            var lastStage = pipeline.Last();
            var lastStageName = lastStage.GetElement(0).Name;
            if (lastStageName == "$out" && lastStage["$out"] is BsonDocument outDocument)
            {
                if (outDocument.TryGetValue("db", out var db) && db.IsString &&
                    outDocument.TryGetValue("coll", out var coll) && coll.IsString)
                {
                    var outputDatabaseName = db.AsString;
                    if (outputDatabaseName == _databaseNamespace.DatabaseName)
                    {
                        var outputCollectionName = coll.AsString;
                        var simplifiedOutStage = lastStage.Clone().AsBsonDocument;
                        simplifiedOutStage["$out"] = outputCollectionName;

                        var modifiedPipeline = new List<BsonDocument>(pipeline);
                        modifiedPipeline[modifiedPipeline.Count - 1] = simplifiedOutStage;

                        return modifiedPipeline;
                    }
                }
            }

            return pipeline; // unchanged
        }

        internal class MayUseSecondary : IMayUseSecondaryCriteria
        {
            public MayUseSecondary(ReadPreference readPreference)
            {
                ReadPreference = EffectiveReadPreference = readPreference;
            }

            public ReadPreference EffectiveReadPreference { get; set; }
            public ReadPreference ReadPreference { get; }

            public bool CanUseSecondary(ServerDescription server)
            {
                return Feature.AggregateOutOnSecondary.IsSupported(server.MaxWireVersion);
            }
        }
    }
}
