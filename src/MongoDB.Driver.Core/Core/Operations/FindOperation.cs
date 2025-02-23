﻿/* Copyright 2015-present MongoDB Inc.
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
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.Servers;
using MongoDB.Driver.Core.WireProtocol;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;

namespace MongoDB.Driver.Core.Operations
{
    /// <summary>
    /// Represents a Find command operation.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    public class FindOperation<TDocument> : IReadOperation<IAsyncCursor<TDocument>>, IExecutableInRetryableReadContext<IAsyncCursor<TDocument>>, IExplainableOperation
    {
        #region static
        // private static fields
        private static IBsonSerializer<BsonDocument> __findCommandResultSerializer = new PartiallyRawBsonDocumentSerializer(
            "cursor", new PartiallyRawBsonDocumentSerializer(
                "firstBatch", new RawBsonArraySerializer()));
        #endregion

        // fields
        private bool? _allowDiskUse;
        private bool? _allowPartialResults;
        private int? _batchSize;
        private Collation _collation;
        private readonly CollectionNamespace _collectionNamespace;
        private string _comment;
        private CursorType _cursorType;
        private BsonDocument _filter;
        private int? _firstBatchSize;
        private BsonValue _hint;
        private BsonDocument _let;
        private int? _limit;
        private BsonDocument _max;
        private TimeSpan? _maxAwaitTime;
        private int? _maxScan;
        private TimeSpan? _maxTime;
        private readonly MessageEncoderSettings _messageEncoderSettings;
        private BsonDocument _min;
        private BsonDocument _modifiers;
        private bool? _noCursorTimeout;
        private bool? _oplogReplay;
        private BsonDocument _projection;
        private ReadConcern _readConcern = ReadConcern.Default;
        private readonly IBsonSerializer<TDocument> _resultSerializer;
        private bool _retryRequested;
        private bool? _returnKey;
        private bool? _showRecordId;
        private bool? _singleBatch;
        private int? _skip;
        private bool? _snapshot;
        private BsonDocument _sort;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="FindOperation{TDocument}"/> class.
        /// </summary>
        /// <param name="collectionNamespace">The collection namespace.</param>
        /// <param name="resultSerializer">The result serializer.</param>
        /// <param name="messageEncoderSettings">The message encoder settings.</param>
        public FindOperation(
            CollectionNamespace collectionNamespace,
            IBsonSerializer<TDocument> resultSerializer,
            MessageEncoderSettings messageEncoderSettings)
        {
            _collectionNamespace = Ensure.IsNotNull(collectionNamespace, nameof(collectionNamespace));
            _resultSerializer = Ensure.IsNotNull(resultSerializer, nameof(resultSerializer));
            _messageEncoderSettings = Ensure.IsNotNull(messageEncoderSettings, nameof(messageEncoderSettings));
            _cursorType = CursorType.NonTailable;
        }

        // properties
        /// <summary>
        /// Gets or sets a value indicating whether the server is allowed to write to disk while executing the Find operation.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the server is allowed to write to disk while executing the Find operation; otherwise, <c>false</c>.
        /// </value>
        public bool? AllowDiskUse
        {
            get { return _allowDiskUse; }
            set { _allowDiskUse = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the server is allowed to return partial results if any shards are unavailable.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the server is allowed to return partial results if any shards are unavailable; otherwise, <c>false</c>.
        /// </value>
        public bool? AllowPartialResults
        {
            get { return _allowPartialResults; }
            set { _allowPartialResults = value; }
        }

        /// <summary>
        /// Gets or sets the size of a batch.
        /// </summary>
        /// <value>
        /// The size of a batch.
        /// </value>
        public int? BatchSize
        {
            get { return _batchSize; }
            set { _batchSize = Ensure.IsNullOrGreaterThanOrEqualToZero(value, nameof(value)); }
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
        /// Gets or sets the type of the cursor.
        /// </summary>
        /// <value>
        /// The type of the cursor.
        /// </value>
        public CursorType CursorType
        {
            get { return _cursorType; }
            set { _cursorType = value; }
        }

        /// <summary>
        /// Gets or sets the filter.
        /// </summary>
        /// <value>
        /// The filter.
        /// </value>
        public BsonDocument Filter
        {
            get { return _filter; }
            set { _filter = value; }
        }

        /// <summary>
        /// Gets or sets the size of the first batch.
        /// </summary>
        /// <value>
        /// The size of the first batch.
        /// </value>
        public int? FirstBatchSize
        {
            get { return _firstBatchSize; }
            set { _firstBatchSize = Ensure.IsNullOrGreaterThanOrEqualToZero(value, nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the hint.
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
        /// Gets or sets the let document.
        /// </summary>
        /// <value>
        /// The let document.
        /// </value>
        public BsonDocument Let
        {
            get { return _let; }
            set { _let = value; }
        }

        /// <summary>
        /// Gets or sets the limit.
        /// </summary>
        /// <value>
        /// The limit.
        /// </value>
        public int? Limit
        {
            get { return _limit; }
            set { _limit = value; }
        }

        /// <summary>
        /// Gets or sets the max key value.
        /// </summary>
        /// <value>
        /// The max key value.
        /// </value>
        public BsonDocument Max
        {
            get { return _max; }
            set { _max = value; }
        }

        /// <summary>
        /// Gets or sets the maximum await time for TailableAwait cursors.
        /// </summary>
        /// <value>
        /// The maximum await time for TailableAwait cursors.
        /// </value>
        public TimeSpan? MaxAwaitTime
        {
            get { return _maxAwaitTime; }
            set { _maxAwaitTime = value; }
        }

        /// <summary>
        /// Gets or sets the max scan.
        /// </summary>
        /// <value>
        /// The max scan.
        /// </value>
        [Obsolete("MaxScan was deprecated in server version 4.0.")]
        public int? MaxScan
        {
            get { return _maxScan; }
            set { _maxScan = Ensure.IsNullOrGreaterThanZero(value, nameof(value)); }
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
        /// Gets or sets the min key value.
        /// </summary>
        /// <value>
        /// The max min value.
        /// </value>
        public BsonDocument Min
        {
            get { return _min; }
            set { _min = value; }
        }

        /// <summary>
        /// Gets or sets any additional query modifiers.
        /// </summary>
        /// <value>
        /// The additional query modifiers.
        /// </value>
        [Obsolete("Use individual properties instead.")]
        public BsonDocument Modifiers
        {
            get { return _modifiers; }
            set { _modifiers = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the server will not timeout the cursor.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the server will not timeout the cursor; otherwise, <c>false</c>.
        /// </value>
        public bool? NoCursorTimeout
        {
            get { return _noCursorTimeout; }
            set { _noCursorTimeout = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the OplogReplay bit will be set.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the OplogReplay bit will be set; otherwise, <c>false</c>.
        /// </value>
        [Obsolete("OplogReplay is ignored by server versions 4.4.0 and newer.")]
        public bool? OplogReplay
        {
            get { return _oplogReplay; }
            set { _oplogReplay = value; }
        }

        /// <summary>
        /// Gets or sets the projection.
        /// </summary>
        /// <value>
        /// The projection.
        /// </value>
        public BsonDocument Projection
        {
            get { return _projection; }
            set { _projection = value; }
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
            set { _readConcern = Ensure.IsNotNull(value, nameof(value)); }
        }

        /// <summary>
        /// Gets the result serializer.
        /// </summary>
        /// <value>
        /// The result serializer.
        /// </value>
        public IBsonSerializer<TDocument> ResultSerializer
        {
            get { return _resultSerializer; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to retry.
        /// </summary>
        /// <value>Whether to retry.</value>
        public bool RetryRequested
        {
            get => _retryRequested;
            set => _retryRequested = value;
        }

        /// <summary>
        /// Gets or sets whether to only return the key values.
        /// </summary>
        /// <value>
        /// Whether to only return the key values.
        /// </value>
        public bool? ReturnKey
        {
            get { return _returnKey; }
            set { _returnKey = value; }
        }

        /// <summary>
        /// Gets or sets whether the record Id should be added to the result document.
        /// </summary>
        /// <value>
        /// Whether the record Id should be added to the result documentr.
        /// </value>
        public bool? ShowRecordId
        {
            get { return _showRecordId; }
            set { _showRecordId = value; }
        }

        /// <summary>
        /// Gets or sets whether to return only a single batch.
        /// </summary>
        /// <value>
        /// Whether to return only a single batchThe single batch.
        /// </value>
        public bool? SingleBatch
        {
            get { return _singleBatch; }
            set { _singleBatch = value; }
        }

        /// <summary>
        /// Gets or sets the number of documents skip.
        /// </summary>
        /// <value>
        /// The number of documents skip.
        /// </value>
        public int? Skip
        {
            get { return _skip; }
            set { _skip = Ensure.IsNullOrGreaterThanOrEqualToZero(value, nameof(value)); }
        }

        /// <summary>
        /// Gets or sets whether to use snapshot behavior.
        /// </summary>
        /// <value>
        /// Whether to use snapshot behavior.
        /// </value>
        [Obsolete("Snapshot was deprecated in server version 3.7.4.")]
        public bool? Snapshot
        {
            get { return _snapshot; }
            set { _snapshot = value; }
        }

        /// <summary>
        /// Gets or sets the sort specification.
        /// </summary>
        /// <value>
        /// The sort specification.
        /// </value>
        public BsonDocument Sort
        {
            get { return _sort; }
            set { _sort = value; }
        }

        // methods
        /// <inheritdoc/>
        public BsonDocument CreateCommand(ConnectionDescription connectionDescription, ICoreSession session)
        {
            var firstBatchSize = _firstBatchSize ?? (_batchSize > 0 ? _batchSize : null);
            var isShardRouter = connectionDescription.HelloResult.ServerType == ServerType.ShardRouter;

            var effectiveComment = _comment;
            var effectiveHint = _hint;
            var effectiveMax = _max;
            var effectiveMaxScan = _maxScan;
            var effectiveMaxTime = _maxTime;
            var effectiveMin = _min;
            var effectiveReturnKey = _returnKey;
            var effectiveShowRecordId = _showRecordId;
            var effectiveSnapshot = _snapshot;
            var effectiveSort = _sort;

            if (_modifiers != null)
            {
                // modifiers don't override explicitly set properties
                foreach (var element in _modifiers)
                {
                    var value = element.Value;
                    switch (element.Name)
                    {
                        case "$comment": effectiveComment = _comment ?? value.AsString; break;
                        case "$hint": effectiveHint = _hint ?? value; break;
                        case "$max": effectiveMax = _max ?? value.AsBsonDocument; break;
                        case "$maxScan": effectiveMaxScan = _maxScan ?? value.ToInt32(); break;
                        case "$maxTimeMS": effectiveMaxTime = _maxTime ?? TimeSpan.FromMilliseconds(value.ToDouble()); break;
                        case "$min": effectiveMin = _min ?? value.AsBsonDocument; break;
                        case "$orderby": effectiveSort = _sort ?? value.AsBsonDocument; break;
                        case "$returnKey": effectiveReturnKey = _returnKey ?? value.ToBoolean(); break;
                        case "$showDiskLoc": effectiveShowRecordId = _showRecordId ?? value.ToBoolean(); break;
                        case "$snapshot": effectiveSnapshot = _snapshot ?? value.ToBoolean(); break;
                        default: throw new ArgumentException($"Modifier not supported by the Find command: '{element.Name}'.");
                    }
                }
            }

            var readConcern = ReadConcernHelper.GetReadConcernForCommand(session, connectionDescription, _readConcern);
            return new BsonDocument
            {
                { "find", _collectionNamespace.CollectionName },
                { "filter", _filter, _filter != null },
                { "sort", effectiveSort, effectiveSort != null },
                { "projection", _projection, _projection != null },
                { "hint", effectiveHint, effectiveHint != null },
                { "skip", () => _skip.Value, _skip.HasValue },
                { "limit", () => Math.Abs(_limit.Value), _limit.HasValue && _limit != 0 },
                { "batchSize", () => firstBatchSize.Value, firstBatchSize.HasValue },
                { "singleBatch", () => _limit < 0 || _singleBatch.Value, _limit < 0 || _singleBatch.HasValue },
                { "comment", effectiveComment, effectiveComment != null },
                { "maxScan", () => effectiveMaxScan.Value, effectiveMaxScan.HasValue },
                { "maxTimeMS", () => MaxTimeHelper.ToMaxTimeMS(effectiveMaxTime.Value), effectiveMaxTime.HasValue },
                { "max", effectiveMax, effectiveMax != null },
                { "min", effectiveMin, effectiveMin != null },
                { "returnKey", () => effectiveReturnKey.Value, effectiveReturnKey.HasValue },
                { "showRecordId", () => effectiveShowRecordId.Value, effectiveShowRecordId.HasValue },
                { "snapshot", () => effectiveSnapshot.Value, effectiveSnapshot.HasValue },
                { "tailable", true, _cursorType == CursorType.Tailable || _cursorType == CursorType.TailableAwait },
                { "oplogReplay", () => _oplogReplay.Value, _oplogReplay.HasValue },
                { "noCursorTimeout", () => _noCursorTimeout.Value, _noCursorTimeout.HasValue },
                { "awaitData", true, _cursorType == CursorType.TailableAwait },
                { "allowDiskUse", () => _allowDiskUse.Value, _allowDiskUse.HasValue },
                { "allowPartialResults", () => _allowPartialResults.Value, _allowPartialResults.HasValue && isShardRouter },
                { "collation", () => _collation.ToBsonDocument(), _collation != null },
                { "readConcern", readConcern, readConcern != null },
                { "let", _let, _let != null }
            };
        }

        private AsyncCursor<TDocument> CreateCursor(IChannelSourceHandle channelSource, IChannelHandle channel, BsonDocument commandResult)
        {
            var cursorDocument = commandResult["cursor"].AsBsonDocument;
            var collectionNamespace = CollectionNamespace.FromFullName(cursorDocument["ns"].AsString);
            var firstBatch = CreateFirstCursorBatch(cursorDocument);
            var getMoreChannelSource = ChannelPinningHelper.CreateGetMoreChannelSource(channelSource, channel, firstBatch.CursorId);

            if (cursorDocument.TryGetValue("atClusterTime", out var atClusterTime))
            {
                channelSource.Session.SetSnapshotTimeIfNeeded(atClusterTime.AsBsonTimestamp);
            }

            return new AsyncCursor<TDocument>(
                getMoreChannelSource,
                collectionNamespace,
                firstBatch.Documents,
                firstBatch.CursorId,
                _batchSize,
                _limit < 0 ? Math.Abs(_limit.Value) : _limit,
                _resultSerializer,
                _messageEncoderSettings,
                _cursorType == CursorType.TailableAwait ? _maxAwaitTime : null);
        }

        private CursorBatch<TDocument> CreateFirstCursorBatch(BsonDocument cursorDocument)
        {
            var cursorId = cursorDocument["id"].ToInt64();
            var batch = (RawBsonArray)cursorDocument["firstBatch"];

            using (batch)
            {
                var documents = CursorBatchDeserializationHelper.DeserializeBatch(batch, _resultSerializer, _messageEncoderSettings);
                return new CursorBatch<TDocument>(cursorId, documents);
            }
        }

        /// <inheritdoc/>
        public IAsyncCursor<TDocument> Execute(IReadBinding binding, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(binding, nameof(binding));

            using (var context = RetryableReadContext.Create(binding, _retryRequested, cancellationToken))
            {
                return Execute(context, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public IAsyncCursor<TDocument> Execute(RetryableReadContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(context, nameof(context));

            using (EventContext.BeginOperation())
            using (EventContext.BeginFind(_batchSize, _limit))
            {
                var operation = CreateOperation(context);
                var commandResult = operation.Execute(context, cancellationToken);
                return CreateCursor(context.ChannelSource, context.Channel, commandResult);
            }
        }

        /// <inheritdoc/>
        public async Task<IAsyncCursor<TDocument>> ExecuteAsync(IReadBinding binding, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(binding, nameof(binding));

            using (var context = await RetryableReadContext.CreateAsync(binding, _retryRequested, cancellationToken).ConfigureAwait(false))
            {
                return await ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<IAsyncCursor<TDocument>> ExecuteAsync(RetryableReadContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(context, nameof(context));

            using (EventContext.BeginOperation())
            using (EventContext.BeginFind(_batchSize, _limit))
            {
                var operation = CreateOperation(context);
                var commandResult = await operation.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                return CreateCursor(context.ChannelSource, context.Channel, commandResult);
            }
        }

        private ReadCommandOperation<BsonDocument> CreateOperation(RetryableReadContext context)
        {
            var command = CreateCommand(context.Channel.ConnectionDescription, context.Binding.Session);
            var operation = new ReadCommandOperation<BsonDocument>(
                _collectionNamespace.DatabaseNamespace,
                command,
                __findCommandResultSerializer,
                _messageEncoderSettings)
            {
                RetryRequested = _retryRequested // might be overridden by retryable read context
            };
            return operation;
        }
    }
}
