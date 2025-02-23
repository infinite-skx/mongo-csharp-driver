﻿/* Copyright 2021-present MongoDB Inc.
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
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using FluentAssertions;
using Xunit;

namespace MongoDB.Bson.Tests.Exceptions
{
    public class BsonInternalExceptionTests
    {
        [Fact]
        public void Serialization_should_work()
        {
            var subject = new BsonInternalException("message", new Exception("inner"));

            var formatter = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                formatter.Serialize(stream, subject);
                stream.Position = 0;
                var rehydrated = (BsonInternalException)formatter.Deserialize(stream);

                rehydrated.Message.Should().Be(subject.Message);
                rehydrated.InnerException.Message.Should().Be(subject.InnerException.Message);
            }
        }
    }
}
