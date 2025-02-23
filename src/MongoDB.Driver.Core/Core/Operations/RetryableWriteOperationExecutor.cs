﻿/* Copyright 2017-present MongoDB Inc.
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
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver.Core.Operations
{
    internal static class RetryableWriteOperationExecutor
    {
        // public static methods
        public static TResult Execute<TResult>(IRetryableWriteOperation<TResult> operation, IWriteBinding binding, bool retryRequested, CancellationToken cancellationToken)
        {
            using (var context = RetryableWriteContext.Create(binding, retryRequested, cancellationToken))
            {
                return Execute(operation, context, cancellationToken);
            }
        }

        public static TResult Execute<TResult>(IRetryableWriteOperation<TResult> operation, RetryableWriteContext context, CancellationToken cancellationToken)
        {
            if (!AreRetriesAllowed(operation, context))
            {
                return operation.ExecuteAttempt(context, 1, null, cancellationToken);
            }

            var transactionNumber = context.Binding.Session.AdvanceTransactionNumber();
            Exception originalException;
            try
            {
                return operation.ExecuteAttempt(context, 1, transactionNumber, cancellationToken);
            }
            catch (Exception ex) when (RetryabilityHelper.IsRetryableWriteException(ex))
            {
                originalException = ex;
            }

            try
            {
                context.ReplaceChannelSource(context.Binding.GetWriteChannelSource(cancellationToken));
                context.ReplaceChannel(context.ChannelSource.GetChannel(cancellationToken));
            }
            catch
            {
                throw originalException;
            }

            if (!AreRetryableWritesSupported(context.Channel.ConnectionDescription))
            {
                throw originalException;
            }

            try
            {
                return operation.ExecuteAttempt(context, 2, transactionNumber, cancellationToken);
            }
            catch (Exception ex) when (ShouldThrowOriginalException(ex))
            {
                throw originalException;
            }
        }

        public async static Task<TResult> ExecuteAsync<TResult>(IRetryableWriteOperation<TResult> operation, IWriteBinding binding, bool retryRequested, CancellationToken cancellationToken)
        {
            using (var context = await RetryableWriteContext.CreateAsync(binding, retryRequested, cancellationToken).ConfigureAwait(false))
            {
                return await ExecuteAsync(operation, context, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task<TResult> ExecuteAsync<TResult>(IRetryableWriteOperation<TResult> operation, RetryableWriteContext context, CancellationToken cancellationToken)
        {
            if (!AreRetriesAllowed(operation, context))
            {
                return await operation.ExecuteAttemptAsync(context, 1, null, cancellationToken).ConfigureAwait(false);
            }

            var transactionNumber = context.Binding.Session.AdvanceTransactionNumber();
            Exception originalException;
            try
            {
                return await operation.ExecuteAttemptAsync(context, 1, transactionNumber, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (RetryabilityHelper.IsRetryableWriteException(ex))
            {
                originalException = ex;
            }

            try
            {
                context.ReplaceChannelSource(await context.Binding.GetWriteChannelSourceAsync(cancellationToken).ConfigureAwait(false));
                context.ReplaceChannel(await context.ChannelSource.GetChannelAsync(cancellationToken).ConfigureAwait(false));
            }
            catch
            {
                throw originalException;
            }

            if (!AreRetryableWritesSupported(context.Channel.ConnectionDescription))
            {
                throw originalException;
            }

            try
            {
                return await operation.ExecuteAttemptAsync(context, 2, transactionNumber, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ShouldThrowOriginalException(ex))
            {
                throw originalException;
            }
        }

        public static bool ShouldConnectionAcquireBeRetried(RetryableWriteContext context, ServerDescription serverDescription)
        {
            return context.RetryRequested &&
                AreRetryableWritesSupported(serverDescription) &&
                context.Binding.Session.Id != null &&
                !context.Binding.Session.IsInTransaction;
        }

        // private static methods
        private static bool AreRetriesAllowed<TResult>(IRetryableWriteOperation<TResult> operation, RetryableWriteContext context)
        {
            return IsOperationAcknowledged(operation) && DoesContextAllowRetries(context);
        }

        private static bool AreRetryableWritesSupported(ConnectionDescription connectionDescription)
        {
            var helloResult = connectionDescription.HelloResult;
            return
                helloResult.ServerType == ServerType.LoadBalanced ||
                (helloResult.LogicalSessionTimeout != null && helloResult.ServerType != ServerType.Standalone);
        }

        private static bool AreRetryableWritesSupported(ServerDescription serverDescription)
        {
            return serverDescription.Type == ServerType.LoadBalanced ||
                (serverDescription.LogicalSessionTimeout != null && serverDescription.Type != ServerType.Standalone);
        }

        private static bool DoesContextAllowRetries(RetryableWriteContext context)
        {
            return
                context.RetryRequested &&
                AreRetryableWritesSupported(context.Channel.ConnectionDescription) &&
                context.Binding.Session.Id != null &&
                !context.Binding.Session.IsInTransaction;
        }

        private static bool IsOperationAcknowledged<TResult>(IRetryableWriteOperation<TResult> operation)
        {
            var writeConcern = operation.WriteConcern;
            return
                writeConcern == null || // null means use server default write concern which implies acknowledged
                writeConcern.IsAcknowledged;
        }

        private static bool ShouldThrowOriginalException(Exception retryException) =>
            retryException == null ||
            retryException is MongoException && !(retryException is MongoConnectionException || retryException is MongoConnectionPoolPausedException);
    }
}
