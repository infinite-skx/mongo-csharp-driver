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

using System.Linq.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.Misc;
using MongoDB.Driver.Linq.Linq3Implementation.Reflection;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators.MethodTranslators
{
    internal static class WhereMethodToAggregationExpressionTranslator
    {
        public static AggregationExpression Translate(TranslationContext context, MethodCallExpression expression)
        {
            var method = expression.Method;
            var arguments = expression.Arguments;

            if (method.Is(EnumerableMethod.Where))
            {
                var sourceExpression = arguments[0];
                var sourceTranslation = ExpressionToAggregationExpressionTranslator.TranslateEnumerable(context, sourceExpression);
                var predicateLambda = (LambdaExpression)arguments[1];
                var predicateParameter = predicateLambda.Parameters[0];
                var predicateParameterSerializer = ArraySerializerHelper.GetItemSerializer(sourceTranslation.Serializer);
                var predicateSymbol = context.CreateSymbol(predicateParameter, predicateParameterSerializer);
                var predicateContext = context.WithSymbol(predicateSymbol);
                var predicateTranslation = ExpressionToAggregationExpressionTranslator.Translate(predicateContext, predicateLambda.Body);
                var ast = AstExpression.Filter(
                    sourceTranslation.Ast,
                    predicateTranslation.Ast,
                    predicateParameter.Name);
                return new AggregationExpression(expression, ast, sourceTranslation.Serializer);
            }

            throw new ExpressionNotSupportedException(expression);
        }
    }
}
