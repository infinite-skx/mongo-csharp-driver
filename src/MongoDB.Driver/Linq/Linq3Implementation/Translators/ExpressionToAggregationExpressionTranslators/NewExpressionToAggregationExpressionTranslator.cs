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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Linq.Linq3Implementation.Ast;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators
{
    internal static class NewExpressionToAggregationExpressionTranslator
    {
        public static AggregationExpression Translate(TranslationContext context, NewExpression expression)
        {
            if (expression.Type == typeof(DateTime))
            {
                return NewDateTimeExpressionToAggregationExpressionTranslator.Translate(context, expression);
            }
            if (expression.Type.IsConstructedGenericType && expression.Type.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                return NewHashSetExpressionToAggregationExpressionTranslator.Translate(context, expression);
            }
            if (expression.Type.IsConstructedGenericType && expression.Type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return NewListExpressionToAggregationExpressionTranslator.Translate(context, expression);
            }

            var classMapType = typeof(BsonClassMap<>).MakeGenericType(expression.Type);
            var classMap = (BsonClassMap)Activator.CreateInstance(classMapType);
            var computedFields = new List<AstComputedField>();

            for (var i = 0; i < expression.Members.Count; i++)
            {
                var member = expression.Members[i];
                var fieldExpression = expression.Arguments[i];
                var fieldTranslation = ExpressionToAggregationExpressionTranslator.Translate(context, fieldExpression);
                var memberSerializer = fieldTranslation.Serializer ?? BsonSerializer.LookupSerializer(fieldExpression.Type);
                var defaultValue = GetDefaultValue(memberSerializer.ValueType);
                classMap.MapProperty(member.Name).SetSerializer(memberSerializer).SetDefaultValue(defaultValue);
                computedFields.Add(AstExpression.ComputedField(member.Name, fieldTranslation.Ast));
            }

            var constructorInfo = expression.Constructor;
            var constructorArgumentNames = expression.Members.Select(m => m.Name).ToArray();
            classMap.MapConstructor(constructorInfo, constructorArgumentNames);
            classMap.Freeze();

            var ast = AstExpression.ComputedDocument(computedFields);
            var serializerType = typeof(BsonClassMapSerializer<>).MakeGenericType(expression.Type);
            // Note that we should use context.KnownSerializersRegistry to find the serializer,
            // but the above implementation builds up computedFields during the mapping process.
            // We need to figure out how to resolve the serializer from KnownSerializers and then
            // populate computedFields from that resolved serializer.
            var serializer = (IBsonSerializer)Activator.CreateInstance(serializerType, classMap);

            return new AggregationExpression(expression, ast, serializer);
        }

        private static object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            else
            {
                return null;
            }
        }
    }
}
