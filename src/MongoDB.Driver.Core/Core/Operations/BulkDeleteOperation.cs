﻿/* Copyright 2010-present MongoDB Inc.
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
using MongoDB.Bson;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;

namespace MongoDB.Driver.Core.Operations
{
    internal class BulkDeleteOperation : BulkUnmixedWriteOperationBase<DeleteRequest>
    {
        private BsonDocument _let;

        // constructors
        public BulkDeleteOperation(
            CollectionNamespace collectionNamespace,
            IEnumerable<DeleteRequest> requests,
            MessageEncoderSettings messageEncoderSettings)
            : base(collectionNamespace, requests, messageEncoderSettings)
        {
        }

        // public properties
        public BsonDocument Let
        {
            get => _let;
            set => _let = value;
        }

        // methods
        protected override IRetryableWriteOperation<BsonDocument> CreateBatchOperation(Batch batch)
        {
            return new RetryableDeleteCommandOperation(CollectionNamespace, batch.Requests, MessageEncoderSettings)
            {
                IsOrdered = IsOrdered,
                Let = _let,
                MaxBatchCount = MaxBatchCount,
                RetryRequested = RetryRequested,
                WriteConcern = WriteConcern
            };
        }

        protected override bool RequestHasHint(DeleteRequest request)
        {
            return request.Hint != null;
        }
    }
}
